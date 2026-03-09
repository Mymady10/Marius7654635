"""
Macro Recorder - Înregistrează și repetă gesturile de pe calculator.
(Macro Recorder - Records and replays mouse and keyboard gestures.)

Utilizare / Usage:
    python macro_recorder.py

Comenzi în timp ce rulează / Commands while running:
    F8  - Pornește / oprește înregistrarea  (Start / stop recording)
    F9  - Redă înregistrarea                (Replay recording)
    F10 - Salvează înregistrarea într-un fișier JSON (Save recording to JSON)
    F11 - Încarcă înregistrarea dintr-un fișier JSON (Load recording from JSON)
    F12 - Ieșire                            (Quit)
"""

import json
import time
import threading
from pathlib import Path

try:
    from pynput import mouse, keyboard
    from pynput.keyboard import Key, KeyCode
    from pynput.mouse import Button
except ImportError:
    raise SystemExit(
        "Instalează dependențele cu:\n"
        "  pip install -r requirements.txt\n\n"
        "Install dependencies with:\n"
        "  pip install -r requirements.txt"
    )

DEFAULT_SAVE_FILE = "macro_recording.json"

# ─────────────────────────────────────────────────────────────────────────────
# Event serialization helpers
# ─────────────────────────────────────────────────────────────────────────────

def _serialize_key(key):
    """Convert a pynput Key/KeyCode to a JSON-serializable dict."""
    if isinstance(key, KeyCode):
        return {"type": "keycode", "char": key.char, "vk": key.vk}
    # Key enum (special keys like Shift, Ctrl …)
    return {"type": "key", "name": key.name}


def _deserialize_key(data):
    """Reconstruct a pynput Key/KeyCode from a dict."""
    if data["type"] == "keycode":
        return KeyCode(char=data.get("char"), vk=data.get("vk"))
    return Key[data["name"]]


def _serialize_button(button):
    return button.name


def _deserialize_button(name):
    return Button[name]


# ─────────────────────────────────────────────────────────────────────────────
# MacroRecorder
# ─────────────────────────────────────────────────────────────────────────────

class MacroRecorder:
    """Records mouse and keyboard events, then replays them at the original speed."""

    def __init__(self):
        self._events = []          # list of (timestamp, event_dict)
        self._recording = False
        self._replaying = False
        self._start_time = None
        self._lock = threading.Lock()

        self._mouse_listener = None
        self._keyboard_listener = None

    # ── properties ──────────────────────────────────────────────────────────

    @property
    def is_recording(self):
        return self._recording

    @property
    def is_replaying(self):
        return self._replaying

    @property
    def event_count(self):
        return len(self._events)

    # ── recording ────────────────────────────────────────────────────────────

    def start_recording(self):
        if self._recording or self._replaying:
            return
        with self._lock:
            self._events.clear()
            self._recording = True
            self._start_time = time.time()

        self._mouse_listener = mouse.Listener(
            on_move=self._on_move,
            on_click=self._on_click,
            on_scroll=self._on_scroll,
        )
        self._keyboard_listener = keyboard.Listener(
            on_press=self._on_press,
            on_release=self._on_release,
        )
        self._mouse_listener.start()
        self._keyboard_listener.start()
        print("⏺  Înregistrare pornită / Recording started …")

    def stop_recording(self):
        if not self._recording:
            return
        self._recording = False
        if self._mouse_listener:
            self._mouse_listener.stop()
        if self._keyboard_listener:
            self._keyboard_listener.stop()
        print(f"⏹  Înregistrare oprită / Recording stopped. "
              f"({self.event_count} evenimente / events captured)")

    # ── internal event handlers ──────────────────────────────────────────────

    def _ts(self):
        return time.time() - self._start_time

    def _record(self, event):
        with self._lock:
            self._events.append((self._ts(), event))

    def _on_move(self, x, y):
        self._record({"action": "move", "x": x, "y": y})

    def _on_click(self, x, y, button, pressed):
        self._record({
            "action": "click",
            "x": x, "y": y,
            "button": _serialize_button(button),
            "pressed": pressed,
        })

    def _on_scroll(self, x, y, dx, dy):
        self._record({"action": "scroll", "x": x, "y": y, "dx": dx, "dy": dy})

    def _on_press(self, key):
        self._record({"action": "key_press", "key": _serialize_key(key)})

    def _on_release(self, key):
        self._record({"action": "key_release", "key": _serialize_key(key)})

    # ── replay ───────────────────────────────────────────────────────────────

    def replay(self, speed: float = 1.0):
        """Replay recorded events.

        Args:
            speed: Playback speed multiplier (default 1.0 = real time).
                   Use 2.0 for double speed, 0.5 for half speed.
        """
        if self._replaying or self._recording:
            return
        if not self._events:
            print("⚠  Nu există nimic de redat / Nothing to replay.")
            return

        thread = threading.Thread(target=self._replay_thread, args=(speed,), daemon=True)
        thread.start()

    def _replay_thread(self, speed: float):
        self._replaying = True
        print(f"▶  Redare pornită / Replay started … (viteză/speed ×{speed})")

        mouse_ctrl = mouse.Controller()
        keyboard_ctrl = keyboard.Controller()

        prev_ts = 0.0
        for ts, event in list(self._events):
            delay = (ts - prev_ts) / speed
            if delay > 0:
                time.sleep(delay)
            prev_ts = ts

            try:
                action = event["action"]

                if action == "move":
                    mouse_ctrl.position = (event["x"], event["y"])

                elif action == "click":
                    mouse_ctrl.position = (event["x"], event["y"])
                    button = _deserialize_button(event["button"])
                    if event["pressed"]:
                        mouse_ctrl.press(button)
                    else:
                        mouse_ctrl.release(button)

                elif action == "scroll":
                    mouse_ctrl.position = (event["x"], event["y"])
                    mouse_ctrl.scroll(event["dx"], event["dy"])

                elif action == "key_press":
                    key = _deserialize_key(event["key"])
                    keyboard_ctrl.press(key)

                elif action == "key_release":
                    key = _deserialize_key(event["key"])
                    keyboard_ctrl.release(key)

            except Exception as exc:
                print(f"  ⚠ Eroare la eveniment / Event error: {exc}")

        self._replaying = False
        print("✅  Redare finalizată / Replay finished.")

    # ── persistence ──────────────────────────────────────────────────────────

    def save(self, path: str = DEFAULT_SAVE_FILE):
        data = [{"ts": ts, "event": ev} for ts, ev in self._events]
        Path(path).write_text(json.dumps(data, indent=2, ensure_ascii=False), encoding="utf-8")
        print(f"💾  Salvat în / Saved to: {path}")

    def load(self, path: str = DEFAULT_SAVE_FILE):
        raw = json.loads(Path(path).read_text(encoding="utf-8"))
        with self._lock:
            self._events = [(item["ts"], item["event"]) for item in raw]
        print(f"📂  Încărcat din / Loaded from: {path} ({self.event_count} evenimente/events)")


