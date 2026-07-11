"""Regenerate every ESCAPE art asset into Assets/Art/Generated/.

Usage:
    tools/.venv/bin/python -m spritegen.generate_all   (from tools/)
or
    tools/.venv/bin/python tools/spritegen/generate_all.py

Deterministic: running twice produces byte-identical output.
"""
from __future__ import annotations

import os
import sys

if __package__ in (None, ""):
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    from spritegen import backgrounds, characters, contact_sheet, fx, icons, props
else:
    from . import backgrounds, characters, contact_sheet, fx, icons, props


def main():
    print("[spritegen] characters ...")
    characters.generate()
    print("[spritegen] props + touch UI ...")
    props.generate()
    print("[spritegen] backgrounds ...")
    backgrounds.generate()
    print("[spritegen] fx ...")
    fx.generate()
    print("[spritegen] icons ...")
    icons.generate()
    print("[spritegen] contact sheets ...")
    contact_sheet.generate()
    print("[spritegen] done.")


if __name__ == "__main__":
    main()
