"""
main_window.py - Main application window for the Macro Recorder.

Layout
------
  ┌─ Control bar (name, loops, speed, mode, status) ──────────────────┐
  ├─ Action buttons (Record, Stop Rec, Play, Stop, Region, Save, Load) ┤
  ├─ Step editor (MacroEditor Treeview) ──────────────────────────────┤
  └─ Status bar (mouse coords, region info, hotkeys) ─────────────────┘

Hotkeys (global):
  F8  → toggle recording
  F9  → play
  F10 → emergency stop
"""

from __future__ import annotations

import os
import threading
import time
import tkinter as tk
from tkinter import ttk, filedialog, messagebox
from typing import Optional

from ..core.events    import MacroStep, StepType
from ..core.recorder  import Recorder
from ..core.player    import Player, INFINITE_LOOPS
from ..core.storage   import MacroFile
from ..core.hotkeys   import HotkeyManager
from .macro_editor    import MacroEditor


# Application state labels
STATE_IDLE      = "Idle"
STATE_RECORDING = "Recording"
STATE_PLAYING   = "Playing"
STATE_STOPPED   = "Stopped"

# Colours for state indicator
STATE_COLORS = {
    STATE_IDLE:      ("#E8E8E8", "#333333"),
    STATE_RECORDING: ("#FF4444", "#FFFFFF"),
    STATE_PLAYING:   ("#44AA44", "#FFFFFF"),
    STATE_STOPPED:   ("#FFAA00", "#FFFFFF"),
}


