"""App icons for ESCAPE.

Designed as small pixel art (64x64 / 48x48) then NEAREST-upscaled:
  - app icon 1024x1024 (64 x16)
  - Play Store icon 512x512 (64 x8)
  - Android adaptive foreground/background 432x432 (48 x9),
    critical content inside the central 264px-diameter safe circle.
"""
from __future__ import annotations

import random

from . import palette
from .characters import _ember_frame, _wisp_frame
from .core import blank, blit, hex_rgba, hflip, out_path, save_single, scale

ENV = palette.ENV


def _heroes():
    # Ember flipped so the hood tip points outward: back-to-back composition.
    ember = hflip(_ember_frame(show_tail=False))   # 16x24, outlined
    wisp = _wisp_frame(motes=1)                    # 16x24, outlined
    return ember, wisp


def _speckle(img, rng, region, colors, n):
    x0, y0, x1, y1 = region
    for _ in range(n):
        x = rng.randrange(x0, x1)
        y = rng.randrange(y0, y1)
        img[y, x] = colors[rng.randrange(len(colors))]


def build_app_icon_64():
    """64x64 composition: heroes back-to-back in a glowing cave mouth."""
    rng = random.Random(64)
    d = blank(64, 64)

    stone = ENV["stone"]
    stone_d = ENV["stone_dark"]
    stone_l = ENV["stone_light"]
    cave = ENV["cave_bg"]
    green = palette.PORTAL["green"]
    green_deep = palette.PORTAL["green_deep"]
    white = palette.PORTAL["white"]
    glow_dim = hex_rgba("#06301c")

    # rock face fills the frame (mostly dark speckles to keep it calm)
    d[:, :] = stone
    _speckle(d, rng, (0, 0, 64, 64), [stone_d, stone_d, stone_d, stone_l], 130)

    # carve the cave mouth (rough ellipse) into darkness
    cx, cy, rx, ry = 32, 37, 26.0, 25.0
    jitter = [rng.uniform(-1.5, 1.5) for _ in range(64)]
    for y in range(64):
        for x in range(64):
            dx = (x - cx) / rx
            dy = (y - cy) / ry
            r = dx * dx + dy * dy
            if r <= 1.0 + jitter[(x * 7 + y * 3) % 64] * 0.04:
                d[y, x] = cave
    # rim shadow just outside the opening
    for y in range(64):
        for x in range(64):
            if tuple(d[y, x]) == tuple(stone) or tuple(d[y, x]) == tuple(stone_l):
                dx = (x - cx) / (rx + 1.5)
                dy = (y - cy) / (ry + 1.5)
                if dx * dx + dy * dy <= 1.0:
                    d[y, x] = stone_d

    # portal glow behind the heroes (posterized ellipse bands).
    # The white core sits high so it peeks out between the heroes' heads.
    gx, gy, grx, gry = 32, 36, 14.0, 16.0
    for y in range(64):
        for x in range(64):
            if tuple(d[y, x]) != tuple(cave):
                continue
            dx = (x - gx) / grx
            dy = (y - gy + 4) / gry
            r = dx * dx + dy * dy
            if r <= 0.07:
                d[y, x] = white
            elif r <= 0.42:
                d[y, x] = green
            elif r <= 0.80:
                d[y, x] = green_deep
            elif r <= 1.20:
                d[y, x] = glow_dim

    # stone ledge the heroes stand on
    for y in range(52, 58):
        for x in range(8, 56):
            dx = (x - cx) / rx
            dy = (y - cy) / ry
            if dx * dx + dy * dy <= 1.05:
                d[y, x] = stone_d if y > 52 else stone
    _speckle(d, rng, (10, 53, 54, 58), [stone, stone_d], 30)

    # heroes back-to-back on the ledge (feet on y=51)
    ember, wisp = _heroes()
    blit(d, ember, 15, 28)
    blit(d, wisp, 33, 28)

    # sparkles
    gold = hex_rgba("#f4c05a")
    mint = palette.WISP["highlight"]
    for x, y, c in [(12, 22, gold), (50, 20, mint), (22, 16, white), (44, 44, mint), (18, 46, gold)]:
        d[y, x] = c
        d[y - 1, x] = c
        d[y + 1, x] = c
        d[y, x - 1] = c
        d[y, x + 1] = c

    return d


def build_adaptive_fg_48():
    """48x48 heroes-only on transparency; content inside r=14 of center."""
    d = blank(48, 48)
    ember, wisp = _heroes()
    blit(d, ember, 9, 12)
    blit(d, wisp, 23, 12)
    mint = palette.WISP["highlight"]
    gold = hex_rgba("#f4c05a")
    d[14, 14] = gold
    d[16, 34] = mint
    return d


def build_adaptive_bg_48():
    """48x48 cave gradient, decorative only (may be cropped by launcher)."""
    rng = random.Random(48)
    d = blank(48, 48)
    navy = ENV["cave_navy"]
    cave = ENV["cave_bg"]
    deep = hex_rgba("#010d14")
    for y in range(48):
        if y < 14:
            d[y, :] = navy
        elif y < 34:
            d[y, :] = cave
        else:
            d[y, :] = deep
    # break the band edges with dither
    for x in range(48):
        if rng.random() < 0.5:
            d[13, x] = cave
        if rng.random() < 0.5:
            d[33, x] = deep
    _speckle(d, rng, (0, 0, 48, 48), [ENV["cave_wall"], navy, deep], 40)
    return d


def generate():
    icon64 = build_app_icon_64()
    save_single(scale(icon64, 16), "app_icon", out_path("Icons", "app_icon_1024.png"))
    save_single(scale(icon64, 8), "playstore_icon", out_path("Icons", "playstore_512.png"))
    fg = build_adaptive_fg_48()
    save_single(scale(fg, 9), "adaptive_foreground", out_path("Icons", "adaptive_foreground_432.png"))
    bg = build_adaptive_bg_48()
    save_single(scale(bg, 9), "adaptive_background", out_path("Icons", "adaptive_background_432.png"))


if __name__ == "__main__":
    generate()
