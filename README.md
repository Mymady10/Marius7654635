# Macro Recorder

An advanced desktop macro recorder and player for Windows, written in Python.

## Features

- **Record** mouse movements, clicks, scrolling, and keyboard actions
- **Filter** redundant micro-movements to keep macro files lean
- **Compress** character sequences into a single `text` step
- **Play back** macros with configurable loop count (including infinite loop)
- **Speed multiplier** (faster or slower playback)
- **Playback modes**: Normal, Fast (capped delays), Humanized (jitter + smooth mouse)
- **Global hotkeys** (F8 = record toggle, F9 = play, F10 = stop)
- **Full-screen region selector** with crosshair, coordinates, dimensions, and zoom magnifier
- **Visual step editor**: reorder, edit, enable/disable, duplicate, delete steps
- **JSON persistence**: save and reload macros with metadata and settings
- **Image anchors**: `wait_for_image` steps locate UI elements visually
- **Pixel color checks**: `wait_for_pixel` steps wait for a specific pixel color
- **Emergency stop** via hotkey or button (thread-safe `stop_event`)

## Project Structure

```
macro_recorder/
├── core/
│   ├── events.py           # MacroStep dataclass & StepType constants
│   ├── recorder.py         # Mouse/keyboard recording (pynput)
│   ├── player.py           # Macro playback engine
│   ├── storage.py          # JSON save/load (MacroFile)
│   ├── region_selector.py  # Full-screen region selection overlay
│   ├── hotkeys.py          # Global hotkey manager
│   └── screen_utils.py     # Screen size, screenshot, image matching
└── ui/
    ├── main_window.py      # Main application window
    ├── macro_editor.py     # Step editor (Treeview + edit dialog)
    └── overlay.py          # Region selection overlay (re-export)
app.py                      # Entry point
requirements.txt
```

## Installation

```bash
pip install -r requirements.txt
```

> **Note:** On Linux, `pynput` may require `python3-xlib` or `python3-evdev`.  
> On macOS, Accessibility permissions are required for global input monitoring.

## Usage

```bash
python app.py
```

### Keyboard shortcuts

| Key | Action |
|-----|--------|
| F8 | Start / Stop Recording (toggle) |
| F9 | Play macro |
| F10 | Emergency Stop |
| Ctrl+S | Save macro |
| Ctrl+O | Open macro |
| Ctrl+N | New macro |

## Macro File Format (JSON)

```json
{
  "meta": {
    "name": "My Macro",
    "version": "1.0",
    "created": "2024-01-01T12:00:00",
    "description": ""
  },
  "settings": {
    "loop_count": 1,
    "speed_multiplier": 1.0,
    "playback_mode": "normal"
  },
  "steps": [
    {
      "step_type": "mouse_move",
      "delay_after": 0.05,
      "x": 400,
      "y": 300,
      ...
    }
  ]
}
```

## Step Types

| Type | Description |
|------|-------------|
| `mouse_move` | Move cursor to (x, y) |
| `mouse_click` | Press or release a mouse button |
| `mouse_scroll` | Scroll at (x, y) |
| `key_press` | Press a keyboard key |
| `key_release` | Release a keyboard key |
| `text` | Type a string |
| `wait` | Pause for N seconds |
| `wait_for_image` | Wait until an image appears on screen |
| `wait_for_pixel` | Wait until a pixel reaches a target color |
| `region_capture` | Take a screenshot of a region |
| `activate_window` | Bring a window to the foreground |

## Dependencies

| Package | Purpose |
|---------|---------|
| `pynput` | Global mouse/keyboard recording and simulation |
| `PyAutoGUI` | Screen automation helpers (image matching) |
| `mss` | Fast region-based screenshots |
| `Pillow` | Image processing and magnifier preview |
| `tkinter` | GUI (standard library) |

## Extending

The project is intentionally modular. To add a new step type:
1. Add the constant to `core/events.py` (`StepType`).
2. Add an execution method in `core/player.py` (`_execute_step`).
3. Update the editor column display in `ui/macro_editor.py` if needed.

## License

MIT
