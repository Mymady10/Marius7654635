"""
player.py - Macro playback engine.

The Player class executes a list of MacroStep objects, respecting delays,
supporting loop counts, speed multipliers, and three playback modes:

  - normal    : exact delays from recording.
  - fast      : delays capped at FAST_MAX_DELAY.
  - humanized : delays ±HUMAN_JITTER jitter; mouse moves via smooth curves.

Playback runs in a background thread.  A threading.Event (stop_event) can be
set at any time to abort playback immediately.
"""

from __future__ import annotations

import random
import threading
import time
import math
from typing import List, Optional, Callable

from .events import MacroStep, StepType
from .screen_utils import find_image_on_screen, pixel_color

# ---------------------------------------------------------------------------
# Playback constants
# ---------------------------------------------------------------------------
FAST_MAX_DELAY   = 0.05   # seconds – maximum delay in fast mode
HUMAN_JITTER     = 0.20   # ± fraction of delay added in humanized mode
HUMAN_MIN_STEPS  = 10     # minimum mouse-move sub-steps in humanized mode
INFINITE_LOOPS   = -1     # sentinel for infinite looping


class Player:
    """Plays back a list of MacroStep objects."""

    def __init__(
        self,
        steps: List[MacroStep],
        loop_count: int = 1,
        speed_multiplier: float = 1.0,
        playback_mode: str = "normal",
        on_step_start: Optional[Callable[[int, MacroStep], None]] = None,
        on_finished: Optional[Callable[[], None]] = None,
        stop_event: Optional[threading.Event] = None,
    ):
        """
        Parameters
        ----------
        steps            : List of MacroStep to execute.
        loop_count       : Number of full passes (-1 = infinite).
        speed_multiplier : > 1.0 faster,  < 1.0 slower.
        playback_mode    : 'normal' | 'fast' | 'humanized'
        on_step_start    : callback(index, step) fired before each step.
        on_finished      : callback fired when playback ends normally.
        stop_event       : shared threading.Event; set it to abort.
        """
        self.steps            = [s for s in steps if s.enabled]
        self.loop_count       = loop_count
        self.speed_multiplier = max(0.01, speed_multiplier)
        self.playback_mode    = playback_mode
        self._on_step_start   = on_step_start
        self._on_finished     = on_finished
        self.stop_event       = stop_event or threading.Event()

        self._thread: Optional[threading.Thread] = None

        # Import pynput controllers lazily
        self._mouse_ctrl    = None
        self._keyboard_ctrl = None

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def start(self) -> None:
        """Start playback in a background thread."""
        self.stop_event.clear()
        self._thread = threading.Thread(target=self._run, daemon=True)
        self._thread.start()

    def stop(self) -> None:
        """Request immediate stop."""
        self.stop_event.set()

    def join(self, timeout: Optional[float] = None) -> None:
        """Block until playback thread finishes."""
        if self._thread:
            self._thread.join(timeout)

    @property
    def is_playing(self) -> bool:
        return self._thread is not None and self._thread.is_alive()

    # ------------------------------------------------------------------
    # Internal playback loop
    # ------------------------------------------------------------------

    def _run(self) -> None:
        self._init_controllers()
        loops_done = 0
        try:
            while not self.stop_event.is_set():
                for idx, step in enumerate(self.steps):
                    if self.stop_event.is_set():
                        return
                    if self._on_step_start:
                        try:
                            self._on_step_start(idx, step)
                        except Exception:
                            pass
                    self._execute_step(step)

                loops_done += 1
                if self.loop_count != INFINITE_LOOPS:
                    if loops_done >= self.loop_count:
                        break
        finally:
            if self._on_finished and not self.stop_event.is_set():
                try:
                    self._on_finished()
                except Exception:
                    pass

    # ------------------------------------------------------------------
    # Controller initialisation
    # ------------------------------------------------------------------

    def _init_controllers(self) -> None:
        try:
            from pynput.mouse    import Controller as MouseCtrl
            from pynput.keyboard import Controller as KbCtrl
            self._mouse_ctrl    = MouseCtrl()
            self._keyboard_ctrl = KbCtrl()
        except Exception as exc:
            raise RuntimeError(f"Cannot initialise pynput controllers: {exc}")

    # ------------------------------------------------------------------
    # Step execution dispatcher
    # ------------------------------------------------------------------

    def _execute_step(self, step: MacroStep) -> None:
        t = step.step_type

        if   t == StepType.MOUSE_MOVE:
            self._do_mouse_move(step)
        elif t == StepType.MOUSE_CLICK:
            self._do_mouse_click(step)
        elif t == StepType.MOUSE_SCROLL:
            self._do_mouse_scroll(step)
        elif t == StepType.KEY_PRESS:
            self._do_key_press(step)
        elif t == StepType.KEY_RELEASE:
            self._do_key_release(step)
        elif t == StepType.TEXT:
            self._do_type_text(step)
        elif t == StepType.WAIT:
            self._do_wait(step)
        elif t == StepType.WAIT_FOR_IMAGE:
            self._do_wait_for_image(step)
        elif t == StepType.WAIT_FOR_PIXEL:
            self._do_wait_for_pixel(step)
        elif t == StepType.REGION_CAPTURE:
            self._do_region_capture(step)
        elif t == StepType.ACTIVATE_WINDOW:
            self._do_activate_window(step)

        self._sleep(self._adjusted_delay(step.delay_after))

    # ------------------------------------------------------------------
    # Delay helpers
    # ------------------------------------------------------------------

    def _adjusted_delay(self, delay: float) -> float:
        if delay <= 0:
            return 0.0
        d = delay / self.speed_multiplier
        if self.playback_mode == "fast":
            d = min(d, FAST_MAX_DELAY)
        elif self.playback_mode == "humanized":
            jitter = random.uniform(-HUMAN_JITTER, HUMAN_JITTER)
            d = max(0.0, d * (1.0 + jitter))
        return d

    def _sleep(self, seconds: float) -> None:
        """Interruptible sleep respecting stop_event."""
        if seconds <= 0:
            return
        end = time.perf_counter() + seconds
        while not self.stop_event.is_set():
            remaining = end - time.perf_counter()
            if remaining <= 0:
                break
            time.sleep(min(0.05, remaining))

    # ------------------------------------------------------------------
    # Mouse actions
    # ------------------------------------------------------------------

    def _do_mouse_move(self, step: MacroStep) -> None:
        if step.x is None or step.y is None:
            return
        if self.playback_mode == "humanized":
            self._smooth_move(step.x, step.y)
        else:
            self._mouse_ctrl.position = (step.x, step.y)

    def _smooth_move(self, tx: int, ty: int) -> None:
        """Move mouse smoothly using a sine-eased curve."""
        sx, sy = self._mouse_ctrl.position
        dist   = math.hypot(tx - sx, ty - sy)
        steps  = max(HUMAN_MIN_STEPS, int(dist / 5))
        for i in range(1, steps + 1):
            if self.stop_event.is_set():
                return
            t  = i / steps
            t  = (1 - math.cos(t * math.pi)) / 2  # ease in-out
            nx = int(sx + (tx - sx) * t)
            ny = int(sy + (ty - sy) * t)
            self._mouse_ctrl.position = (nx, ny)
            time.sleep(0.005)

    def _do_mouse_click(self, step: MacroStep) -> None:
        from pynput.mouse import Button
        btn_map = {
            "left":   Button.left,
            "right":  Button.right,
            "middle": Button.middle,
        }
        btn = btn_map.get(step.button or "left", Button.left)
        if step.x is not None and step.y is not None:
            self._mouse_ctrl.position = (step.x, step.y)
        if step.pressed:
            self._mouse_ctrl.press(btn)
        else:
            self._mouse_ctrl.release(btn)

    def _do_mouse_scroll(self, step: MacroStep) -> None:
        if step.x is not None and step.y is not None:
            self._mouse_ctrl.position = (step.x, step.y)
        dy = int(step.value or 0)
        self._mouse_ctrl.scroll(0, dy)

    # ------------------------------------------------------------------
    # Keyboard actions
    # ------------------------------------------------------------------

    def _key_from_str(self, key_str: str):
        from pynput.keyboard import Key, KeyCode
        # Check if it is a named special key
        if hasattr(Key, key_str):
            return getattr(Key, key_str)
        # Single character → KeyCode
        if len(key_str) == 1:
            return KeyCode.from_char(key_str)
        return KeyCode.from_char(key_str)

    def _do_key_press(self, step: MacroStep) -> None:
        if step.key:
            self._keyboard_ctrl.press(self._key_from_str(step.key))

    def _do_key_release(self, step: MacroStep) -> None:
        if step.key:
            self._keyboard_ctrl.release(self._key_from_str(step.key))

    def _do_type_text(self, step: MacroStep) -> None:
        if step.value:
            self._keyboard_ctrl.type(str(step.value))

    # ------------------------------------------------------------------
    # Wait conditions
    # ------------------------------------------------------------------

    def _do_wait(self, step: MacroStep) -> None:
        duration = float(step.value or 0)
        self._sleep(duration)

    def _do_wait_for_image(self, step: MacroStep) -> None:
        timeout  = float(step.extra.get("timeout", 30))
        interval = float(step.extra.get("interval", 0.5))
        deadline = time.perf_counter() + timeout
        while not self.stop_event.is_set():
            pos = find_image_on_screen(
                step.image_path or "",
                confidence=float(step.extra.get("confidence", 0.9)),
                region=step.region,
            )
            if pos:
                # Optionally move to the found location
                if step.extra.get("click_on_find"):
                    self._mouse_ctrl.position = pos
                    from pynput.mouse import Button
                    self._mouse_ctrl.click(Button.left)
                return
            if time.perf_counter() >= deadline:
                break
            self._sleep(interval)

    def _do_wait_for_pixel(self, step: MacroStep) -> None:
        if step.x is None or step.y is None or step.value is None:
            return
        target  = tuple(step.value)  # (R, G, B)
        timeout = float(step.extra.get("timeout", 30))
        deadline = time.perf_counter() + timeout
        while not self.stop_event.is_set():
            color = pixel_color(step.x, step.y)
            if color == target:
                return
            if time.perf_counter() >= deadline:
                break
            self._sleep(0.2)

    # ------------------------------------------------------------------
    # Misc
    # ------------------------------------------------------------------

    def _do_region_capture(self, step: MacroStep) -> None:
        if not step.region:
            return
        from .screen_utils import save_screenshot
        path = step.extra.get("output_path") or "capture.png"
        save_screenshot(path, step.region)

    def _do_activate_window(self, step: MacroStep) -> None:
        if not step.window_title:
            return
        try:
            import pygetwindow as gw
            wins = gw.getWindowsWithTitle(step.window_title)
            if wins:
                wins[0].activate()
        except Exception:
            pass
