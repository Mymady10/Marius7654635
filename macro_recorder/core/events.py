"""
events.py - Data model for macro steps.

Defines the MacroStep dataclass and StepType constants representing
all supported action types in a recorded macro.
"""

from dataclasses import dataclass, field, asdict
from typing import Optional, Any, Dict
import copy

# ---------------------------------------------------------------------------
# Step type constants
# ---------------------------------------------------------------------------
class StepType:
    MOUSE_MOVE      = "mouse_move"
    MOUSE_CLICK     = "mouse_click"
    MOUSE_SCROLL    = "mouse_scroll"
    KEY_PRESS       = "key_press"
    KEY_RELEASE     = "key_release"
    TEXT            = "text"
    WAIT            = "wait"
    WAIT_FOR_IMAGE  = "wait_for_image"
    WAIT_FOR_PIXEL  = "wait_for_pixel"
    REGION_CAPTURE  = "region_capture"
    ACTIVATE_WINDOW = "activate_window"

    ALL = [
        MOUSE_MOVE, MOUSE_CLICK, MOUSE_SCROLL,
        KEY_PRESS, KEY_RELEASE, TEXT,
        WAIT, WAIT_FOR_IMAGE, WAIT_FOR_PIXEL,
        REGION_CAPTURE, ACTIVATE_WINDOW,
    ]


@dataclass
class MacroStep:
    """
    Represents a single step in a macro recording.

    Fields
    ------
    step_type   : One of the StepType constants.
    delay_after : Seconds to wait after executing this step.
    x, y        : Screen coordinates (pixels), if applicable.
    button      : Mouse button name ('left', 'right', 'middle').
    pressed     : True = press, False = release (for mouse/key events).
    key         : Key name or character for keyboard events.
    value       : Generic value (scroll amount, text string, etc.).
    image_path  : Path to a reference image (wait_for_image / anchor).
    region      : Bounding box dict {'x','y','w','h'} for a screen region.
    window_title: Window title string for activate_window steps.
    enabled     : If False, the step is skipped during playback.
    extra       : Arbitrary extra metadata.
    """
    step_type:    str
    delay_after:  float               = 0.0
    x:            Optional[int]       = None
    y:            Optional[int]       = None
    button:       Optional[str]       = None
    pressed:      Optional[bool]      = None
    key:          Optional[str]       = None
    value:        Optional[Any]       = None
    image_path:   Optional[str]       = None
    region:       Optional[Dict]      = None
    window_title: Optional[str]       = None
    enabled:      bool                = True
    extra:        Dict[str, Any]      = field(default_factory=dict)

    # ------------------------------------------------------------------
    # Serialisation helpers
    # ------------------------------------------------------------------
    def to_dict(self) -> dict:
        """Convert to a plain dict suitable for JSON serialisation."""
        d = asdict(self)
        return d

    @classmethod
    def from_dict(cls, data: dict) -> "MacroStep":
        """Reconstruct a MacroStep from a plain dict (e.g. loaded from JSON)."""
        known = {f for f in cls.__dataclass_fields__}
        filtered = {k: v for k, v in data.items() if k in known}
        return cls(**filtered)

    def clone(self) -> "MacroStep":
        """Return a deep copy of this step."""
        return copy.deepcopy(self)

    # ------------------------------------------------------------------
    # Human-readable summary
    # ------------------------------------------------------------------
    def summary(self) -> str:
        """Short one-line description used in the UI step list."""
        t = self.step_type
        if t == StepType.MOUSE_MOVE:
            return f"Move  ({self.x}, {self.y})"
        if t == StepType.MOUSE_CLICK:
            action = "Press" if self.pressed else "Release"
            return f"Click {action} {self.button}  ({self.x}, {self.y})"
        if t == StepType.MOUSE_SCROLL:
            return f"Scroll  ({self.x}, {self.y})  dy={self.value}"
        if t == StepType.KEY_PRESS:
            return f"Key Press  {self.key}"
        if t == StepType.KEY_RELEASE:
            return f"Key Release  {self.key}"
        if t == StepType.TEXT:
            preview = (str(self.value) or "")[:30]
            return f'Text  "{preview}"'
        if t == StepType.WAIT:
            return f"Wait  {self.value}s"
        if t == StepType.WAIT_FOR_IMAGE:
            return f"Wait For Image  {self.image_path}"
        if t == StepType.WAIT_FOR_PIXEL:
            return f"Wait For Pixel  ({self.x},{self.y})  color={self.value}"
        if t == StepType.REGION_CAPTURE:
            return f"Capture Region  {self.region}"
        if t == StepType.ACTIVATE_WINDOW:
            return f"Activate Window  {self.window_title}"
        return t
