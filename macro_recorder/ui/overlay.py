"""
overlay.py - Fullscreen screenshot/region overlay (tkinter-based).

Re-exports RegionSelector for convenience, and provides a helper function
`select_region()` for one-shot use.
"""

from __future__ import annotations
from typing import Optional, Dict
from ..core.region_selector import RegionSelector


def select_region() -> Optional[Dict]:
    """
    Open the fullscreen region-selection overlay.

    Returns a dict {'x','y','w','h','cx','cy'} on success, or None if
    the user pressed Escape.
    """
    sel = RegionSelector()
    return sel.select()
