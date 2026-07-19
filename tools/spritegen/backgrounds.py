"""Parallax background layers + intro cutscene plates for ESCAPE.

Two zone themes ("grotto", "depths") x 3 layers (far, mid, near).
Final size 640x360; drawn at 320x180 and NEAREST-upscaled 2x so background
pixels are chunkier than the 16px foreground tiles that sit on top.

All layers are horizontally tileable by construction: every horizontal
feature is a sum of integer-harmonic sinusoids (period == layer width) and
every placed feature is drawn with x modulo the layer width.

The four cutscene story plates and the title-screen backdrop are full-bleed
illustrations — they do NOT tile. They ship at NATIVE 960x540 (no NEAREST
upscale): each is composed in a 240x135 "composition space" (so the layouts
match the original plates), rasterized hard-edged at 1920x1080, then
LANCZOS-downsampled 2x for cheap anti-aliasing. Gradients are smooth
multi-stop ramps with a fine deterministic grain so 8-bit bands never show.
"""
from __future__ import annotations

import math
import random

import numpy as np
from PIL import Image as _PILImage
from PIL import ImageDraw as _PILDraw

from . import palette
from .core import blank, blit, hex_rgba, out_path, save_single, scale

W, H = 320, 180  # layer working resolution; output is 2x
PW, PH = 240, 135    # plate COMPOSITION space (layout coordinates)
PLATE_W, PLATE_H = 960, 540   # native plate output size
_PSS = 2                      # plate supersample factor (AA via downsample)
_S = PLATE_W * _PSS // PW     # composition units -> supersampled px (8)
_HW, _HH = PW * _S, PH * _S   # supersampled canvas: 1920x1080
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
# Intro cutscene story plates + title backdrop: native 960x540.
# Composed in 240x135 units, rasterized at 1920x1080, LANCZOS-halved.
# =====================================================================


def _plate_finish(img):
    """LANCZOS-downsample the supersampled canvas to the native plate size."""
    pil = _PILImage.fromarray(img, "RGBA")
    out = pil.resize((PLATE_W, PLATE_H), _PILImage.Resampling.LANCZOS)
    arr = np.array(out)
    arr[:, :, 3] = 255  # plates are full-bleed opaque
    return arr


def _grad_rows(stops):
    """(canvas-height, 3) float rows interpolating hex stops given in comp-y."""
    ys = np.array([s[0] * _S for s in stops], dtype=np.float64)
    cols = np.array([hex_rgba(s[1])[:3] for s in stops], dtype=np.float64)
    rows = np.arange(_HH)
    return np.stack([np.interp(rows, ys, cols[:, c]) for c in range(3)], axis=1)


def _vgrad_smooth(img, stops, seed, grain=1.6):
    """Smooth multi-stop vertical gradient + fine deterministic grain."""
    band = _grad_rows(stops)
    rng = np.random.RandomState(seed)
    noise = rng.uniform(-grain, grain, size=(img.shape[0], img.shape[1], 3))
    img[:, :, :3] = np.clip(band[:, None, :] + noise, 0, 255).astype(np.uint8)
    img[:, :, 3] = 255


def _fill_below_shaded(img, curve, top_hex, bot_hex, y0_comp, y1_comp):
    """Fill below `curve` with a smooth vertical tone ramp (comp-y stops)."""
    band = _grad_rows([(y0_comp, top_hex), (y1_comp, bot_hex)]).astype(np.uint8)
    h = img.shape[0]
    for x in range(img.shape[1]):
        t = max(0, min(h, int(curve[x])))
        img[t:, x, :3] = band[t:]
        img[t:, x, 3] = 255


