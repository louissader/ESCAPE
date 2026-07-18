"""Parallax background layers + intro cutscene plates for ESCAPE.

Two zone themes ("grotto", "depths") x 3 layers (far, mid, near).
Final size 640x360; drawn at 320x180 and NEAREST-upscaled 2x so background
pixels are chunkier than the 16px foreground tiles that sit on top.

All layers are horizontally tileable by construction: every horizontal
feature is a sum of integer-harmonic sinusoids (period == layer width) and
every placed feature is drawn with x modulo the layer width.

The four cutscene story plates (480x270, drawn at 240x135) are full-bleed
illustrations for the intro sequence — they do NOT tile.
"""
from __future__ import annotations

import math
import random

import numpy as np

from . import palette
from .core import blank, hex_rgba, out_path, save_single, scale
from .props import _star_points

W, H = 320, 180  # working resolution; output is 2x
PW, PH = 240, 135  # cutscene plate working resolution; output is 2x (480x270)
ENV = palette.ENV


def _wrapped_curve(rng: random.Random, w: int, base: float, amps) -> np.ndarray:
    """Height curve that wraps perfectly: sum of integer-harmonic sinusoids."""
    xs = np.arange(w)
    y = np.full(w, float(base))
    for k, amp in enumerate(amps, start=1):
        phase = rng.random() * 2 * math.pi
        y += amp * np.sin(2 * math.pi * k * xs / w + phase)
    return y


def _fill_below(img, curve, color, y_off=0):
    for x in range(img.shape[1]):
        top = int(curve[x]) + y_off
        top = max(0, min(img.shape[0], top))
        img[top:, x] = color


def _fill_above(img, curve, color, y_off=0):
    for x in range(img.shape[1]):
        bot = int(curve[x]) + y_off
        bot = max(0, min(img.shape[0], bot))
        img[:bot, x] = color


def _dot(img, x, y, color, size=1):
    h, w = img.shape[:2]
    for dy in range(size):
        for dx in range(size):
            if 0 <= y + dy < h:
                img[y + dy, (x + dx) % w] = color


def _spike_down(img, x0, top_w, length, color, rim=None):
    """Stalactite hanging from y=0, drawn with wrapped x."""
    h, w = img.shape[:2]
    for row in range(length):
        half = max(0, int(top_w / 2 * (1 - row / length)))
        for dx in range(-half, half + 1):
            if row < h:
                img[row, (x0 + dx) % w] = color
        if rim is not None and row < h:
            img[row, (x0 + half) % w] = rim


def _mound_up(img, x0, base_w, height, color, rim=None):
    """Rock mound rising from the bottom edge, wrapped x."""
    h, w = img.shape[:2]
    for i in range(height):
        row = h - 1 - i
        half = max(0, int(base_w / 2 * (1 - i / height)))
        for dx in range(-half, half + 1):
            img[row, (x0 + dx) % w] = color
        if rim is not None:
            img[row, (x0 - half) % w] = rim


# =====================================================================
# GROTTO theme: dark teal-green cave.
# =====================================================================

def grotto_far():
    rng = random.Random(101)
    img = blank(W, H)
    bg = ENV["cave_bg"]
    navy = ENV["cave_navy"]
    wall = ENV["cave_wall"]
    img[:, :] = bg
    # faint upper vault
    vault = _wrapped_curve(rng, W, 30, [8, 5, 3])
    _fill_above(img, vault, navy)
    # distant wall silhouette across the lower half
    ridge = _wrapped_curve(rng, W, 110, [14, 9, 5, 3])
    _fill_below(img, ridge, navy)
    ridge2 = _wrapped_curve(rng, W, 140, [10, 6, 4])
    _fill_below(img, ridge2, hex_rgba("#03222f"))
    # sparse wall glints
    for _ in range(26):
        x = rng.randrange(W)
        y = rng.randrange(40, H - 10)
        if tuple(img[y, x]) != bg:
            _dot(img, x, y, wall, 1)
    return img