# ─────────────────────────────────────────────────────────────────────────────
# Hotkey controller
# ─────────────────────────────────────────────────────────────────────────────

class HotkeyController:
    """Listens for global hotkeys to drive the MacroRecorder."""

    HOTKEYS = {
        "F8":  "Pornește/Oprește înregistrarea  (Start/Stop recording)",
        "F9":  "Redă înregistrarea              (Replay recording)",
        "F10": "Salvează înregistrarea           (Save recording to JSON)",
        "F11": "Încarcă înregistrarea            (Load recording from JSON)",
        "F12": "Ieșire                           (Quit)",
    }

    def __init__(self, recorder: MacroRecorder):
        self._recorder = recorder
        self._quit = threading.Event()

    def run(self):
        print("\n╔══════════════════════════════════════════════════╗")
        print("║         MACRO RECORDER  –  Dimitriu Marius       ║")
        print("╚══════════════════════════════════════════════════╝\n")
        print("Taste rapide / Hotkeys:")
        for key, desc in self.HOTKEYS.items():
            print(f"  {key:4s}  →  {desc}")
        print()

        def on_press(key):
            r = self._recorder
            if key == Key.f8:
                if r.is_recording:
                    r.stop_recording()
                else:
                    r.start_recording()
            elif key == Key.f9:
                r.replay(speed=1.0)
            elif key == Key.f10:
                r.save()
            elif key == Key.f11:
                path = DEFAULT_SAVE_FILE
                if Path(path).exists():
                    r.load(path)
                else:
                    print(f"⚠  Fișierul '{path}' nu există. / File '{path}' not found.")
            elif key == Key.f12:
                print("👋  La revedere! / Goodbye!")
                self._quit.set()
                return False   # stop listener

        with keyboard.Listener(on_press=on_press) as listener:
            self._quit.wait()
            listener.stop()


# ─────────────────────────────────────────────────────────────────────────────
# Entry point
# ─────────────────────────────────────────────────────────────────────────────

def main():
    recorder = MacroRecorder()
    controller = HotkeyController(recorder)
    controller.run()


if __name__ == "__main__":
    main()
