"""
storage.py - Saving and loading macros as JSON files.

Macro file format
-----------------
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
    "steps": [ ... ]
}
"""

import json
import os
from datetime import datetime
from typing import List, Optional, Dict, Any

from .events import MacroStep

MACRO_VERSION = "1.0"


class MacroFile:
    """Container for a complete macro (metadata + settings + steps)."""

    def __init__(self, name: str = "Untitled"):
        self.name: str = name
        self.version: str = MACRO_VERSION
        self.created: str = datetime.now().isoformat(timespec="seconds")
        self.description: str = ""
        self.loop_count: int = 1
        self.speed_multiplier: float = 1.0
        self.playback_mode: str = "normal"   # normal | fast | humanized
        self.steps: List[MacroStep] = []

    # ------------------------------------------------------------------
    # Serialisation
    # ------------------------------------------------------------------
    def to_dict(self) -> dict:
        return {
            "meta": {
                "name": self.name,
                "version": self.version,
                "created": self.created,
                "description": self.description,
            },
            "settings": {
                "loop_count": self.loop_count,
                "speed_multiplier": self.speed_multiplier,
                "playback_mode": self.playback_mode,
            },
            "steps": [s.to_dict() for s in self.steps],
        }

    @classmethod
    def from_dict(cls, data: dict) -> "MacroFile":
        m = cls()
        meta = data.get("meta", {})
        m.name = meta.get("name", "Untitled")
        m.version = meta.get("version", MACRO_VERSION)
        m.created = meta.get("created", datetime.now().isoformat())
        m.description = meta.get("description", "")

        settings = data.get("settings", {})
        m.loop_count = settings.get("loop_count", 1)
        m.speed_multiplier = settings.get("speed_multiplier", 1.0)
        m.playback_mode = settings.get("playback_mode", "normal")

        m.steps = [MacroStep.from_dict(s) for s in data.get("steps", [])]
        return m

    # ------------------------------------------------------------------
    # File I/O
    # ------------------------------------------------------------------
    def save(self, path: str) -> None:
        """Serialise macro to *path* (JSON)."""
        os.makedirs(os.path.dirname(os.path.abspath(path)), exist_ok=True)
        with open(path, "w", encoding="utf-8") as fh:
            json.dump(self.to_dict(), fh, indent=2, ensure_ascii=False)

    @classmethod
    def load(cls, path: str) -> "MacroFile":
        """Load a macro from *path* (JSON)."""
        with open(path, "r", encoding="utf-8") as fh:
            data = json.load(fh)
        return cls.from_dict(data)


# Convenience functions -----------------------------------------------------

def save_macro(macro: MacroFile, path: str) -> None:
    macro.save(path)


def load_macro(path: str) -> MacroFile:
    return MacroFile.load(path)