def grotto_mid():
    rng = random.Random(102)
    img = blank(W, H)  # transparent
    navy = ENV["cave_navy"]
    wall = ENV["cave_wall"]
    dark = hex_rgba("#03222f")
    # ceiling strip with hanging stalactites
    ceil = _wrapped_curve(rng, W, 16, [4, 3])
    _fill_above(img, ceil, navy)
    for _ in range(14):
        x = rng.randrange(W)
        _spike_down(img, x, rng.randrange(6, 14), rng.randrange(18, 52), navy, rim=dark)
    # floor rocks
    floor = _wrapped_curve(rng, W, H - 14, [4, 3])
    _fill_below(img, floor, navy)
    for _ in range(11):
        x = rng.randrange(W)
        _mound_up(img, x, rng.randrange(16, 40), rng.randrange(16, 44), navy, rim=dark)
    # sparse edge highlights on silhouettes
    for _ in range(40):
        x = rng.randrange(W)
        y = rng.randrange(H)
        if img[y, x, 3] > 0 and (y + 1 < H and img[(y + 1) % H, x, 3] == 0):
            _dot(img, x, y, wall, 1)
    return img


def grotto_near():
    rng = random.Random(103)
    img = blank(W, H)
    dark = hex_rgba("#021018")
    pd = ENV["plant_dark"]
    pg = ENV["plant_green"]
    # top fringe: hanging vines/roots
    fringe = _wrapped_curve(rng, W, 8, [3, 2])
    _fill_above(img, fringe, dark)
    for _ in range(30):
        x = rng.randrange(W)
        ln = rng.randrange(4, 16)
        for i in range(ln):
            img[min(H - 1, int(fringe[x]) + i), x % W] = dark if i < ln - 2 else pd
    # bottom fringe: rocks + grass tufts
    ground = _wrapped_curve(rng, W, H - 7, [2, 2])
    _fill_below(img, ground, dark)
    for _ in range(26):
        x = rng.randrange(W)
        gy = int(ground[x])
        h = rng.randrange(2, 6)
        for i in range(h):
            img[max(0, gy - 1 - i), x % W] = pd if i < h - 1 else pg
    for _ in range(9):
        x = rng.randrange(W)
        _mound_up(img, x, rng.randrange(10, 22), rng.randrange(6, 16), dark, rim=pd)
    return img


# =====================================================================
# DEPTHS theme: darker, red / ember accents.
# =====================================================================

_D_BG = hex_rgba("#0b060a")
_D_ROCK = hex_rgba("#170d13")
_D_RED = hex_rgba("#3a1010")
_D_MAGMA = hex_rgba("#7a1a08")
_D_ORANGE = ENV["lava_orange"]
_D_GOLD = hex_rgba("#ffd23f")


def depths_far():
    rng = random.Random(201)
    img = blank(W, H)
    img[:, :] = _D_BG
    # magma glow rising from below: layered gradient ridges
    glow1 = _wrapped_curve(rng, W, 120, [10, 6, 4])
    _fill_below(img, glow1, _D_RED)
    glow2 = _wrapped_curve(rng, W, 145, [8, 5, 3])
    _fill_below(img, glow2, _D_MAGMA)
    glow3 = _wrapped_curve(rng, W, 168, [4, 3])
    _fill_below(img, glow3, _D_ORANGE)
    # far wall silhouette hanging into the glow
    wallc = _wrapped_curve(rng, W, 60, [16, 10, 6])
    _fill_above(img, wallc, _D_ROCK)
    # drifting ember pinpricks
    for _ in range(24):
        x = rng.randrange(W)
        y = rng.randrange(70, H - 20)
        _dot(img, x, y, _D_ORANGE, 1)
    for _ in range(8):
        x = rng.randrange(W)
        y = rng.randrange(90, H - 30)
        _dot(img, x, y, _D_GOLD, 1)
    return img


def depths_mid():
    rng = random.Random(202)
    img = blank(W, H)
    # obsidian ceiling spikes with red rim on one side
    ceil = _wrapped_curve(rng, W, 12, [4, 3])
    _fill_above(img, ceil, _D_ROCK)
    for _ in range(12):
        x = rng.randrange(W)
        _spike_down(img, x, rng.randrange(8, 16), rng.randrange(22, 60), _D_ROCK, rim=_D_RED)
    # obsidian floor crags
    floor = _wrapped_curve(rng, W, H - 12, [4, 3])
    _fill_below(img, floor, _D_ROCK)
    for _ in range(10):
        x = rng.randrange(W)
        _mound_up(img, x, rng.randrange(18, 44), rng.randrange(20, 52), _D_ROCK, rim=_D_MAGMA)
    # magma cracks glinting on rock
    for _ in range(30):
        x = rng.randrange(W)
        y = rng.randrange(H)
        if img[y, x, 3] > 0:
            _dot(img, x, y, _D_RED, 1)
    return img


