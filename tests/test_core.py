"""
Unit tests for the macro recorder application.
Tests core logic: events, storage, player delay calculation,
recorder key helpers, and hotkey manager — all without requiring
a display or real hardware.
"""

import json
import os
import tempfile
import threading
import unittest

# ---------------------------------------------------------------------------
# events.py tests
# ---------------------------------------------------------------------------
from macro_recorder.core.events import MacroStep, StepType


class TestMacroStep(unittest.TestCase):

    def test_defaults(self):
        s = MacroStep(step_type=StepType.MOUSE_MOVE)
        self.assertEqual(s.step_type, "mouse_move")
        self.assertEqual(s.delay_after, 0.0)
        self.assertTrue(s.enabled)
        self.assertEqual(s.extra, {})

    def test_to_dict_from_dict_roundtrip(self):
        s = MacroStep(
            step_type=StepType.MOUSE_CLICK,
            x=50, y=100,
            button="left",
            pressed=True,
            delay_after=0.123,
        )
        d    = s.to_dict()
        s2   = MacroStep.from_dict(d)
        self.assertEqual(s2.step_type, s.step_type)
        self.assertEqual(s2.x, 50)
        self.assertEqual(s2.y, 100)
        self.assertEqual(s2.button, "left")
        self.assertTrue(s2.pressed)
        self.assertAlmostEqual(s2.delay_after, 0.123)

    def test_clone_is_independent(self):
        s  = MacroStep(step_type=StepType.TEXT, value="hello", extra={"k": 1})
        s2 = s.clone()
        s2.value = "world"
        s2.extra["k"] = 99
        self.assertEqual(s.value, "hello")
        self.assertEqual(s.extra["k"], 1)

    def test_summary_all_types(self):
        cases = [
            (MacroStep(step_type=StepType.MOUSE_MOVE, x=1, y=2),       "Move  (1, 2)"),
            (MacroStep(step_type=StepType.MOUSE_CLICK, x=1, y=2,
                       button="right", pressed=True),                    "Click Press right  (1, 2)"),
            (MacroStep(step_type=StepType.MOUSE_SCROLL, x=1, y=2,
                       value=3),                                         "Scroll  (1, 2)  dy=3"),
            (MacroStep(step_type=StepType.KEY_PRESS,  key="ctrl_l"),    "Key Press  ctrl_l"),
            (MacroStep(step_type=StepType.KEY_RELEASE, key="ctrl_l"),   "Key Release  ctrl_l"),
            (MacroStep(step_type=StepType.TEXT, value="hi"),            'Text  "hi"'),
            (MacroStep(step_type=StepType.WAIT, value=2.0),             "Wait  2.0s"),
            (MacroStep(step_type=StepType.WAIT_FOR_IMAGE,
                       image_path="/tmp/x.png"),                         "Wait For Image  /tmp/x.png"),
            (MacroStep(step_type=StepType.ACTIVATE_WINDOW,
                       window_title="Notepad"),                          "Activate Window  Notepad"),
        ]
        for step, expected in cases:
            with self.subTest(step_type=step.step_type):
                self.assertEqual(step.summary(), expected)

    def test_extra_field_preserved(self):
        s = MacroStep(step_type=StepType.WAIT_FOR_IMAGE,
                      extra={"timeout": 30, "confidence": 0.9})
        d  = s.to_dict()
        s2 = MacroStep.from_dict(d)
        self.assertEqual(s2.extra["timeout"], 30)
        self.assertAlmostEqual(s2.extra["confidence"], 0.9)

    def test_from_dict_ignores_unknown_keys(self):
        d = {
            "step_type": "mouse_move",
            "x": 10,
            "y": 20,
            "unknown_field": "should be ignored",
        }
        s = MacroStep.from_dict(d)
        self.assertEqual(s.x, 10)

    def test_step_type_constants(self):
        for attr in ("MOUSE_MOVE", "MOUSE_CLICK", "MOUSE_SCROLL",
                     "KEY_PRESS", "KEY_RELEASE", "TEXT", "WAIT",
                     "WAIT_FOR_IMAGE", "WAIT_FOR_PIXEL",
                     "REGION_CAPTURE", "ACTIVATE_WINDOW"):
            self.assertIn(getattr(StepType, attr), StepType.ALL)


# ---------------------------------------------------------------------------
# storage.py tests
# ---------------------------------------------------------------------------
from macro_recorder.core.storage import MacroFile, save_macro, load_macro


