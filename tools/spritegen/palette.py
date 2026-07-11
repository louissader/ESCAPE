"""Named palettes for ESCAPE.

Hero palettes are fixed by design. Environment accents are sampled
deterministically from the Grotto Escape tileset so generated props and
backgrounds sit in the same world as the licensed art.
"""
from __future__ import annotations

import os
from collections import Counter

from .core import REPO_ROOT, hex_rgba, load_png

OUTLINE = hex_rgba("#2a1a22")
TRANSPARENT = (0, 0, 0, 0)

# Shared neutral / skin ramp (used by both heroes; counts toward their 5).
SKIN = hex_rgba("#f0d0a8")
NEUTRAL_DARK = hex_rgba("#4a3540")

# P1 "EMBER" — angular hooded hero. 5 colors + outline + transparency.
EMBER = {
    "primary": hex_rgba("#8f2b2b"),   # deep red (hood, tunic)
    "secondary": hex_rgba("#e07438"),  # orange (scarf, boots cuff)
    "highlight": hex_rgba("#f4c05a"),  # gold (belt, trim, muzzle flash)
    "skin": SKIN,
    "dark": NEUTRAL_DARK,              # boots, shading
    "outline": OUTLINE,
}

# P2 "WISP" — rounded cloaked hero. 5 colors + outline + transparency.
WISP = {
    "primary": hex_rgba("#2b4a8f"),    # deep blue (cloak)
    "secondary": hex_rgba("#38aee0"),  # cyan (rim, trim)
    "highlight": hex_rgba("#7af4c0"),  # mint (wisp tail, glow)
    "skin": SKIN,
    "dark": NEUTRAL_DARK,              # feet, shading
    "outline": OUTLINE,
}

GROTTO_TILES = os.path.join(
    REPO_ROOT, "Assets", "GrottoEscapeArtwork", "Sprites", "Tileset", "grotto-tiles.png"
)

# Fallbacks (hand-sampled from the pack) in case the tileset ever moves.
_ENV_FALLBACK = {
    "cave_bg": hex_rgba("#021721"),
    "cave_navy": hex_rgba("#002a3e"),
    "cave_wall": hex_rgba("#056592"),
    "cave_edge": hex_rgba("#21b6fb"),
    "stone": hex_rgba("#30534e"),
    "stone_dark": hex_rgba("#223e3a"),
    "stone_light": hex_rgba("#4a847c"),
    "lava_orange": hex_rgba("#ff8d07"),
    "plant_green": hex_rgba("#04946d"),
    "plant_dark": hex_rgba("#047657"),
}


def _classify(colors):
    """Pick named environment accents from a color frequency table."""

    def lum(c):
        return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]

    named = {}
    opaque = [(c, n) for c, n in colors.items() if c[3] == 255 and n >= 8]
    if not opaque:
        return dict(_ENV_FALLBACK)

    # darkest common color -> cave background
    named["cave_bg"] = min(opaque, key=lambda cn: lum(cn[0]))[0]

    # blue-dominant family (b > g > r): dark navy wall fill, mid wall, bright edge
    blues = [(c, n) for c, n in opaque if c[2] > c[1] > c[0] and c != named["cave_bg"]]
    if blues:
        named["cave_navy"] = max(blues, key=lambda cn: cn[1])[0]  # most common
        named["cave_edge"] = max(blues, key=lambda cn: lum(cn[0]))[0]  # brightest
        mids = [c for c, n in blues if c not in (named["cave_navy"], named["cave_edge"])]
        if mids:
            named["cave_wall"] = max(mids, key=lum)

    # green-gray family (g dominant, b close to g) -> stone ramp
    stones = [
        (c, n)
        for c, n in opaque
        if c[1] > c[0] and c[1] >= c[2] and c[1] - c[2] < 30 and n >= 100
    ]
    if stones:
        named["stone"] = max(stones, key=lambda cn: cn[1])[0]  # most common block color
        named["stone_dark"] = min(stones, key=lambda cn: lum(cn[0]))[0]
        named["stone_light"] = max(stones, key=lambda cn: lum(cn[0]))[0]

    # warm orange -> lava
    oranges = [c for c, n in opaque if c[0] > 180 and c[2] < 90]
    if oranges:
        named["lava_orange"] = max(oranges, key=lum)

    # saturated green -> plants
    greens = sorted(
        (c for c, n in opaque if c[1] > 100 and c[1] > c[0] + 40 and c[1] > c[2] + 30),
        key=lum,
    )
    if greens:
        named["plant_dark"] = greens[0]
        named["plant_green"] = greens[-1]

    for key, val in _ENV_FALLBACK.items():
        named.setdefault(key, val)
    return {k: tuple(int(v) for v in c) for k, c in named.items()}


def _sample_env():
    if not os.path.exists(GROTTO_TILES):
        return dict(_ENV_FALLBACK)
    img = load_png(GROTTO_TILES)
    counts = Counter(tuple(int(v) for v in px) for px in img.reshape(-1, 4))
    return _classify(counts)


ENV = _sample_env()

# Palette used for the exit portal (mystical green/white, on top of env greens).
PORTAL = {
    "green_deep": hex_rgba("#1e7a44"),
    "green": ENV["plant_green"],
    "white": hex_rgba("#e8fff0"),
    "void": hex_rgba("#0a1410"),
    "outline": OUTLINE,
}