def depths_near():
    rng = random.Random(203)
    img = blank(W, H)
    dark = hex_rgba("#060309")
    # top + bottom rock frame
    fringe = _wrapped_curve(rng, W, 6, [2, 2])
    _fill_above(img, fringe, dark)
    ground = _wrapped_curve(rng, W, H - 6, [2, 2])
    _fill_below(img, ground, dark)
    for _ in range(8):
        x = rng.randrange(W)
        _mound_up(img, x, rng.randrange(10, 20), rng.randrange(5, 12), dark, rim=_D_RED)
    # floating ember particles: single dots and tiny crosses
    for _ in range(38):
        x = rng.randrange(W)
        y = rng.randrange(12, H - 12)
        _dot(img, x, y, _D_ORANGE, 1)
    for _ in range(10):
        x = rng.randrange(W)
        y = rng.randrange(20, H - 20)
        _dot(img, x, y, _D_GOLD, 1)
        _dot(img, x - 1, y, _D_ORANGE, 1)
        _dot(img, x + 1, y, _D_ORANGE, 1)
        _dot(img, x, y - 1, _D_ORANGE, 1)
        _dot(img, x, y + 1, _D_ORANGE, 1)
    return img


# =====================================================================
# Intro cutscene story plates: 240x135 painterly-pixel, output 480x270.
# =====================================================================

_HERO_ROWS = [
    "..XXX..",
    ".XXXXX.",
    ".XXXXX.",
    "..XXX..",
    ".XXXXX.",
    "XXXXXXX",
    "XXXXXXX",
    ".XXXXX.",
    ".XX.XX.",
    ".XX.XX.",
]


def _hero(img, x0, y0, color, accent, facing=1, zoom=1):
    """Tiny cloaked hero silhouette (7x10 at zoom 1); feet at y0 + 10*zoom."""
    for dy, row in enumerate(_HERO_ROWS):
        for dx, ch in enumerate(row):
            if ch != "X":
                continue
            for zy in range(zoom):
                for zx in range(zoom):
                    img[y0 + dy * zoom + zy, x0 + dx * zoom + zx] = color
    # hood rim catching the hero's own glow on the facing side
    hx = x0 + (4 if facing > 0 else 2) * zoom
    for zy in range(zoom):
        for zx in range(zoom):
            img[y0 + zoom + zy, hx + zx] = accent


def _vgrad(img, stops):
    """Vertical color bands with 2-row checker-dithered edges (dusk sky)."""
    h, w = img.shape[:2]
    ys = [s[0] for s in stops] + [h]
    for i, (y0, col) in enumerate(stops):
        img[y0 : ys[i + 1], :] = col
    for i in range(1, len(stops)):
        yb = stops[i][0]
        below, above = stops[i][1], stops[i - 1][1]
        for y in (yb - 1, yb):
            if 0 <= y < h:
                for x in range(w):
                    if (x + y) % 2 == 0:
                        img[y, x] = below if y == yb - 1 else above


def _boulder(img, cx, cy, r, body, shade, rim):
    """Rounded boulder: lower-right shading, upper-left lit rim."""
    h, w = img.shape[:2]
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy > r * r:
                continue
            y, x = cy + dy, cx + dx
            if not (0 <= y < h and 0 <= x < w):
                continue
            if dx + dy > r * 0.5:
                img[y, x] = shade
            elif dx + dy < -r * 0.9:
                img[y, x] = rim
            else:
                img[y, x] = body


def _dust(img, cx, cy, r, col):
    """Checker-dithered dust puff."""
    h, w = img.shape[:2]
    for dy in range(-r, r + 1):
        for dx in range(-r, r + 1):
            if dx * dx + dy * dy <= r * r and (dx + dy) % 2 == 0:
                y, x = cy + dy, cx + dx
                if 0 <= y < h and 0 <= x < w:
                    img[y, x] = col