def _glow(img, cx, cy, rx, ry, color, strength, power=1.6):
    """Soft elliptical light (comp coords/radii): lerps RGB toward `color`."""
    cx, cy, rx, ry = cx * _S, cy * _S, rx * _S, ry * _S
    x0 = max(0, int(cx - rx))
    x1 = min(img.shape[1], int(cx + rx) + 1)
    y0 = max(0, int(cy - ry))
    y1 = min(img.shape[0], int(cy + ry) + 1)
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1]
    d = ((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2
    a = np.clip(1.0 - d, 0.0, 1.0) ** power * strength
    region = img[y0:y1, x0:x1, :3].astype(np.float64)
    col = np.array(hex_rgba(color)[:3], dtype=np.float64)
    img[y0:y1, x0:x1, :3] = (region + (col - region) * a[:, :, None]).astype(np.uint8)


def _mask_bounds(img, xs, ys):
    x0 = max(0, int(min(xs)) - 1)
    x1 = min(img.shape[1], int(max(xs)) + 2)
    y0 = max(0, int(min(ys)) - 1)
    y1 = min(img.shape[0], int(max(ys)) + 2)
    return x0, x1, y0, y1


def _draw_poly(img, pts_comp, color):
    """Filled hard-edged polygon; points in comp coords (floats OK)."""
    xs = [p[0] * _S for p in pts_comp]
    ys = [p[1] * _S for p in pts_comp]
    x0, x1, y0, y1 = _mask_bounds(img, xs, ys)
    if x1 <= x0 or y1 <= y0:
        return
    m = _PILImage.new("L", (x1 - x0, y1 - y0), 0)
    _PILDraw.Draw(m).polygon([(x - x0, y - y0) for x, y in zip(xs, ys)], fill=255)
    mask = np.array(m) > 0
    img[y0:y1, x0:x1][mask] = hex_rgba(color) if isinstance(color, str) else color


def _draw_ellipse(img, cx, cy, rx, ry, color):
    """Filled hard-edged ellipse; comp coords/radii (floats OK)."""
    xs = ((cx - rx) * _S, (cx + rx) * _S)
    ys = ((cy - ry) * _S, (cy + ry) * _S)
    x0, x1, y0, y1 = _mask_bounds(img, xs, ys)
    if x1 <= x0 or y1 <= y0:
        return
    m = _PILImage.new("L", (x1 - x0, y1 - y0), 0)
    _PILDraw.Draw(m).ellipse(
        [xs[0] - x0, ys[0] - y0, xs[1] - x0, ys[1] - y0], fill=255)
    mask = np.array(m) > 0
    img[y0:y1, x0:x1][mask] = hex_rgba(color) if isinstance(color, str) else color


def _star_pts(cx, cy, r_out, r_in, points=5, rot=-math.pi / 2):
    """Star polygon vertices in comp coords."""
    pts = []
    for i in range(points * 2):
        r = r_out if i % 2 == 0 else r_in
        a = rot + i * math.pi / points
        pts.append((cx + r * math.cos(a), cy + r * math.sin(a)))
    return pts


def _sparkle(img, x, y, r, core_hex, halo_hex, glow_r=None):
    """4-point glint: halo cross + hot core, optional soft bloom behind."""
    if glow_r:
        _glow(img, x, y, glow_r, glow_r, halo_hex, 0.35, power=2.0)
    _draw_poly(img, _star_pts(x, y, r, r * 0.30, points=4), halo_hex)
    _draw_poly(img, _star_pts(x, y, r * 0.55, r * 0.20, points=4), core_hex)


def _hero_smooth(img, x0, y0, color, accent, facing=1, zoom=1):
    """Cloaked hero silhouette, comp footprint 7x10*zoom; feet at y0+10*zoom.

    Smooth-shape update of the old 7x10 pixel hero: same stance, colors and
    placement, drawn as hood + flaring cloak + boots so the AA downsample
    gives it clean edges instead of fat NEAREST blocks.
    """
    u = zoom * _S
    layer = _PILImage.new("RGBA", (7 * u, 10 * u), (0, 0, 0, 0))
    d = _PILDraw.Draw(layer)
    col = tuple(int(v) for v in color)
    acc = tuple(int(v) for v in accent)
    # hood
    d.ellipse([1.55 * u, 0.15 * u, 5.45 * u, 3.7 * u], fill=col)
    # cloak flaring from the shoulders to the hem
    d.polygon([(3.5 * u, 1.9 * u), (0.55 * u, 8.9 * u), (6.45 * u, 8.9 * u)], fill=col)
    d.rectangle([0.9 * u, 7.7 * u, 6.1 * u, 8.9 * u], fill=col)
    # boots
    d.rectangle([1.7 * u, 8.6 * u, 2.9 * u, 10 * u - 1], fill=col)
    d.rectangle([4.1 * u, 8.6 * u, 5.3 * u, 10 * u - 1], fill=col)
    # hood rim catching the hero's own glow on the facing side
    hx = (4.6 if facing > 0 else 2.4) * u
    d.ellipse([hx - 0.55 * u, 0.9 * u, hx + 0.55 * u, 2.0 * u], fill=acc)
    blit(img, np.array(layer), int(x0 * _S), int(y0 * _S))


def _boulder_smooth(img, cx, cy, r, body_hex, shade_hex, rim_hex):
    """Round boulder, comp coords: lit upper-left rim -> body -> shaded foot."""
    cx, cy, r = cx * _S, cy * _S, r * _S
    x0 = max(0, int(cx - r))
    x1 = min(img.shape[1], int(cx + r) + 1)
    y0 = max(0, int(cy - r))
    y1 = min(img.shape[0], int(cy + r) + 1)
    if x1 <= x0 or y1 <= y0:
        return
    yy, xx = np.mgrid[y0:y1, x0:x1]
    dx = (xx - cx) / r
    dy = (yy - cy) / r
    inside = dx * dx + dy * dy <= 1.0
    t = np.clip((dx + dy) * 0.25 + 0.5, 0.0, 1.0)   # 0 upper-left -> 1 lower-right
    rim = np.array(hex_rgba(rim_hex)[:3], dtype=np.float64)
    body = np.array(hex_rgba(body_hex)[:3], dtype=np.float64)
    shade = np.array(hex_rgba(shade_hex)[:3], dtype=np.float64)
    t1 = np.clip(t / 0.38, 0.0, 1.0)[:, :, None]
    t2 = np.clip((t - 0.5) / 0.5, 0.0, 1.0)[:, :, None]
    col = rim + (body - rim) * t1
    col = col + (shade - col) * t2
    img[y0:y1, x0:x1, :3][inside] = col[inside].astype(np.uint8)
    img[y0:y1, x0:x1, 3][inside] = 255


def _entrance_scene():
    """Shared dusk cave-mouth framing for the entrance / collapse plates."""
    rng = random.Random(301)
    img = blank(_HW, _HH)
    # dusk sky, brightest just above the rocks
    _vgrad_smooth(
        img,
        [(0, "#1b1230"), (22, "#43224a"), (40, "#8a3c46"), (56, "#d97a3c")],
        seed=311,
    )
    # low sun bloom sitting right on the cliff line
    _glow(img, 120, 58, 95, 30, "#e8935a", 0.45, power=2.2)
    # cliff face holding the cave mouth, dusk-lit crest fading to dark
    top = _wrapped_curve(
        rng, _HW, 62 * _S, [10 * _S, 6 * _S, 4 * _S, 2 * _S, 1.2 * _S])
    _fill_below_shaded(img, top, "#31242b", "#150d13", 62, 135)
    crest = hex_rgba("#443137")
    for x in range(_HW):
        t = max(0, min(_HH - 3, int(top[x])))
        img[t : t + 3, x] = crest
    speck = hex_rgba("#180f16")
    for _ in range(1600):  # rock texture
        _dot(img, rng.randrange(_HW), rng.randrange(70 * _S, _HH), speck,
             rng.randrange(2, 4))
    img[124 * _S :, :] = hex_rgba("#100a10")  # shadowed ground at the base
    # the mouth: an arch of near-black cut into the cliff, lifting at the floor
    void_rows = _grad_rows([(70, "#060309"), (124, "#0b0612")]).astype(np.uint8)
    rim = hex_rgba("#d97a3c")
    cx, half_w, floor_y, height = 120, 34, 124, 54
    for x in range((cx - half_w) * _S, (cx + half_w) * _S + 1):
        t = (x / _S - cx) / half_w
        top_y = int((floor_y - height * math.sqrt(max(0.0, 1 - t * t))) * _S)
        img[top_y : floor_y * _S, x, :3] = void_rows[top_y : floor_y * _S]
        img[top_y : floor_y * _S, x, 3] = 255
        if x < (cx - 4) * _S:  # dusk light grazing the left rim
            img[top_y : top_y + 3, x] = rim
    return img, (cx, half_w, floor_y, height)


def cutscene_entrance():
    """Cave mouth at dusk; the heroes walk in, gold star glints deep inside."""
    img, (cx, half_w, floor_y, _height) = _entrance_scene()
    rng = random.Random(302)
    for i in range(7):  # star glints deep in the dark
        x = cx - 22 + rng.randrange(44)
        y = floor_y - 26 + rng.randrange(20)
        if i < 3:  # the brightest read as tiny sparkles with a soft bloom
            _sparkle(img, x, y, 1.6, "#fff3c4", "#f4c05a", glow_r=5)
        else:
            _sparkle(img, x, y, 1.0, "#f4c05a", "#c98a2e")
    # soft contact shadows + the two heroes walking in, WISP trailing EMBER
    _glow(img, 111.5, floor_y - 0.4, 7, 1.7, "#060309", 0.55, power=1.2)
    _glow(img, 97.5, floor_y - 0.4, 7, 1.7, "#060309", 0.55, power=1.2)
    _hero_smooth(img, 108, floor_y - 10,
                 palette.EMBER["primary"], palette.EMBER["secondary"], 1)
    _hero_smooth(img, 94, floor_y - 10,
                 palette.WISP["primary"], palette.WISP["secondary"], 1)
    return _plate_finish(img)


def cutscene_collapse():
    """The entrance collapses: boulders, dust, the light down to a sliver."""
    img, (cx, half_w, floor_y, height) = _entrance_scene()
    body, shade, rim = "#3a2a2c", "#241a1e", "#55403c"
    # rubble pile filling the mouth, biggest blocks at the bottom
    pile = [
        (96, 118, 8), (114, 119, 9), (132, 118, 8), (146, 120, 6),
        (90, 112, 5), (150, 112, 5),
        (103, 106, 7), (121, 104, 8), (138, 107, 6),
        (107, 97, 4), (133, 97, 4), (120, 93, 4),
        (112, 92, 6), (128, 91, 5), (120, 82, 5),
    ]
    for bx, by, r in pile:
        _boulder_smooth(img, bx, by, r, body, shade, rim)
    # two more still falling across the shrinking gap
    for bx, by, r in [(108, 74, 4), (131, 77, 4)]:
        _boulder_smooth(img, bx, by, r, body, shade, rim)
        for i in range(3):  # soft motion streaks above
            _glow(img, bx, by - r - 2 - i * 2.2, 1.1, 2.0, shade, 0.7, power=1.2)
    # one last glint through the sliver still open at the top
    _sparkle(img, 119, 73, 1.5, "#fff3c4", "#f4c05a", glow_r=4)
    # dust clouds rolling off the impact: layered soft puffs
    for dx_, dy_, r in [(86, 120, 7), (154, 117, 8), (120, 124, 9), (98, 96, 5), (142, 94, 5)]:
        _glow(img, dx_, dy_, r, r * 0.8, "#6a5648", 0.5, power=1.2)
        _glow(img, dx_ - r * 0.4, dy_ + r * 0.25, r * 0.6, r * 0.5, "#7a6656", 0.35,
              power=1.2)
    return _plate_finish(img)


def cutscene_stars():
    """Dark cavern: a swarm of gold stars streaks down into the depths."""
    rng = random.Random(303)
    img = blank(_HW, _HH)
    _vgrad_smooth(img, [(0, "#050308"), (70, "#070410"), (135, "#0a0512")],
                  seed=313, grain=1.2)
    wall = hex_rgba("#120c18")
    wall_lit = hex_rgba("#221830")
    # cavern vault + floor crags, barely lit
    ceil = _wrapped_curve(rng, _HW, 14 * _S, [5 * _S, 3 * _S, 1.5 * _S])
    _fill_above(img, ceil, wall)
    for _ in range(8):
        x = rng.randrange(_HW)
        _spike_down(img, x, rng.randrange(5 * _S, 10 * _S),
                    rng.randrange(10 * _S, 26 * _S), wall, rim=wall_lit)
    floor = _wrapped_curve(rng, _HW, (PH - 12) * _S, [4 * _S, 3 * _S])
    _fill_below(img, floor, wall)
    for _ in range(6):
        x = rng.randrange(_HW)
        _mound_up(img, x, rng.randrange(10 * _S, 24 * _S),
                  rng.randrange(6 * _S, 16 * _S), wall, rim=wall_lit)
    # faint warm glow pooling where the stars are headed
    _glow(img, 120, 126, 55, 18, "#3a2416", 0.5, power=1.8)

    # the swarm: gold 5-point stars streaking down-right, soft trails behind
    def star(x0, y0, big):
        cx_, cy_ = x0 + 8, y0 + 8   # cell origin -> star center (old layout)
        for i in range(1, 6):  # fading streak trail, up and left
            _glow(img, cx_ - 2 * i, cy_ - 3 * i, 0.9 + 0.12 * i, 0.9 + 0.12 * i,
                  "#c98a2e", max(0.08, 0.5 - 0.08 * i), power=1.4)
        r = 7 if big else 4
        _glow(img, cx_, cy_, r * 2.1, r * 2.1, "#c98a2e", 0.28, power=2.0)
        _draw_poly(img, _star_pts(cx_, cy_, r, r * 0.42), "#c98a2e")
        if big:
            _draw_poly(img, _star_pts(cx_, cy_, 5, 2), "#f4c05a")
            _glow(img, cx_ - 0.5, cy_ - 1.5, 1.7, 1.7, "#fff3c4", 0.85, power=1.0)

    for x0, y0 in [(48, 20), (96, 38), (142, 24), (76, 68), (122, 74), (166, 56)]:
        star(x0, y0, True)
    for x0, y0 in [(28, 44), (66, 12), (108, 14), (150, 92), (94, 96), (178, 84), (40, 82)]:
        star(x0, y0, False)
    for _ in range(14):  # loose glints trailing the swarm
        _sparkle(img, rng.randrange(PW), rng.randrange(16, PH - 20), 0.8,
                 "#f4c05a", "#c98a2e")
    return _plate_finish(img)


def cutscene_resolve():
    """Near-black: the heroes face each other, lit only by their own glow."""
    rng = random.Random(304)
    img = blank(_HW, _HH)
    _vgrad_smooth(img, [(0, "#050308"), (135, "#060310")], seed=314, grain=1.0)
    # the faintest hint of the cavern around them
    hint = hex_rgba("#0d0a12")
    ceil = _wrapped_curve(rng, _HW, 10 * _S, [4 * _S, 3 * _S])
    _fill_above(img, ceil, hint)
    floor = _wrapped_curve(rng, _HW, 100 * _S, [3 * _S, 2 * _S])
    _fill_below(img, floor, hint)
    # each hero's own light pooling around them (smooth two-lobe glows)
    _glow(img, 103, 90, 19, 16, "#2e1210", 0.75, power=1.6)   # EMBER's warmth
    _glow(img, 103, 90, 11, 9, "#6e2c1c", 0.6, power=1.4)
    _glow(img, 137, 90, 19, 16, "#101c30", 0.75, power=1.6)   # WISP's shimmer
    _glow(img, 137, 90, 11, 9, "#275079", 0.6, power=1.4)
    # a faint path of stones descending into the dark below them
    for i in range(9):
        _draw_ellipse(img, 117 + i * 5, 104.6 + i * 3, 1.6, 0.9,
                      "#241c26" if i < 5 else "#161018")
    # heroes at double zoom, facing each other
    _hero_smooth(img, 96, 80, palette.EMBER["primary"], palette.EMBER["secondary"],
                 1, zoom=2)
    _hero_smooth(img, 130, 80, palette.WISP["primary"], palette.WISP["secondary"],
                 -1, zoom=2)
    return _plate_finish(img)


def cutscene_dust():
    """Soft round dust mote for the collapse-beat cutscene overlay.

    Analytic two-lobe radial falloff — smooth at any UI scale (imports
    Bilinear via the Backgrounds/cutscene_* rule, unlike the 8px pixel-art
    FX dust_puff, which would read as blocks over the AA plates). Warm
    near-white; the intro builder tints it down to rolling-dust brown.
    """
    size = 128
    img = blank(size, size)
    yy, xx = np.mgrid[0:size, 0:size]
    c = (size - 1) / 2.0
    d1 = np.sqrt((xx - c) ** 2 + (yy - c) ** 2) / (size / 2.0)
    lobe1 = np.clip(1.0 - d1, 0.0, 1.0) ** 2.0
    d2 = np.sqrt((xx - c * 0.80) ** 2 + (yy - c * 1.12) ** 2) / (size / 2.6)
    lobe2 = np.clip(1.0 - d2, 0.0, 1.0) ** 2.4
    alpha = np.clip(lobe1 * 0.85 + lobe2 * 0.45, 0.0, 1.0)
    img[:, :, 0], img[:, :, 1], img[:, :, 2] = 239, 227, 212   # #efe3d4
    img[:, :, 3] = np.round(alpha * 235).astype(np.uint8)
    return img


def title_bg():
    """Title-screen backdrop: night cave-mouth vista framing a calm center.

    Composition contract: the center / center-left stays clear and
    low-contrast so the big white title, the PLAY button, and the dark
    vignette the menu builder layers on top all read cleanly.
    """
    rng = random.Random(401)
    img = blank(_HW, _HH)
    # night sky: black at the top, deep dusk violet toward the horizon
    _vgrad_smooth(
        img,
        [(0, "#050308"), (24, "#0c0916"), (52, "#161028"), (82, "#221532")],
        seed=411, grain=1.4,
    )
    # sparse dim sky stars (the story's stars, long before they fall)
    for i in range(22):
        x = rng.randrange(PW)
        y = rng.randrange(4, 78)
        if i % 4 == 0:
            _sparkle(img, x, y, 0.8, "#5c5480", "#48406a")
        else:
            _draw_ellipse(img, x, y, 0.35, 0.35, "#3c3654")
    for x, y in [(18, 12), (222, 18), (204, 7)]:
        _sparkle(img, x, y, 0.7, "#c98a2e", "#8f6420")  # faint gold pinpricks
    # distant ridge silhouettes on the horizon
    ridge_far = _wrapped_curve(rng, _HW, 96 * _S, [7 * _S, 4 * _S, 3 * _S])
    _fill_below_shaded(img, ridge_far, "#171024", "#100a18", 96, 135)
    ridge_near = _wrapped_curve(rng, _HW, 108 * _S, [5 * _S, 3 * _S, 2 * _S])
    _fill_below_shaded(img, ridge_near, "#0f0a15", "#0a0511", 108, 135)
    # cave floor: dark foreground band the menu buttons sit above
    floor = _wrapped_curve(rng, _HW, 118 * _S, [3 * _S, 2 * _S])
    _fill_below(img, floor, hex_rgba("#080510"))
    # the cave arch: near-black rock framing the top and side edges
    rock = hex_rgba("#070409")
    rock_lit = hex_rgba("#170f20")
    jag = _wrapped_curve(rng, _HW, 0, [3 * _S, 2 * _S])

    def arch_depth(x):  # x in canvas px, returns canvas depth
        return max(2 * _S,
                   int((8 + 34 * (abs(x / _S - 120) / 120.0) ** 2.0) * _S + jag[x]))

    for x in range(_HW):
        depth = arch_depth(x)
        img[:depth, x] = rock
        img[depth : depth + 2, x] = rock_lit  # night sky grazing the underside
    # stalactites off the arch underside, kept clear of the title zone
    for sx in (28, 46, 66, 176, 196, 214):
        ln = (7 + (sx * 7) % 8) * _S
        topw = (5 + (sx // 3) % 3) * _S
        d0 = arch_depth(sx * _S)
        for row in range(ln):
            half = max(0, int(topw / 2 * (1 - row / ln)))
            y = d0 + row
            if y < _HH:
                img[y, max(0, sx * _S - half) : min(_HW, sx * _S + half + 1)] = rock
    lw = _wrapped_curve(rng, _HH, 0, [4 * _S, 3 * _S])
    rw = _wrapped_curve(rng, _HH, 0, [4 * _S, 3 * _S])
    for y in range(_HH):
        wl = max(0, int((5 + 30 * (y / _HH) ** 1.5) * _S + lw[y]))
        if wl > 0:
            wl = min(wl, _HW - 1)
            lit = tuple(img[y, wl]) not in (rock, rock_lit)
            img[y, :wl] = rock
            if lit:
                img[y, wl : min(_HW, wl + 2)] = rock_lit
        wr = max(0, int((5 + 30 * (y / _HH) ** 1.5) * _S + rw[y]))
        if wr > 0:
            wr = min(wr, _HW - 1)
            lit = tuple(img[y, _HW - wr - 1]) not in (rock, rock_lit)
            img[y, _HW - wr :] = rock
            if lit:
                img[y, max(0, _HW - wr - 2) : _HW - wr] = rock_lit
    # sparse texture on the rock so the frame isn't a flat mass
    speck = hex_rgba("#100a18")
    for _ in range(2400):
        x = rng.randrange(_HW)
        y = rng.randrange(_HH)
        if tuple(img[y, x]) == rock:
            _dot(img, x, y, speck, rng.randrange(2, 4))

    # faint hero-colored glow hints pooling low in the frame
    _glow(img, 64, 125, 16, 11, "#1e0e0c", 0.6, power=1.5)    # EMBER warmth
    _glow(img, 64, 125, 8, 6, "#3c1c14", 0.5, power=1.3)
    _glow(img, 176, 125, 16, 11, "#0e1626", 0.6, power=1.5)   # WISP shimmer
    _glow(img, 176, 125, 8, 6, "#18324e", 0.5, power=1.3)
    # gold star glints deep in the dark: cave floor + lower side masses
    glints = [(44, 127), (150, 124), (118, 129), (196, 126), (78, 131), (10, 100), (230, 96)]
    for i, (x, y) in enumerate(glints):
        if i < 3:  # the brightest read as tiny sparkles with a soft bloom
            _sparkle(img, x, y, 1.3, "#fff3c4", "#f4c05a", glow_r=4)
        else:
            _sparkle(img, x, y, 0.8, "#f4c05a", "#c98a2e")
    return _plate_finish(img)


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
    "title_bg": title_bg,
}


def generate():
    for name, fn in LAYERS.items():
        img = scale(fn(), 2)  # 640x360
        save_single(img, name, out_path("Backgrounds", f"{name}.png"))
    for name, fn in PLATES.items():
        # native 960x540 — no NEAREST upscale (plate fns downsample for AA)
        save_single(fn(), "plate_0", out_path("Backgrounds", f"{name}.png"))
    # smooth overlay sprite for the intro's collapse beat (not a plate)
    save_single(cutscene_dust(), "dust_0", out_path("Backgrounds", "cutscene_dust.png"))


if __name__ == "__main__":
    generate()
