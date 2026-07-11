"""Parallax background layers for ESCAPE.

Two zone themes ("grotto", "depths") x 3 layers (far, mid, near).
Final size 640x360; drawn at 320x180 and NEAREST-upscaled 2x so background
pixels are chunkier than the 16px foreground tiles that sit on top.

All layers are horizontally tileable by construction: every horizontal
feature is a sum of integer-harmonic sinusoids (period == layer width) and
every placed feature is drawn with x modulo the layer width.
"""
from __future__ import annotations

import math
import random

import numpy as np

from . import palette
from .core import blank, hex_rgba, out_path, save_single, scale

W, H = 320, 180  # working resolution; output is 2x
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


LAYERS = {
    "grotto_far": grotto_far,
    "grotto_mid": grotto_mid,
    "grotto_near": grotto_near,
    "depths_far": depths_far,
    "depths_mid": depths_mid,
    "depths_near": depths_near,
}


def generate():
    for name, fn in LAYERS.items():
        img = scale(fn(), 2)  # 640x360
        save_single(img, name, out_path("Backgrounds", f"{name}.png"))


if __name__ == "__main__":
    generate()