def _entrance_scene():
    """Shared dusk cave-mouth framing for the entrance / collapse plates."""
    rng = random.Random(301)
    img = blank(PW, PH)
    # dusk sky, brightest just above the rocks
    _vgrad(
        img,
        [
            (0, hex_rgba("#1b1230")),
            (22, hex_rgba("#43224a")),
            (40, hex_rgba("#8a3c46")),
            (56, hex_rgba("#d97a3c")),
        ],
    )
    # cliff face holding the cave mouth
    rock = hex_rgba("#241820")
    rock_dark = hex_rgba("#180f16")
    rock_lit = hex_rgba("#3a2a30")
    top = _wrapped_curve(rng, PW, 62, [10, 6, 4])
    _fill_below(img, top, rock)
    for x in range(PW):
        img[int(top[x]), x] = rock_lit  # dusk light catching the crest
    for _ in range(140):  # rock texture
        _dot(img, rng.randrange(PW), rng.randrange(70, PH), rock_dark, 1)
    img[124:, :] = hex_rgba("#100a10")  # shadowed ground at the base
    # the mouth: an arch of near-black cut into the cliff
    void = hex_rgba("#060309")
    rim = hex_rgba("#d97a3c")
    cx, half_w, floor_y, height = 120, 34, 124, 54
    for x in range(cx - half_w, cx + half_w + 1):
        t = (x - cx) / half_w
        top_y = floor_y - int(height * math.sqrt(max(0.0, 1 - t * t)))
        img[top_y:floor_y, x] = void
        if x < cx - 4:  # dusk light grazing the left rim
            img[top_y, x] = rim
    return img, (cx, half_w, floor_y, height)


def cutscene_entrance():
    """Cave mouth at dusk; the heroes walk in, gold star glints deep inside."""
    img, (cx, half_w, floor_y, _height) = _entrance_scene()
    rng = random.Random(302)
    gold = hex_rgba("#f4c05a")
    gold_dim = hex_rgba("#c98a2e")
    for i in range(7):  # star glints deep in the dark
        x = cx - 22 + rng.randrange(44)
        y = floor_y - 26 + rng.randrange(20)
        _dot(img, x, y, gold if i < 3 else gold_dim, 1)
        if i < 3:  # the brightest read as tiny + twinkles
            _dot(img, x - 1, y, gold_dim, 1)
            _dot(img, x + 1, y, gold_dim, 1)
            _dot(img, x, y - 1, gold_dim, 1)
            _dot(img, x, y + 1, gold_dim, 1)
    # the two heroes walking into the dark, WISP trailing EMBER
    _hero(img, 108, floor_y - 10, palette.EMBER["primary"], palette.EMBER["secondary"], 1)
    _hero(img, 94, floor_y - 10, palette.WISP["primary"], palette.WISP["secondary"], 1)
    return img


def cutscene_collapse():
    """The entrance collapses: boulders, dust, the light down to a sliver."""
    img, (cx, half_w, floor_y, height) = _entrance_scene()
    body = hex_rgba("#3a2a2c")
    shade = hex_rgba("#241a1e")
    rim = hex_rgba("#55403c")
    # rubble pile filling the mouth, biggest blocks at the bottom
    pile = [
        (96, 118, 8), (114, 119, 9), (132, 118, 8), (146, 120, 6),
        (90, 112, 5), (150, 112, 5),
        (103, 106, 7), (121, 104, 8), (138, 107, 6),
        (107, 97, 4), (133, 97, 4), (120, 93, 4),
        (112, 92, 6), (128, 91, 5), (120, 82, 5),
    ]
    for bx, by, r in pile:
        _boulder(img, bx, by, r, body, shade, rim)
    # two more still falling across the shrinking gap
    for bx, by, r in [(108, 74, 4), (131, 77, 4)]:
        _boulder(img, bx, by, r, body, shade, rim)
        for i in range(3):  # motion streaks above
            _dot(img, bx, by - r - 2 - i * 2, shade, 1)
    # one last glint through the sliver still open at the top
    _dot(img, 119, 73, hex_rgba("#f4c05a"), 1)
    # dust clouds rolling off the impact
    dust = hex_rgba("#6a5648")
    for dx_, dy_, r in [(86, 120, 7), (154, 117, 8), (120, 124, 9), (98, 96, 5), (142, 94, 5)]:
        _dust(img, dx_, dy_, r, dust)
    return img


