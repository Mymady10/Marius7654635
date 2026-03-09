"""
region_selector.py - Full-screen overlay for precise screen-region selection.

Opens a transparent full-screen Tkinter window on top of everything else.
The user can:
  - Click and drag to select a rectangular region.
  - Press Enter or release the mouse to confirm.
  - Press Escape to cancel.

During selection a crosshair cursor, live coordinates, selection dimensions,
and a zoom/magnifier preview are shown.

Usage
-----
    sel = RegionSelector()
    result = sel.select()   # blocks until confirmed or cancelled
    # result is None (cancelled) or a dict:
    # {'x', 'y', 'w', 'h', 'cx', 'cy'}  (region + centre point)
"""

from __future__ import annotations
import tkinter as tk
from typing import Optional, Dict


class RegionSelector:
    """
    Full-screen semi-transparent overlay for pixel-precise region selection.
    Blocks the calling thread until the user confirms or cancels.
    """

    OVERLAY_ALPHA  = 0.35       # transparency of the dark overlay (0–1)
    OVERLAY_COLOR  = "#000000"
    CROSSHAIR_COLOR = "#FF4444"
    RECT_COLOR     = "#00FF00"
    TEXT_COLOR     = "#FFFFFF"
    ZOOM_SIZE      = 120        # pixels: width/height of the magnifier square
    ZOOM_FACTOR    = 4          # zoom level

    def __init__(self):
        self._result: Optional[Dict] = None
        self._cancelled = False

        # Selection state
        self._start_x = self._start_y = 0
        self._cur_x   = self._cur_y   = 0
        self._dragging = False

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def select(self) -> Optional[Dict]:
        """
        Open the overlay and block until the user selects a region.

        Returns
        -------
        dict with keys 'x', 'y', 'w', 'h', 'cx', 'cy'  on success.
        None if cancelled.
        """
        self._result    = None
        self._cancelled = False

        root = tk.Tk()
        self._root = root
        root.title("Select Region")
        root.attributes("-fullscreen", True)
        root.attributes("-alpha", self.OVERLAY_ALPHA)
        root.attributes("-topmost", True)
        root.configure(bg=self.OVERLAY_COLOR)
        root.config(cursor="crosshair")

        # Try to make it truly transparent (Windows)
        try:
            root.wm_attributes("-transparentcolor", "")
        except Exception:
            pass

        sw = root.winfo_screenwidth()
        sh = root.winfo_screenheight()

        canvas = tk.Canvas(root, width=sw, height=sh,
                           bg=self.OVERLAY_COLOR, cursor="crosshair",
                           highlightthickness=0)
        canvas.pack(fill="both", expand=True)
        self._canvas = canvas
        self._sw, self._sh = sw, sh

        # Grab a screenshot for the magnifier
        self._bg_image = None
        try:
            from .screen_utils import take_screenshot
            pil_img = take_screenshot()
            from PIL import ImageTk
            self._bg_pil = pil_img
            self._bg_image = ImageTk.PhotoImage(pil_img)
        except Exception:
            self._bg_pil = None

        # Event bindings
        canvas.bind("<ButtonPress-1>",   self._on_press)
        canvas.bind("<B1-Motion>",        self._on_drag)
        canvas.bind("<ButtonRelease-1>",  self._on_release)
        canvas.bind("<Motion>",           self._on_motion)
        root.bind("<Escape>",             self._on_escape)
        root.bind("<Return>",             self._on_enter)

        # Initial draw
        self._draw(0, 0)

        root.mainloop()

        return None if self._cancelled else self._result

    # ------------------------------------------------------------------
    # Event handlers
    # ------------------------------------------------------------------

    def _on_press(self, event):
        self._start_x  = event.x
        self._start_y  = event.y
        self._cur_x    = event.x
        self._cur_y    = event.y
        self._dragging = True
        self._draw(event.x, event.y)

    def _on_drag(self, event):
        self._cur_x = event.x
        self._cur_y = event.y
        self._draw(event.x, event.y)

    def _on_release(self, event):
        self._cur_x    = event.x
        self._cur_y    = event.y
        self._dragging = False
        self._confirm()

    def _on_motion(self, event):
        if not self._dragging:
            self._draw(event.x, event.y)

    def _on_escape(self, event=None):
        self._cancelled = True
        self._root.destroy()

    def _on_enter(self, event=None):
        self._confirm()

    def _confirm(self):
        x1 = min(self._start_x, self._cur_x)
        y1 = min(self._start_y, self._cur_y)
        x2 = max(self._start_x, self._cur_x)
        y2 = max(self._start_y, self._cur_y)
        w  = max(1, x2 - x1)
        h  = max(1, y2 - y1)
        self._result = {
            "x":  x1,
            "y":  y1,
            "w":  w,
            "h":  h,
            "cx": x1 + w // 2,
            "cy": y1 + h // 2,
        }
        try:
            self._root.destroy()
        except Exception:
            pass

    # ------------------------------------------------------------------
    # Drawing
    # ------------------------------------------------------------------

    def _draw(self, mx: int, my: int):
        c  = self._canvas
        sw = self._sw
        sh = self._sh
        c.delete("all")

        # Semi-transparent dark overlay using a canvas rectangle
        c.create_rectangle(0, 0, sw, sh,
                            fill=self.OVERLAY_COLOR, stipple="gray50",
                            outline="")

        # Selection rectangle
        if self._dragging:
            x1 = min(self._start_x, self._cur_x)
            y1 = min(self._start_y, self._cur_y)
            x2 = max(self._start_x, self._cur_x)
            y2 = max(self._start_y, self._cur_y)
            # Clear the selected area
            c.create_rectangle(x1, y1, x2, y2,
                                fill="", outline=self.RECT_COLOR, width=2)
            # Dimensions label
            w_sel = x2 - x1
            h_sel = y2 - y1
            c.create_text(x1 + 4, y1 - 14,
                          text=f"{w_sel}×{h_sel}",
                          fill=self.TEXT_COLOR, anchor="w",
                          font=("Consolas", 11, "bold"))

        # Crosshair
        c.create_line(mx, 0, mx, sh, fill=self.CROSSHAIR_COLOR, width=1)
        c.create_line(0, my, sw, my, fill=self.CROSSHAIR_COLOR, width=1)

        # Coordinate label
        c.create_text(mx + 8, my + 8,
                      text=f"({mx}, {my})",
                      fill=self.TEXT_COLOR, anchor="nw",
                      font=("Consolas", 10))

        # Magnifier / zoom preview
        self._draw_zoom(c, mx, my)

        # Instructions
        c.create_text(sw // 2, 20,
                      text="Drag to select region  •  Enter/Release to confirm  •  Esc to cancel",
                      fill=self.TEXT_COLOR,
                      font=("Segoe UI", 12))

    def _draw_zoom(self, canvas, mx: int, my: int):
        """Draw a magnified preview around the cursor."""
        if self._bg_pil is None:
            return
        zs = self.ZOOM_SIZE
        zf = self.ZOOM_FACTOR
        half = zs // (2 * zf)

        # Source crop from background screenshot
        src_x = max(0, mx - half)
        src_y = max(0, my - half)
        src_w = min(self._sw, src_x + zs // zf) - src_x
        src_h = min(self._sh, src_y + zs // zf) - src_y
        if src_w <= 0 or src_h <= 0:
            return

        try:
            from PIL import Image, ImageTk, ImageDraw
            crop  = self._bg_pil.crop((src_x, src_y, src_x + src_w, src_y + src_h))
            zoomed = crop.resize((src_w * zf, src_h * zf), Image.NEAREST)

            # Pad to full zoom box size
            padded = Image.new("RGB", (zs, zs), (30, 30, 30))
            padded.paste(zoomed, (0, 0))

            # Cross-hair overlay on zoomed image
            draw = ImageDraw.Draw(padded)
            cx_z = (mx - src_x) * zf
            cy_z = (my - src_y) * zf
            draw.line([(cx_z, 0), (cx_z, zs)], fill=(255, 68, 68), width=1)
            draw.line([(0, cy_z), (zs, cy_z)], fill=(255, 68, 68), width=1)

            photo = ImageTk.PhotoImage(padded)

            # Position: top-right of cursor
            px = min(mx + 16, self._sw - zs - 4)
            py = max(4, my - zs - 4)

            # Keep reference to prevent GC
            self._zoom_photo = photo
            canvas.create_image(px, py, anchor="nw", image=photo)
            canvas.create_rectangle(px - 1, py - 1, px + zs, py + zs,
                                     outline=self.RECT_COLOR, width=1)
        except Exception:
            pass
