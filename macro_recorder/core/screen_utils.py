"""
screen_utils.py - Screen utility helpers.

Provides:
- get_screen_size()     : returns (width, height) of the primary monitor.
- take_screenshot()     : full or region screenshot returning a PIL Image.
- find_image_on_screen(): locate a template image on the screen.
- pixel_color()         : return the RGB color of a specific pixel.
"""

from __future__ import annotations
import math
from typing import Optional, Tuple

try:
    import mss
    import mss.tools
    _MSS_AVAILABLE = True
except ImportError:
    _MSS_AVAILABLE = False

try:
    from PIL import Image
    _PIL_AVAILABLE = True
except ImportError:
    _PIL_AVAILABLE = False


# ---------------------------------------------------------------------------
# Screen size
# ---------------------------------------------------------------------------

def get_screen_size() -> Tuple[int, int]:
    """Return (width, height) of the primary monitor in pixels."""
    if _MSS_AVAILABLE:
        try:
            with mss.mss() as sct:
                mon = sct.monitors[1]  # index 0 = all monitors combined
                return mon["width"], mon["height"]
        except Exception:
            pass
    # Fallback: tkinter
    try:
        import tkinter as tk
        root = tk.Tk()
        root.withdraw()
        w, h = root.winfo_screenwidth(), root.winfo_screenheight()
        root.destroy()
        return w, h
    except Exception:
        return 1920, 1080


# ---------------------------------------------------------------------------
# Screenshot helpers
# ---------------------------------------------------------------------------

def take_screenshot(region: Optional[dict] = None):
    """
    Capture the screen (or a sub-region) and return a PIL Image.

    Parameters
    ----------
    region : dict with keys 'x', 'y', 'w', 'h', or None for full screen.
    """
    if not _MSS_AVAILABLE or not _PIL_AVAILABLE:
        raise RuntimeError("mss and Pillow are required for screenshots.")

    with mss.mss() as sct:
        if region:
            mon = {"top": region["y"], "left": region["x"],
                   "width": region["w"], "height": region["h"]}
        else:
            mon = sct.monitors[1]
        raw = sct.grab(mon)
        img = Image.frombytes("RGB", raw.size, raw.bgra, "raw", "BGRX")
    return img


def save_screenshot(path: str, region: Optional[dict] = None) -> None:
    """Capture and save to *path* (PNG)."""
    img = take_screenshot(region)
    img.save(path)


# ---------------------------------------------------------------------------
# Image matching
# ---------------------------------------------------------------------------

def find_image_on_screen(
    template_path: str,
    confidence: float = 0.9,
    region: Optional[dict] = None,
) -> Optional[Tuple[int, int]]:
    """
    Search for *template_path* on the screen and return its centre (x, y).

    Uses PyAutoGUI's locateCenterOnScreen if available, otherwise falls back
    to a basic Pillow-based template search.

    Returns None if the image is not found.
    """
    try:
        import pyautogui
        kwargs: dict = {"confidence": confidence}
        if region:
            kwargs["region"] = (region["x"], region["y"],
                                region["w"], region["h"])
        result = pyautogui.locateCenterOnScreen(template_path, **kwargs)
        if result:
            return int(result.x), int(result.y)
        return None
    except Exception:
        pass

    # Basic fallback: PIL-based normalised cross-correlation
    try:
        screen = take_screenshot(region)
        template = Image.open(template_path).convert("RGB")
        sw, sh = screen.size
        tw, th = template.size
        if tw > sw or th > sh:
            return None
        # Convert to greyscale numpy for speed (if numpy available)
        try:
            import numpy as np
            s = np.array(screen.convert("L"), dtype=float)
            t = np.array(template.convert("L"), dtype=float)
            best = -1.0
            bx, by = 0, 0
            # Scan with stride 2 for speed
            for y in range(0, sh - th + 1, 2):
                for x in range(0, sw - tw + 1, 2):
                    patch = s[y:y+th, x:x+tw]
                    diff = patch - t
                    score = 1.0 - (np.std(diff) / 255.0)
                    if score > best:
                        best = score
                        bx, by = x, y
            if best >= confidence:
                offset_x = region["x"] if region else 0
                offset_y = region["y"] if region else 0
                return bx + tw // 2 + offset_x, by + th // 2 + offset_y
        except ImportError:
            pass
        return None
    except Exception:
        return None


# ---------------------------------------------------------------------------
# Pixel colour
# ---------------------------------------------------------------------------

def pixel_color(x: int, y: int) -> Tuple[int, int, int]:
    """Return the (R, G, B) colour of the pixel at (x, y)."""
    try:
        img = take_screenshot({"x": x, "y": y, "w": 1, "h": 1})
        return img.getpixel((0, 0))[:3]
    except Exception:
        return (0, 0, 0)


# ---------------------------------------------------------------------------
# Distance helper (used by recorder for movement filtering)
# ---------------------------------------------------------------------------

def point_distance(x1: int, y1: int, x2: int, y2: int) -> float:
    return math.sqrt((x2 - x1) ** 2 + (y2 - y1) ** 2)