def cutscene_stars():
    """Dark cavern: a swarm of gold stars streaks down into the depths."""
    rng = random.Random(303)
    img = blank(PW, PH)
    img[:, :] = hex_rgba("#050308")
    wall = hex_rgba("#120c18")
    wall_lit = hex_rgba("#221830")
    # cavern vault + floor crags, barely lit
    ceil = _wrapped_curve(rng, PW, 14, [5, 3])
    _fill_above(img, ceil, wall)
    for _ in range(8):
        x = rng.randrange(PW)
        _spike_down(img, x, rng.randrange(5, 10), rng.randrange(10, 26), wall, rim=wall_lit)
    floor = _wrapped_curve(rng, PW, PH - 12, [4, 3])
    _fill_below(img, floor, wall)
    for _ in range(6):
        x = rng.randrange(PW)
        _mound_up(img, x, rng.randrange(10, 24), rng.randrange(6, 16), wall, rim=wall_lit)
    # faint warm glow pooling where the stars are headed
    warm = hex_rgba("#1c120e")
    for y in range(PH - 18, PH):
        for x in range(70, 170):
            if x % 3 == 0 and y % 3 == 0:
                img[y, x] = warm

    # the swarm: the same 5-point silhouette as props star.png, streaking down
    gold = hex_rgba("#f4c05a")
    gold_dim = hex_rgba("#c98a2e")
    gold_hot = hex_rgba("#fff3c4")
    big_o = _star_points(8, 8, 7, 3)
    big_i = _star_points(8, 8, 5, 2)
    small = _star_points(8, 8, 4, 1.6)

    def star(x0, y0, big):
        for px, py in (big_o if big else small):
            y, x = y0 + py, x0 + px
            if 0 <= y < PH and 0 <= x < PW:
                img[y, x] = gold_dim
        if big:
            for px, py in big_i:
                img[y0 + py, x0 + px] = gold
            img[y0 + 5, x0 + 7] = gold_hot
        for i in range(1, 6, 2):  # dashed streak trail, up and left
            y, x = y0 + 8 - 3 * i, x0 + 8 - 2 * i
            if 0 <= y < PH and 0 <= x < PW:
                img[y, x] = gold_dim

    for x0, y0 in [(48, 20), (96, 38), (142, 24), (76, 68), (122, 74), (166, 56)]:
        star(x0, y0, True)
    for x0, y0 in [(28, 44), (66, 12), (108, 14), (150, 92), (94, 96), (178, 84), (40, 82)]:
        star(x0, y0, False)
    for _ in range(14):  # loose glints trailing the swarm
        _dot(img, rng.randrange(PW), rng.randrange(16, PH - 20), gold_dim, 1)
    return img


def cutscene_resolve():
    """Near-black: the heroes face each other, lit only by their own glow."""
    rng = random.Random(304)
    img = blank(PW, PH)
    img[:, :] = hex_rgba("#050308")
    # the faintest hint of the cavern around them
    hint = hex_rgba("#0d0a12")
    ceil = _wrapped_curve(rng, PW, 10, [4, 3])
    _fill_above(img, ceil, hint)
    floor = _wrapped_curve(rng, PW, 100, [3, 2])
    _fill_below(img, floor, hint)

    def glow(cx_, cy_, inner, outer):
        for y in range(PH):
            for x in range(PW):
                d = math.hypot(x - cx_, (y - cy_) * 1.2)
                if d <= 11 and (x + y) % 2 == 0:
                    img[y, x] = inner
                elif d <= 19 and x % 2 == 0 and y % 2 == 0:
                    img[y, x] = outer

    glow(103, 90, hex_rgba("#6e2c1c"), hex_rgba("#2e1210"))   # EMBER's warmth
    glow(137, 90, hex_rgba("#275079"), hex_rgba("#101c30"))   # WISP's shimmer
    # a faint path of stones descending into the dark below them
    path = hex_rgba("#241c26")
    path_far = hex_rgba("#161018")
    for i in range(9):
        _dot(img, 116 + i * 5, 104 + i * 3, path if i < 5 else path_far, 2)
    # heroes at double zoom, facing each other
    _hero(img, 96, 80, palette.EMBER["primary"], palette.EMBER["secondary"], 1, zoom=2)
    _hero(img, 130, 80, palette.WISP["primary"], palette.WISP["secondary"], -1, zoom=2)
    return img


LAYERS = {
    "grotto_far": grotto_far,
    "grotto_mid": grotto_mid,
    "grotto_near": grotto_near,
    "depths_far": depths_far,
    "depths_mid": depths_mid,
    "depths_near": depths_near,
}

PLATES = {
    "cutscene_entrance": cutscene_entrance,
    "cutscene_collapse": cutscene_collapse,
    "cutscene_stars": cutscene_stars,
    "cutscene_resolve": cutscene_resolve,
}


def generate():
    for name, fn in LAYERS.items():
        img = scale(fn(), 2)  # 640x360
        save_single(img, name, out_path("Backgrounds", f"{name}.png"))
    for name, fn in PLATES.items():
        img = scale(fn(), 2)  # 480x270
        save_single(img, "plate_0", out_path("Backgrounds", f"{name}.png"))


if __name__ == "__main__":
    generate()
