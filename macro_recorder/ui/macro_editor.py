"""
macro_editor.py - Visual step editor widget.

Provides a Tkinter-based table (Treeview) displaying all macro steps with
columns: #, Type, Coordinates, Key/Value, Delay, Enabled.

Actions available via toolbar buttons or context menu:
  - Edit selected step's delay / key / value.
  - Move step up / down.
  - Delete step.
  - Enable / disable step.
  - Duplicate step.
  - Insert a WAIT step before the selection.
"""

from __future__ import annotations
import tkinter as tk
from tkinter import ttk, messagebox, simpledialog
from typing import List, Callable, Optional

from ..core.events import MacroStep, StepType


class MacroEditor(ttk.Frame):
    """
    A self-contained Ttk Frame that displays and edits a list of MacroStep
    objects.  Embed this inside the main window.
    """

    COL_NO    = "#"
    COL_TYPE  = "Type"
    COL_COORD = "Coordinates"
    COL_VAL   = "Key / Value"
    COL_DELAY = "Delay (s)"
    COL_EN    = "Enabled"

    COLUMNS = (COL_NO, COL_TYPE, COL_COORD, COL_VAL, COL_DELAY, COL_EN)

    def __init__(self, parent, on_change: Optional[Callable] = None, **kwargs):
        """
        on_change: called whenever the step list is modified.
        """
        super().__init__(parent, **kwargs)
        self._steps: List[MacroStep] = []
        self._on_change = on_change
        self._build_ui()

    # ------------------------------------------------------------------
    # Public API
    # ------------------------------------------------------------------

    def set_steps(self, steps: List[MacroStep]) -> None:
        """Replace the displayed step list."""
        self._steps = steps
        self._refresh()

    def get_steps(self) -> List[MacroStep]:
        return list(self._steps)

    def clear(self) -> None:
        self._steps = []
        self._refresh()

    def append_step(self, step: MacroStep) -> None:
        """Append one step and refresh the table."""
        self._steps.append(step)
        self._refresh()
        # Auto-scroll to bottom
        children = self._tree.get_children()
        if children:
            self._tree.see(children[-1])

    # ------------------------------------------------------------------
    # UI construction
    # ------------------------------------------------------------------

    def _build_ui(self) -> None:
        self.columnconfigure(0, weight=1)
        self.rowconfigure(1, weight=1)

        # ---- Toolbar ----
        toolbar = ttk.Frame(self)
        toolbar.grid(row=0, column=0, sticky="ew", padx=2, pady=2)

        btn_cfg = [
            ("▲ Up",        self._move_up),
            ("▼ Down",      self._move_down),
            ("✎ Edit",      self._edit_step),
            ("⊕ Insert Wait", self._insert_wait),
            ("⊕ Duplicate", self._duplicate),
            ("✕ Delete",    self._delete),
            ("☑ Toggle",    self._toggle_enabled),
        ]
        for label, cmd in btn_cfg:
            ttk.Button(toolbar, text=label, command=cmd, width=12).pack(
                side="left", padx=2)

        # ---- Treeview ----
        tree_frame = ttk.Frame(self)
        tree_frame.grid(row=1, column=0, sticky="nsew")
        tree_frame.rowconfigure(0, weight=1)
        tree_frame.columnconfigure(0, weight=1)

        self._tree = ttk.Treeview(
            tree_frame,
            columns=self.COLUMNS,
            show="headings",
            selectmode="browse",
        )
        col_widths = {
            self.COL_NO:    45,
            self.COL_TYPE: 130,
            self.COL_COORD: 140,
            self.COL_VAL:  200,
            self.COL_DELAY:  75,
            self.COL_EN:     60,
        }
        for col in self.COLUMNS:
            self._tree.heading(col, text=col)
            self._tree.column(col, width=col_widths[col], minwidth=40,
                               anchor="center")

        vsb = ttk.Scrollbar(tree_frame, orient="vertical",
                             command=self._tree.yview)
        self._tree.configure(yscrollcommand=vsb.set)

        self._tree.grid(row=0, column=0, sticky="nsew")
        vsb.grid(row=0, column=1, sticky="ns")

        # Row colouring tags
        self._tree.tag_configure("disabled", foreground="#999999")
        self._tree.tag_configure("even",     background="#F8F8F8")
        self._tree.tag_configure("odd",      background="#FFFFFF")

        # Double-click to edit
        self._tree.bind("<Double-1>", lambda e: self._edit_step())

    # ------------------------------------------------------------------
    # Table refresh
    # ------------------------------------------------------------------

    def _refresh(self) -> None:
        sel_idx = self._selected_index()
        for iid in self._tree.get_children():
            self._tree.delete(iid)
        for i, step in enumerate(self._steps):
            coord = ""
            if step.x is not None and step.y is not None:
                coord = f"({step.x}, {step.y})"
            val = ""
            if step.key is not None:
                val = step.key
            elif step.value is not None:
                v = step.value
                val = (str(v)[:28] + "…") if len(str(v)) > 28 else str(v)
            row = (
                str(i + 1),
                step.step_type,
                coord,
                val,
                f"{step.delay_after:.3f}",
                "✓" if step.enabled else "✗",
            )
            tags = []
            tags.append("even" if i % 2 == 0 else "odd")
            if not step.enabled:
                tags.append("disabled")
            self._tree.insert("", "end", iid=str(i), values=row, tags=tags)
        # Restore selection
        if sel_idx is not None and sel_idx < len(self._steps):
            self._tree.selection_set(str(sel_idx))
            self._tree.see(str(sel_idx))

    # ------------------------------------------------------------------
    # Selection helpers
    # ------------------------------------------------------------------

    def _selected_index(self) -> Optional[int]:
        sel = self._tree.selection()
        if not sel:
            return None
        return int(sel[0])

    def _require_selection(self) -> Optional[int]:
        idx = self._selected_index()
        if idx is None:
            messagebox.showinfo("No selection", "Please select a step first.")
        return idx

    # ------------------------------------------------------------------
    # Actions
    # ------------------------------------------------------------------

    def _move_up(self):
        idx = self._require_selection()
        if idx is None or idx == 0:
            return
        self._steps[idx], self._steps[idx - 1] = \
            self._steps[idx - 1], self._steps[idx]
        self._refresh()
        self._tree.selection_set(str(idx - 1))
        self._notify()

    def _move_down(self):
        idx = self._require_selection()
        if idx is None or idx >= len(self._steps) - 1:
            return
        self._steps[idx], self._steps[idx + 1] = \
            self._steps[idx + 1], self._steps[idx]
        self._refresh()
        self._tree.selection_set(str(idx + 1))
        self._notify()

    def _delete(self):
        idx = self._require_selection()
        if idx is None:
            return
        if not messagebox.askyesno("Delete Step",
                                   f"Delete step #{idx + 1}?"):
            return
        del self._steps[idx]
        self._refresh()
        self._notify()

    def _toggle_enabled(self):
        idx = self._require_selection()
        if idx is None:
            return
        self._steps[idx].enabled = not self._steps[idx].enabled
        self._refresh()
        self._tree.selection_set(str(idx))
        self._notify()

    def _duplicate(self):
        idx = self._require_selection()
        if idx is None:
            return
        clone = self._steps[idx].clone()
        self._steps.insert(idx + 1, clone)
        self._refresh()
        self._tree.selection_set(str(idx + 1))
        self._notify()

    def _insert_wait(self):
        idx = self._selected_index()
        secs = simpledialog.askfloat(
            "Insert Wait", "Duration in seconds:", minvalue=0.0, initialvalue=1.0)
        if secs is None:
            return
        step = MacroStep(step_type=StepType.WAIT, value=secs)
        pos  = idx + 1 if idx is not None else len(self._steps)
        self._steps.insert(pos, step)
        self._refresh()
        self._tree.selection_set(str(pos))
        self._notify()

    def _edit_step(self):
        idx = self._require_selection()
        if idx is None:
            return
        StepEditDialog(self, self._steps[idx], on_save=lambda: (self._refresh(), self._notify()))

    # ------------------------------------------------------------------
    # Change notification
    # ------------------------------------------------------------------

    def _notify(self):
        if self._on_change:
            try:
                self._on_change()
            except Exception:
                pass