class TestMacroFile(unittest.TestCase):

    def _sample_macro(self) -> MacroFile:
        m = MacroFile(name="SampleMacro")
        m.loop_count       = 3
        m.speed_multiplier = 2.0
        m.playback_mode    = "fast"
        m.steps = [
            MacroStep(step_type=StepType.MOUSE_MOVE, x=10, y=20,  delay_after=0.1),
            MacroStep(step_type=StepType.TEXT,      value="abc", delay_after=0.2),
        ]
        return m

    def test_to_dict_structure(self):
        m = self._sample_macro()
        d = m.to_dict()
        self.assertIn("meta", d)
        self.assertIn("settings", d)
        self.assertIn("steps", d)
        self.assertEqual(d["meta"]["name"], "SampleMacro")
        self.assertEqual(d["settings"]["loop_count"], 3)
        self.assertEqual(len(d["steps"]), 2)

    def test_from_dict_roundtrip(self):
        m  = self._sample_macro()
        m2 = MacroFile.from_dict(m.to_dict())
        self.assertEqual(m2.name, m.name)
        self.assertEqual(m2.loop_count, 3)
        self.assertAlmostEqual(m2.speed_multiplier, 2.0)
        self.assertEqual(m2.playback_mode, "fast")
        self.assertEqual(len(m2.steps), 2)
        self.assertEqual(m2.steps[1].value, "abc")

    def test_save_and_load(self):
        m = self._sample_macro()
        with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as f:
            path = f.name
        try:
            m.save(path)
            self.assertTrue(os.path.isfile(path))
            with open(path) as fh:
                raw = json.load(fh)
            self.assertEqual(raw["meta"]["name"], "SampleMacro")

            m2 = MacroFile.load(path)
            self.assertEqual(m2.name, "SampleMacro")
            self.assertEqual(len(m2.steps), 2)
        finally:
            os.unlink(path)

    def test_convenience_functions(self):
        m = MacroFile(name="Quick")
        m.steps = [MacroStep(step_type=StepType.WAIT, value=1.0)]
        with tempfile.NamedTemporaryFile(suffix=".json", delete=False) as f:
            path = f.name
        try:
            save_macro(m, path)
            m2 = load_macro(path)
            self.assertEqual(m2.name, "Quick")
            self.assertEqual(m2.steps[0].value, 1.0)
        finally:
            os.unlink(path)

    def test_empty_macro_defaults(self):
        m = MacroFile()
        self.assertEqual(m.loop_count, 1)
        self.assertAlmostEqual(m.speed_multiplier, 1.0)
        self.assertEqual(m.playback_mode, "normal")
        self.assertEqual(m.steps, [])

    def test_load_missing_file_raises(self):
        with self.assertRaises(FileNotFoundError):
            MacroFile.load("/nonexistent/path/macro.json")


# ---------------------------------------------------------------------------
# player.py delay tests (no display required)
# ---------------------------------------------------------------------------
from macro_recorder.core.player import Player, INFINITE_LOOPS, FAST_MAX_DELAY


class TestPlayerDelays(unittest.TestCase):

    def _player(self, **kwargs) -> Player:
        return Player(steps=[], **kwargs)

    def test_normal_delay_exact(self):
        p = self._player(speed_multiplier=1.0, playback_mode="normal")
        self.assertAlmostEqual(p._adjusted_delay(0.5), 0.5)

    def test_speed_multiplier_halves_delay(self):
        p = self._player(speed_multiplier=2.0, playback_mode="normal")
        self.assertAlmostEqual(p._adjusted_delay(1.0), 0.5)

    def test_speed_multiplier_doubles_delay(self):
        p = self._player(speed_multiplier=0.5, playback_mode="normal")
        self.assertAlmostEqual(p._adjusted_delay(1.0), 2.0)

    def test_fast_mode_caps_delay(self):
        p = self._player(speed_multiplier=1.0, playback_mode="fast")
        self.assertAlmostEqual(p._adjusted_delay(10.0), FAST_MAX_DELAY)

    def test_fast_mode_short_delay_unchanged(self):
        p = self._player(speed_multiplier=1.0, playback_mode="fast")
        self.assertAlmostEqual(p._adjusted_delay(0.01), 0.01)

    def test_humanized_mode_stays_non_negative(self):
        p = self._player(speed_multiplier=1.0, playback_mode="humanized")
        for _ in range(100):
            self.assertGreaterEqual(p._adjusted_delay(0.001), 0.0)

    def test_zero_delay_returns_zero(self):
        p = self._player()
        self.assertEqual(p._adjusted_delay(0.0), 0.0)

    def test_negative_delay_returns_zero(self):
        p = self._player()
        self.assertEqual(p._adjusted_delay(-1.0), 0.0)

    def test_infinite_loops_sentinel(self):
        self.assertEqual(INFINITE_LOOPS, -1)

    def test_disabled_steps_filtered(self):
        steps = [
            MacroStep(step_type=StepType.WAIT, value=1.0, enabled=True),
            MacroStep(step_type=StepType.WAIT, value=1.0, enabled=False),
            MacroStep(step_type=StepType.WAIT, value=1.0, enabled=True),
        ]
        p = Player(steps=steps)
        self.assertEqual(len(p.steps), 2)


