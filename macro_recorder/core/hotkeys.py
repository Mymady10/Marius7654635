"""
hotkeys.py - Global hotkey management using pynput.

Registers hotkeys that work system-wide, even when the application window
is not focused.  The default bindings are:

  F8  → start / stop recording  (toggle)
  F9  → start playback
  F10 → emergency stop

All bindings are configurable via the HotkeyManager constructor.
"""

from __future__ import annotations
import threading
from typing import Callable, Dict, Optional


class HotkeyManager:
    """
    Listens to global keyboard events and fires callbacks for configured
    hotkeys.

    Parameters
    ----------
    bindings : dict mapping key name strings → zero-argument callables.
               Key names match pynput Key attribute names (e.g. 'f8').
    """

    DEFAULT_BINDINGS: Dict[str, str] = {
        "f8":  "record_toggle",
        "f9":  "play",
        "f10": "stop",
    }

    def __init__(self, callbacks: Dict[str, Callable] = None):
        """
        callbacks : dict mapping action name → callable, e.g.
                    {"record_toggle": func, "play": func, "stop": func}
        """
        self._callbacks: Dict[str, Callable] = callbacks or {}
        self._bindings:  Dict[str, str]      = dict(self.DEFAULT_BINDINGS)
        self._listener = None
        self._running  = False
        self._lock     = threading.Lock()

    # ------------------------------------------------------------------
    # Configuration
    # ------------------------------------------------------------------

    def set_binding(self, key_name: str, action: str) -> None:
        """Map *key_name* (e.g. 'f8') to *action* (e.g. 'record_toggle')."""
        self._bindings[key_name.lower()] = action

    def set_callback(self, action: str, func: Callable) -> None:
        """Register *func* as the handler for *action*."""
        self._callbacks[action] = func

    # ------------------------------------------------------------------
    # Start / stop
    # ------------------------------------------------------------------

    def start(self) -> None:
        """Start listening for global hotkeys."""
        if self._running:
            return
        try:
            from pynput import keyboard as _kb

            def on_press(key):
                name = self._key_name(key)
                action = self._bindings.get(name)
                if action and action in self._callbacks:
                    try:
                        self._callbacks[action]()
                    except Exception:
                        pass

            self._listener = _kb.Listener(on_press=on_press)
            self._listener.start()
            self._running = True
        except Exception as exc:
            raise RuntimeError(f"Could not start hotkey listener: {exc}") from exc

    def stop(self) -> None:
        """Stop listening for global hotkeys."""
        self._running = False
        if self._listener is not None:
            try:
                self._listener.stop()
            except Exception:
                pass
            self._listener = None

    # ------------------------------------------------------------------
    # Internal helpers
    # ------------------------------------------------------------------

    @staticmethod
    def _key_name(key) -> str:
        """Normalise a pynput key object to a lower-case string."""
        try:
            return key.name.lower()
        except AttributeError:
            pass
        try:
            return key.char.lower() if key.char else ""
        except AttributeError:
            return str(key).lower()
