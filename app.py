"""
app.py - Entry point for the Macro Recorder application.

Run with:
    python app.py
Or, after installing:
    macro-recorder
"""

import sys
import os

# Ensure the package root is on the path when running directly.
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))


def main():
    from macro_recorder.ui.main_window import MainWindow
    app = MainWindow()
    app.run()


if __name__ == "__main__":
    main()