# ---------------------------------------------------------------------------
# recorder.py helper tests
# ---------------------------------------------------------------------------
from macro_recorder.core.recorder import Recorder


class TestRecorderHelpers(unittest.TestCase):

    def setUp(self):
        self._rec = Recorder()

    def test_key_to_str_char(self):
        class FakeKeyCode:
            char = "a"
        self.assertEqual(Recorder._key_to_str(FakeKeyCode()), "a")

    def test_key_to_str_special(self):
        class FakeKey:
            char = None
            name = "ctrl_l"
        self.assertEqual(Recorder._key_to_str(FakeKey()), "ctrl_l")

    def test_key_to_str_fallback(self):
        class WeirdKey:
            pass
        result = Recorder._key_to_str(WeirdKey())
        self.assertIsInstance(result, str)

    def test_initial_state(self):
        self.assertFalse(self._rec.is_recording)
        self.assertEqual(self._rec.steps, [])

    def test_flush_pending_chars_produces_text_step(self):
        self._rec._running      = True
        self._rec._last_event_time = __import__("time").perf_counter()
        self._rec._pending_chars = list("hello")
        self._rec._flush_pending_chars()
        self.assertEqual(len(self._rec.steps), 1)
        s = self._rec.steps[0]
        self.assertEqual(s.step_type, StepType.TEXT)
        self.assertEqual(s.value, "hello")

    def test_flush_empty_chars_no_step(self):
        self._rec._pending_chars = []
        self._rec._flush_pending_chars()
        self.assertEqual(len(self._rec.steps), 0)


# ---------------------------------------------------------------------------
# hotkeys.py tests
# ---------------------------------------------------------------------------
from macro_recorder.core.hotkeys import HotkeyManager


class TestHotkeyManager(unittest.TestCase):

    def test_default_bindings(self):
        hm = HotkeyManager()
        self.assertEqual(hm._bindings["f8"], "record_toggle")
        self.assertEqual(hm._bindings["f9"], "play")
        self.assertEqual(hm._bindings["f10"], "stop")

    def test_set_binding(self):
        hm = HotkeyManager()
        hm.set_binding("F11", "stop")
        self.assertEqual(hm._bindings["f11"], "stop")

    def test_set_callback(self):
        hm = HotkeyManager()
        fn = lambda: None
        hm.set_callback("stop", fn)
        self.assertIs(hm._callbacks["stop"], fn)

    def test_key_name_with_name_attr(self):
        class K:
            name = "F8"
        self.assertEqual(HotkeyManager._key_name(K()), "f8")

    def test_key_name_with_char_attr(self):
        class K:
            name = None
            char = "A"
        self.assertEqual(HotkeyManager._key_name(K()), "a")

    def test_key_name_fallback(self):
        class K:
            pass
        result = HotkeyManager._key_name(K())
        self.assertIsInstance(result, str)


# ---------------------------------------------------------------------------
# screen_utils.py tests (headless-safe)
# ---------------------------------------------------------------------------
from macro_recorder.core.screen_utils import get_screen_size, point_distance


class TestScreenUtils(unittest.TestCase):

    def test_get_screen_size_returns_tuple(self):
        size = get_screen_size()
        self.assertIsInstance(size, tuple)
        self.assertEqual(len(size), 2)
        self.assertGreater(size[0], 0)
        self.assertGreater(size[1], 0)

    def test_point_distance_zero(self):
        self.assertAlmostEqual(point_distance(5, 5, 5, 5), 0.0)

    def test_point_distance_3_4_5(self):
        self.assertAlmostEqual(point_distance(0, 0, 3, 4), 5.0)

    def test_point_distance_negative_coords(self):
        self.assertAlmostEqual(point_distance(-1, -1, 2, 3), 5.0)


if __name__ == "__main__":
    unittest.main()