class MainWindow:
    """Root application window."""

    def __init__(self):
        self._macro    = MacroFile()
        self._recorder = Recorder(on_step=self._on_recorded_step)
        self._player:  Optional[Player] = None
        self._stop_event = threading.Event()
        self._current_file: Optional[str] = None

        # Track selected region for info panel
        self._selected_region: Optional[dict] = None

        self._build_root()
        self._build_menu()
        self._build_control_bar()
        self._build_action_buttons()
        self._build_editor()
        self._build_status_bar()

        self._start_hotkeys()
        self._poll_mouse_pos()
        self._set_state(STATE_IDLE)

    # ------------------------------------------------------------------
    # Window construction
    # ------------------------------------------------------------------

    def _build_root(self) -> None:
        self._root = tk.Tk()
        self._root.title("Macro Recorder")
        self._root.geometry("1000x680")
        self._root.minsize(800, 550)
        self._root.protocol("WM_DELETE_WINDOW", self._on_close)

        # App icon (best effort)
        try:
            self._root.iconbitmap(default="")
        except Exception:
            pass

        style = ttk.Style()
        try:
            style.theme_use("clam")
        except Exception:
            pass

        self._root.columnconfigure(0, weight=1)
        self._root.rowconfigure(2, weight=1)

    def _build_menu(self) -> None:
        menubar = tk.Menu(self._root)

        file_menu = tk.Menu(menubar, tearoff=0)
        file_menu.add_command(label="New",  command=self._new_macro, accelerator="Ctrl+N")
        file_menu.add_command(label="Open…", command=self._load_macro, accelerator="Ctrl+O")
        file_menu.add_command(label="Save",  command=self._save_macro, accelerator="Ctrl+S")
        file_menu.add_command(label="Save As…", command=self._save_macro_as)
        file_menu.add_separator()
        file_menu.add_command(label="Exit", command=self._on_close)
        menubar.add_cascade(label="File", menu=file_menu)

        edit_menu = tk.Menu(menubar, tearoff=0)
        edit_menu.add_command(label="Clear All Steps", command=self._clear_steps)
        menubar.add_cascade(label="Edit", menu=edit_menu)

        help_menu = tk.Menu(menubar, tearoff=0)
        help_menu.add_command(label="Hotkeys", command=self._show_hotkeys)
        help_menu.add_command(label="About",   command=self._show_about)
        menubar.add_cascade(label="Help", menu=help_menu)

        self._root.config(menu=menubar)

        # Keyboard shortcuts
        self._root.bind("<Control-n>", lambda e: self._new_macro())
        self._root.bind("<Control-o>", lambda e: self._load_macro())
        self._root.bind("<Control-s>", lambda e: self._save_macro())

    def _build_control_bar(self) -> None:
        bar = ttk.LabelFrame(self._root, text="Macro Settings")
        bar.grid(row=0, column=0, sticky="ew", padx=6, pady=4)
        bar.columnconfigure(1, weight=1)

        # Name
        ttk.Label(bar, text="Name:").grid(row=0, column=0, padx=4, pady=3, sticky="e")
        self._name_var = tk.StringVar(value=self._macro.name)
        name_entry = ttk.Entry(bar, textvariable=self._name_var, width=24)
        name_entry.grid(row=0, column=1, padx=4, pady=3, sticky="ew")
        self._name_var.trace_add("write", lambda *a: setattr(self._macro, "name", self._name_var.get()))

        # Loop count
        ttk.Label(bar, text="Loops:").grid(row=0, column=2, padx=4, sticky="e")
        self._loop_var = tk.StringVar(value="1")
        loop_spin = ttk.Spinbox(bar, textvariable=self._loop_var,
                                from_=0, to=9999, width=6)
        loop_spin.grid(row=0, column=3, padx=4, sticky="w")
        ttk.Label(bar, text="(0 = ∞)").grid(row=0, column=4, padx=(0, 8))

        # Speed
        ttk.Label(bar, text="Speed:").grid(row=0, column=5, padx=4, sticky="e")
        self._speed_var = tk.DoubleVar(value=1.0)
        speed_spin = ttk.Spinbox(bar, textvariable=self._speed_var,
                                 from_=0.1, to=10.0, increment=0.1,
                                 format="%.1f", width=6)
        speed_spin.grid(row=0, column=6, padx=4, sticky="w")
        ttk.Label(bar, text="×").grid(row=0, column=7, padx=(0, 8))

        # Playback mode
        ttk.Label(bar, text="Mode:").grid(row=0, column=8, padx=4, sticky="e")
        self._mode_var = tk.StringVar(value="normal")
        mode_combo = ttk.Combobox(bar, textvariable=self._mode_var,
                                  values=["normal", "fast", "humanized"],
                                  state="readonly", width=10)
        mode_combo.grid(row=0, column=9, padx=4, sticky="w")

        # State indicator
        self._state_var = tk.StringVar(value=STATE_IDLE)
        self._state_lbl = tk.Label(bar, textvariable=self._state_var,
                                   width=12, font=("Segoe UI", 10, "bold"),
                                   relief="solid", bd=1, padx=4)
        self._state_lbl.grid(row=0, column=10, padx=8, sticky="e")

    def _build_action_buttons(self) -> None:
        btn_bar = ttk.Frame(self._root)
        btn_bar.grid(row=1, column=0, sticky="ew", padx=6, pady=2)

        buttons = [
            ("⏺  Start Recording", "red",      self._start_recording),
            ("⏹  Stop Recording",  "darkred",   self._stop_recording),
            ("▶  Play",           "green",     self._play),
            ("■  Stop",           "orange",    self._emergency_stop),
            ("⊞  Select Region",  "steelblue", self._select_region),
            ("💾  Save",           "#444444",   self._save_macro),
            ("📂  Load",           "#444444",   self._load_macro),
        ]

        for label, fg, cmd in buttons:
            btn = tk.Button(btn_bar, text=label, command=cmd,
                            bg="#EEEEEE", activebackground="#DDDDDD",
                            fg=fg, font=("Segoe UI", 9, "bold"),
                            relief="raised", padx=6, pady=4)
            btn.pack(side="left", padx=3, pady=2)

    def _build_editor(self) -> None:
        self._editor = MacroEditor(self._root, on_change=self._sync_steps_from_editor)
        self._editor.grid(row=2, column=0, sticky="nsew", padx=6, pady=2)

    def _build_status_bar(self) -> None:
        status_frame = ttk.Frame(self._root, relief="sunken", padding=2)
        status_frame.grid(row=3, column=0, sticky="ew")
        status_frame.columnconfigure(1, weight=1)

        # Mouse coordinates
        self._mouse_pos_var = tk.StringVar(value="Mouse: (-, -)")
        ttk.Label(status_frame, textvariable=self._mouse_pos_var,
                  width=20).grid(row=0, column=0, padx=4)

        # Selected region
        self._region_var = tk.StringVar(value="Region: none")
        ttk.Label(status_frame, textvariable=self._region_var).grid(
            row=0, column=1, padx=4, sticky="w")

        # Step count
        self._step_count_var = tk.StringVar(value="Steps: 0")
        ttk.Label(status_frame, textvariable=self._step_count_var).grid(
            row=0, column=2, padx=4)

        # Hotkey reminder
        ttk.Label(status_frame,
                  text="F8=Rec  F9=Play  F10=Stop",
                  foreground="#666666").grid(row=0, column=3, padx=8)

    # ------------------------------------------------------------------
    # State management
    # ------------------------------------------------------------------

    def _set_state(self, state: str) -> None:
        self._state_var.set(state)
        bg, fg = STATE_COLORS.get(state, ("#E8E8E8", "#333333"))
        self._state_lbl.config(bg=bg, fg=fg)

    # ------------------------------------------------------------------
    # Recording
    # ------------------------------------------------------------------

    def _start_recording(self) -> None:
        if self._recorder.is_recording:
            return
        self._macro.steps = []
        self._editor.clear()
        self._recorder.start()
        self._set_state(STATE_RECORDING)

    def _stop_recording(self) -> None:
        if not self._recorder.is_recording:
            return
        self._recorder.stop()
        self._macro.steps = self._recorder.steps[:]
        self._editor.set_steps(self._macro.steps)
        self._update_step_count()
        self._set_state(STATE_IDLE)

    def _toggle_recording(self) -> None:
        if self._recorder.is_recording:
            self._stop_recording()
        else:
            self._start_recording()

    def _on_recorded_step(self, step: MacroStep) -> None:
        """Called from the recorder's listener thread."""
        # Must schedule UI updates on the main thread
        self._root.after(0, self._editor.append_step, step)
        self._root.after(0, self._update_step_count)

    # ------------------------------------------------------------------
    # Playback
    # ------------------------------------------------------------------

    def _play(self) -> None:
        if self._player and self._player.is_playing:
            return
        # Sync steps from editor
        self._macro.steps = self._editor.get_steps()
        if not self._macro.steps:
            messagebox.showinfo("Empty Macro", "No steps to play.")
            return

        loop_raw = self._loop_var.get()
        try:
            loop_count = int(loop_raw)
        except ValueError:
            loop_count = 1
        if loop_count == 0:
            loop_count = INFINITE_LOOPS

        self._stop_event.clear()
        self._player = Player(
            steps=self._macro.steps,
            loop_count=loop_count,
            speed_multiplier=float(self._speed_var.get()),
            playback_mode=self._mode_var.get(),
            on_step_start=self._on_player_step,
            on_finished=self._on_player_finished,
            stop_event=self._stop_event,
        )
        self._player.start()
        self._set_state(STATE_PLAYING)

    def _emergency_stop(self) -> None:
        self._stop_event.set()
        if self._recorder.is_recording:
            self._stop_recording()
        if self._player:
            self._player.stop()
        self._set_state(STATE_STOPPED)

    def _on_player_step(self, idx: int, step: MacroStep) -> None:
        self._root.after(0, lambda: self._highlight_step(idx))

    def _on_player_finished(self) -> None:
        self._root.after(0, lambda: self._set_state(STATE_IDLE))

    def _highlight_step(self, idx: int) -> None:
        try:
            iid = str(idx)
            children = self._editor._tree.get_children()
            if iid in children:
                self._editor._tree.selection_set(iid)
                self._editor._tree.see(iid)
        except Exception:
            pass

    # ------------------------------------------------------------------
    # Region selection
    # ------------------------------------------------------------------

    def _select_region(self) -> None:
        # Must close/hide main window briefly or overlay won't cover it
        self._root.iconify()
        self._root.after(200, self._run_region_selector)

    def _run_region_selector(self) -> None:
        from .overlay import select_region
        result = select_region()
        self._root.deiconify()
        if result:
            self._selected_region = result
            self._region_var.set(
                f"Region: ({result['x']},{result['y']}) "
                f"{result['w']}×{result['h']}"
            )
        else:
            self._region_var.set("Region: cancelled")

    # ------------------------------------------------------------------
    # Save / Load
    # ------------------------------------------------------------------

    def _save_macro(self) -> None:
        if self._current_file:
            self._do_save(self._current_file)
        else:
            self._save_macro_as()

    def _save_macro_as(self) -> None:
        path = filedialog.asksaveasfilename(
            title="Save Macro",
            defaultextension=".json",
            filetypes=[("Macro files", "*.json"), ("All files", "*.*")],
        )
        if path:
            self._current_file = path
            self._do_save(path)

    def _do_save(self, path: str) -> None:
        self._macro.steps = self._editor.get_steps()
        self._macro.name  = self._name_var.get()
        try:
            loop_count = int(self._loop_var.get())
        except ValueError:
            loop_count = 1
        self._macro.loop_count       = loop_count
        self._macro.speed_multiplier = float(self._speed_var.get())
        self._macro.playback_mode    = self._mode_var.get()
        try:
            self._macro.save(path)
            self._root.title(f"Macro Recorder — {os.path.basename(path)}")
        except Exception as exc:
            messagebox.showerror("Save Error", str(exc))

    def _load_macro(self) -> None:
        path = filedialog.askopenfilename(
            title="Load Macro",
            filetypes=[("Macro files", "*.json"), ("All files", "*.*")],
        )
        if not path:
            return
        try:
            macro = MacroFile.load(path)
            self._macro          = macro
            self._current_file   = path
            self._name_var.set(macro.name)
            self._loop_var.set(str(macro.loop_count))
            self._speed_var.set(macro.speed_multiplier)
            self._mode_var.set(macro.playback_mode)
            self._editor.set_steps(macro.steps)
            self._update_step_count()
            self._root.title(f"Macro Recorder — {os.path.basename(path)}")
        except Exception as exc:
            messagebox.showerror("Load Error", str(exc))

    def _new_macro(self) -> None:
        if not messagebox.askyesno("New Macro",
                                   "Discard current macro and create a new one?"):
            return
        self._macro        = MacroFile()
        self._current_file = None
        self._name_var.set(self._macro.name)
        self._editor.clear()
        self._update_step_count()
        self._root.title("Macro Recorder")

    def _clear_steps(self) -> None:
        if not messagebox.askyesno("Clear Steps", "Remove all steps?"):
            return
        self._editor.clear()
        self._macro.steps = []
        self._update_step_count()

    # ------------------------------------------------------------------
    # Step count sync
    # ------------------------------------------------------------------

    def _sync_steps_from_editor(self) -> None:
        self._macro.steps = self._editor.get_steps()
        self._update_step_count()

    def _update_step_count(self) -> None:
        self._step_count_var.set(f"Steps: {len(self._editor.get_steps())}")

    # ------------------------------------------------------------------
    # Hotkeys
    # ------------------------------------------------------------------

    def _start_hotkeys(self) -> None:
        self._hotkeys = HotkeyManager(callbacks={
            "record_toggle": self._toggle_recording,
            "play":          self._play,
            "stop":          self._emergency_stop,
        })
        try:
            self._hotkeys.start()
        except Exception:
            pass  # Hotkeys are a best-effort feature

    # ------------------------------------------------------------------
    # Mouse position polling
    # ------------------------------------------------------------------

    def _poll_mouse_pos(self) -> None:
        """Update mouse coordinates in the status bar every 100 ms."""
        try:
            from pynput.mouse import Controller
            ctrl = Controller()
            x, y = ctrl.position
            self._mouse_pos_var.set(f"Mouse: ({int(x)}, {int(y)})")
        except Exception:
            pass
        self._root.after(100, self._poll_mouse_pos)

    # ------------------------------------------------------------------
    # Help dialogs
    # ------------------------------------------------------------------

    def _show_hotkeys(self) -> None:
        messagebox.showinfo(
            "Global Hotkeys",
            "F8  →  Start / Stop Recording (toggle)\n"
            "F9  →  Play macro\n"
            "F10 →  Emergency Stop\n\n"
            "Ctrl+S  →  Save\n"
            "Ctrl+O  →  Open\n"
            "Ctrl+N  →  New macro",
        )

    def _show_about(self) -> None:
        messagebox.showinfo(
            "About Macro Recorder",
            "Advanced Macro Recorder & Player\n\n"
            "Records mouse and keyboard actions,\n"
            "saves them as JSON, and replays them.\n\n"
            "Technologies: tkinter · pynput · mss · Pillow",
        )

    # ------------------------------------------------------------------
    # Close
    # ------------------------------------------------------------------

    def _on_close(self) -> None:
        self._emergency_stop()
        try:
            self._hotkeys.stop()
        except Exception:
            pass
        self._root.destroy()

    # ------------------------------------------------------------------
    # Main loop
    # ------------------------------------------------------------------

    def run(self) -> None:
        self._root.mainloop()
