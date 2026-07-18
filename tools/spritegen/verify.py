"""Acceptance checks for the generated art.

  1. Every expected PNG + JSON manifest exists and loads.
  2. Hero sheets use only their 5 palette colors + outline + transparency.
  3. Background layers wrap horizontally (seam column diff is within the
     distribution of ordinary adjacent-column diffs).
  4. Determinism: regenerating produces byte-identical files.

Run: tools/.venv/bin/python -m spritegen.verify   (from tools/)
"""
from __future__ import annotations

import hashlib
import json
import os
import subprocess
import sys

import numpy as np

if __package__ in (None, ""):
    sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    from spritegen import palette
    from spritegen.core import OUT_ROOT, load_png
else:
    from . import palette
    from .core import OUT_ROOT, load_png

EXPECTED = {
    "Characters": ["hero1_ember", "hero2_wisp"],
    "Props": [
        "lava", "bridge", "lever", "crystal_slab", "exit_portal", "checkpoint_torch",
        "door_ember", "door_wisp", "star", "star_empty", "powerup_ember", "powerup_wisp",
    ],
    "UI": ["dpad_left", "dpad_right", "dpad_down", "btn_a", "btn_b", "btn_swap", "btn_pause"],
    "Backgrounds": [
        "grotto_far", "grotto_mid", "grotto_near",
        "depths_far", "depths_mid", "depths_near",
        "cutscene_entrance", "cutscene_collapse", "cutscene_stars", "cutscene_resolve",
    ],
    "FX": ["dust_puff", "spark", "impact_ring", "death_poof", "death_poof_strip", "lava_bubble"],
    "Icons": ["app_icon_1024", "playstore_512", "adaptive_foreground_432", "adaptive_background_432"],
}

FRAME_COUNTS = {
    "hero1_ember": 18,   # idle2 walk4 jump1 fall1 crouch1 hurt1 death4 win2 shoot2
    "hero2_wisp": 20,    # idle2 walk4 jump1 fall1 crouch1 hurt1 death4 win2 transform4
    "lava": 6, "bridge": 6, "lever": 4, "crystal_slab": 4,
    "exit_portal": 4, "checkpoint_torch": 6, "death_poof_strip": 4,
    "door_ember": 7, "door_wisp": 7,   # closed_0 half_0 half_1 open_0..open_3
    "star": 4, "star_empty": 1, "powerup_ember": 4, "powerup_wisp": 4,
}


def check_files():
    ok = True
    for cat, names in EXPECTED.items():
        for n in names:
            png = os.path.join(OUT_ROOT, cat, f"{n}.png")
            man = os.path.join(OUT_ROOT, cat, f"{n}.json")
            if not os.path.exists(png) or not os.path.exists(man):
                print(f"  MISSING: {cat}/{n}")
                ok = False
                continue
            img = load_png(png)
            with open(man) as f:
                m = json.load(f)
            frames = len(m["frames"])
            fw, fh = m["frameWidth"], m["frameHeight"]
            if img.shape[1] != fw * frames or img.shape[0] != fh:
                print(f"  BAD DIMS: {cat}/{n}: png {img.shape[1]}x{img.shape[0]} vs "
                      f"manifest {fw}x{fh} x{frames}")
                ok = False
            want = FRAME_COUNTS.get(n)
            if want is not None and frames != want:
                print(f"  BAD FRAME COUNT: {cat}/{n}: {frames} != {want}")
                ok = False
    print(f"[verify] files + manifests: {'OK' if ok else 'FAIL'}")
    return ok


def check_hero_palettes():
    ok = True
    for name, pal in [("hero1_ember", palette.EMBER), ("hero2_wisp", palette.WISP)]:
        img = load_png(os.path.join(OUT_ROOT, "Characters", f"{name}.png"))
        seen = {tuple(int(v) for v in px) for px in img.reshape(-1, 4)}
        allowed = {tuple(c) for c in pal.values()} | {palette.OUTLINE}
        seen_opaque = {c for c in seen if c[3] != 0}
        extra = seen_opaque - allowed
        n_palette = len({c for c in pal.values()} - {palette.OUTLINE})
        if extra:
            print(f"  {name}: colors outside palette: {sorted(extra)}")
            ok = False
        if len(seen_opaque - {palette.OUTLINE}) > 5:
            print(f"  {name}: more than 5 non-outline colors: {len(seen_opaque) - 1}")
            ok = False
        transparent = {c for c in seen if c[3] == 0}
        if any(c != (0, 0, 0, 0) for c in transparent):
            # harmless but keep the sheets clean
            print(f"  {name}: non-zero RGB under transparent pixels")
            ok = False
    print(f"[verify] hero palettes: {'OK' if ok else 'FAIL'}")
    return ok


def check_backgrounds_tileable():
    ok = True
    for n in EXPECTED["Backgrounds"]:
        if n.startswith("cutscene_"):
            continue  # story plates are full-bleed illustrations, not scrolling layers
        img = load_png(os.path.join(OUT_ROOT, "Backgrounds", f"{n}.png")).astype(np.int32)
        cols = img.reshape(img.shape[0], img.shape[1], 4)
        diffs = np.abs(cols[:, 1:] - cols[:, :-1]).mean(axis=(0, 2))
        seam = np.abs(cols[:, 0] - cols[:, -1]).mean()
        limit = diffs.max()
        if seam > limit:
            print(f"  {n}: seam diff {seam:.2f} exceeds max interior diff {limit:.2f}")
            ok = False
    print(f"[verify] background wrap seams: {'OK' if ok else 'FAIL'}")
    return ok


def _hash_tree():
    hashes = {}
    for root, _, files in os.walk(OUT_ROOT):
        for f in sorted(files):
            p = os.path.join(root, f)
            with open(p, "rb") as fh:
                hashes[os.path.relpath(p, OUT_ROOT)] = hashlib.sha256(fh.read()).hexdigest()
    return hashes


def check_determinism():
    before = _hash_tree()
    subprocess.run(
        [sys.executable, "-m", "spritegen.generate_all"],
        cwd=os.path.dirname(os.path.dirname(os.path.abspath(__file__))),
        check=True,
        capture_output=True,
    )
    after = _hash_tree()
    diff = [k for k in before if before[k] != after.get(k)]
    diff += [k for k in after if k not in before]
    if diff:
        print(f"  non-deterministic files: {diff}")
    print(f"[verify] determinism: {'OK' if not diff else 'FAIL'}")
    return not diff


def main():
    results = [
        check_files(),
        check_hero_palettes(),
        check_backgrounds_tileable(),
        check_determinism(),
    ]
    if all(results):
        print("[verify] ALL CHECKS PASSED")
        return 0
    print("[verify] FAILURES PRESENT")
    return 1


if __name__ == "__main__":
    sys.exit(main())
