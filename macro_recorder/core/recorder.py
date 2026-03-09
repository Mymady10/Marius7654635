"""
recorder.py - Mouse and keyboard event recording.

The Recorder class listens to global mouse and keyboard events via pynput,
converts them to MacroStep objects, and accumulates them in an internal list.
Mouse movements are filtered to avoid oversized recordings: a movement step
is only saved when the cursor has moved at least MIN_MOVE_PX pixels or
MIN_MOVE_INTERVAL seconds have elapsed since the last recorded position.

Usage
-----
    rec = Recorder()
    rec.start()
    # ... user performs actions ...
    rec.stop()
    steps = rec.steps
"""

from __future__ import annotations
import threading
import time
from typing import List, Optional, Callable

from .events import MacroStep, StepType
from .screen_utils import point_distance

# Movement filtering thresholds
MIN_MOVE_PX       = 10     # pixels
MIN_MOVE_INTERVAL = 0.05   # seconds

# Minimum interval between any two consecutive events (de-bounce)
MIN_EVENT_INTERVAL = 0.001  # seconds


class Recorder:
    """Records mouse and keyboard actions into a list of MacroStep objects."""

    def __init__(self, on_step: Optional[Callable[[MacroStep], None]] = None):
        """
        Parameters
        ----------
        on_step : optional callback invoked (in the pynput listener thread)
                  each time a new step is appended.
        """
        self.steps: List[MacroStep] = []
        self._on_step = on_step

        self._running  = False
        self._lock     = threading.Lock()

        # Timing
        self._last_event_time: float = 0.0
        self._start_time:      float = 0.0

        # Mouse movement filtering
        self._last_move_x:    int   = -9999
        self._last_move_y:    int   = -9999
        self._last_move_time: float = 0.0

        # Key-press tracking for text compression
        self._pending_chars: List[str] = []

        # pynput listener objects (created fresh each recording session)
        self._mouse_listener    = None
        self._keyboard_listener = None

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def start(self) -> None:
        """Begin recording. Does nothing if already recording."""
        if self._running:
            return
        self.steps = []
        self._pending_chars = []
        self._running  = True
        self._start_time = time.perf_counter()
        self._last_event_time = self._start_time

        self._start_listeners()

    def stop(self) -> None:
        """Stop recording and flush any pending text step."""
        if not self._running:
            return
        self._running = False
        self._flush_pending_chars()
        self._stop_listeners()

    @property
    def is_recording(self) -> bool:
        return self._running

    # ------------------------------------------------------------------
    # pynput listener management
    # ------------------------------------------------------------------

    def _start_listeners(self) -> None:
        try:
            from pynput import mouse as _mouse, keyboard as _keyboard

            self._mouse_listener = _mouse.Listener(
                on_move=self._on_mouse_move,
                on_click=self._on_mouse_click,
                on_scroll=self._on_mouse_scroll,
            )
            self._keyboard_listener = _keyboard.Listener(
                on_press=self._on_key_press,
                on_release=self._on_key_release,
            )
            self._mouse_listener.start()
            self._keyboard_listener.start()
        except Exception as exc:
            self._running = False
            raise RuntimeError(f"Could not start pynput listeners: {exc}") from exc

    def _stop_listeners(self) -> None:
        for listener in (self._mouse_listener, self._keyboard_listener):
            if listener is not None:
                try:
                    listener.stop()
                except Exception:
                    pass
        self._mouse_listener    = None
        self._keyboard_listener = None

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    def _elapsed(self) -> float:
        """Seconds since recording started."""
        return time.perf_counter() - self._start_time

    def _compute_delay(self) -> float:
        """Return seconds since last recorded event."""
        now   = time.perf_counter()
        delay = max(0.0, now - self._last_event_time)
        self._last_event_time = now
        return round(delay, 4)

    def _append(self, step: MacroStep) -> None:
        with self._lock:
            self.steps.append(step)
        if self._on_step:
            try:
                self._on_step(step)
            except Exception:
                pass

    def _flush_pending_chars(self) -> None:
        """Collapse accumulated printable characters into a single text step."""
        if not self._pending_chars:
            return
        text = "".join(self._pending_chars)
        self._pending_chars = []
        delay = self._compute_delay()
        self._append(MacroStep(step_type=StepType.TEXT, value=text,
                               delay_after=delay))

    # ------------------------------------------------------------------
    # Mouse callbacks
    # ------------------------------------------------------------------

    def _on_mouse_move(self, x: int, y: int) -> None:
        if not self._running:
            return
        now = time.perf_counter()
        dist = point_distance(x, y, self._last_move_x, self._last_move_y)
        time_since = now - self._last_move_time
        if dist < MIN_MOVE_PX and time_since < MIN_MOVE_INTERVAL:
            return  # filter redundant micro-movements

        self._flush_pending_chars()
        delay = self._compute_delay()
        self._last_move_x    = x
        self._last_move_y    = y
        self._last_move_time = now
        self._append(MacroStep(step_type=StepType.MOUSE_MOVE,
                               x=x, y=y, delay_after=delay))

    def _on_mouse_click(self, x: int, y: int, button, pressed: bool) -> None:
        if not self._running:
            return
        self._flush_pending_chars()
        delay      = self._compute_delay()
        btn_name   = button.name if hasattr(button, "name") else str(button)
        self._append(MacroStep(step_type=StepType.MOUSE_CLICK,
                               x=x, y=y, button=btn_name,
                               pressed=pressed, delay_after=delay))

    def _on_mouse_scroll(self, x: int, y: int, dx: int, dy: int) -> None:
        if not self._running:
            return
        self._flush_pending_chars()
        delay = self._compute_delay()
        self._append(MacroStep(step_type=StepType.MOUSE_SCROLL,
                               x=x, y=y, value=dy, delay_after=delay))

    # ------------------------------------------------------------------
    # Keyboard callbacks
    # ------------------------------------------------------------------

    def _on_key_press(self, key) -> None:
        if not self._running:
            return
        key_str = self._key_to_str(key)
        # Accumulate printable single characters for text compression
        if len(key_str) == 1 and key_str.isprintable():
            self._pending_chars.append(key_str)
            return
        # Special key → flush accumulated text first, then record press
        self._flush_pending_chars()
        delay = self._compute_delay()
        self._append(MacroStep(step_type=StepType.KEY_PRESS,
                               key=key_str, delay_after=delay))

    def _on_key_release(self, key) -> None:
        if not self._running:
            return
        key_str = self._key_to_str(key)
        # Printable chars are collapsed into text steps; skip individual releases
        if len(key_str) == 1 and key_str.isprintable():
            return
        # For special keys, flush pending chars then record the release
        self._flush_pending_chars()
        delay = self._compute_delay()
        self._append(MacroStep(step_type=StepType.KEY_RELEASE,
                               key=key_str, delay_after=delay))

    # ------------------------------------------------------------------
    # Key name helper
    # ------------------------------------------------------------------

    @staticmethod
    def _key_to_str(key) -> str:
        """Convert a pynput Key or KeyCode to a canonical string."""
        try:
            # KeyCode with a printable character
            if key.char is not None:
                return key.char
        except AttributeError:
            pass
        try:
            # Named special key (e.g. Key.ctrl_l → 'ctrl_l')
            return key.name
        except AttributeError:
            pass
        return str(key)