# ---------------------------------------------------------------------------
# Step edit dialog
# ---------------------------------------------------------------------------

class StepEditDialog(tk.Toplevel):
    """Simple dialog to edit a single MacroStep's editable fields."""

    def __init__(self, parent, step: MacroStep, on_save=None):
        super().__init__(parent)
        self.title("Edit Step")
        self.resizable(False, False)
        self.grab_set()
        self._step    = step
        self._on_save = on_save
        self._build(step)
        self.transient(parent)
        self.wait_window()

    def _build(self, step: MacroStep) -> None:
        pad = {"padx": 8, "pady": 4}

        def row(label, widget_factory, row_num):
            ttk.Label(self, text=label).grid(row=row_num, column=0, sticky="e", **pad)
            w = widget_factory()
            w.grid(row=row_num, column=1, sticky="ew", **pad)
            return w

        self.columnconfigure(1, weight=1)
        r = 0

        # Step type (read-only)
        ttk.Label(self, text="Type:").grid(row=r, column=0, sticky="e", **pad)
        ttk.Label(self, text=step.step_type).grid(row=r, column=1, sticky="w", **pad)
        r += 1

        # Delay
        ttk.Label(self, text="Delay after (s):").grid(row=r, column=0, sticky="e", **pad)
        self._delay_var = tk.StringVar(value=str(step.delay_after))
        ttk.Entry(self, textvariable=self._delay_var).grid(row=r, column=1, sticky="ew", **pad)
        r += 1

        # X coordinate
        if step.x is not None:
            ttk.Label(self, text="X:").grid(row=r, column=0, sticky="e", **pad)
            self._x_var = tk.StringVar(value=str(step.x))
            ttk.Entry(self, textvariable=self._x_var).grid(row=r, column=1, sticky="ew", **pad)
            r += 1
        else:
            self._x_var = None

        if step.y is not None:
            ttk.Label(self, text="Y:").grid(row=r, column=0, sticky="e", **pad)
            self._y_var = tk.StringVar(value=str(step.y))
            ttk.Entry(self, textvariable=self._y_var).grid(row=r, column=1, sticky="ew", **pad)
            r += 1
        else:
            self._y_var = None

        # Key / value
        if step.key is not None:
            ttk.Label(self, text="Key:").grid(row=r, column=0, sticky="e", **pad)
            self._key_var = tk.StringVar(value=str(step.key))
            ttk.Entry(self, textvariable=self._key_var).grid(row=r, column=1, sticky="ew", **pad)
            r += 1
        else:
            self._key_var = None

        if step.value is not None:
            ttk.Label(self, text="Value:").grid(row=r, column=0, sticky="e", **pad)
            self._val_var = tk.StringVar(value=str(step.value))
            ttk.Entry(self, textvariable=self._val_var).grid(row=r, column=1, sticky="ew", **pad)
            r += 1
        else:
            self._val_var = None

        # Enabled
        ttk.Label(self, text="Enabled:").grid(row=r, column=0, sticky="e", **pad)
        self._en_var = tk.BooleanVar(value=step.enabled)
        ttk.Checkbutton(self, variable=self._en_var).grid(row=r, column=1, sticky="w", **pad)
        r += 1

        # Buttons
        btn_frame = ttk.Frame(self)
        btn_frame.grid(row=r, column=0, columnspan=2, pady=6)
        ttk.Button(btn_frame, text="Save",   command=self._save).pack(side="left", padx=4)
        ttk.Button(btn_frame, text="Cancel", command=self.destroy).pack(side="left", padx=4)

    def _save(self):
        try:
            self._step.delay_after = float(self._delay_var.get())
        except ValueError:
            messagebox.showerror("Invalid", "Delay must be a number.")
            return
        if self._x_var is not None:
            try:
                self._step.x = int(self._x_var.get())
            except ValueError:
                pass
        if self._y_var is not None:
            try:
                self._step.y = int(self._y_var.get())
            except ValueError:
                pass
        if self._key_var is not None:
            self._step.key = self._key_var.get()
        if self._val_var is not None:
            self._step.value = self._val_var.get()
        self._step.enabled = self._en_var.get()

        if self._on_save:
            self._on_save()
        self.destroy()
