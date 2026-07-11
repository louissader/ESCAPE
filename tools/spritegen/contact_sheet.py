"""QA contact sheets (written next to the generator, not into Assets/).

- contact_sheet.png: both heroes' full frame sets at 1x and 4x composited
  over a strip of real grotto tiles.
- contact_sheet_props.png: all props/UI frames at 4x plus both background
  stacks at half scale with the wrap seam shown.
"""
from __future__ import annotations

import os

import numpy as np

from . import characters, palette
from .core import blank, blend, blit, hex_rgba, load_png, out_path, save_png, scale

HERE = os.path.dirname(os.path.abspath(__file__))


def _tile_strip(width_tiles: int, height_tiles: int) -> np.ndarray:
    """Background strip built from real grotto tiles (dark wall + floor row)."""
    tiles = load_png(palette.GROTTO_TILES)

    def tile(tx, ty):
        return tiles[ty * 16 : (ty + 1) * 16, tx * 16 : (tx + 1) * 16]

    wall = tile(1, 1)      # dark interior wall
    floor = tile(1, 0)     # top edge tile (walkable surface)
    img = blank(width_tiles * 16, height_tiles * 16)
    for ty in range(height_tiles):
        for tx in range(width_tiles):
            src = floor if ty == height_tiles - 1 else wall
            blit(img, src, tx * 16, ty * 16)
    return img


def build_hero_sheet() -> np.ndarray:
    ef, en = characters.build_ember()
    wf, wn = characters.build_wisp()
    n = max(len(ef), len(wf))
    cell = 20  # 16px frame + 4px gap at 1x

    # 1x block: two rows of frames over tiles
    strip = _tile_strip(width_tiles=(n * cell + 8) // 16 + 1, height_tiles=3)
    block1 = strip[:, : n * cell + 8].copy()
    for i, fr in enumerate(ef):
        blit(block1, fr, 4 + i * cell, 46 - 24)  # feet on the floor tile row
    strip2 = _tile_strip(width_tiles=(n * cell + 8) // 16 + 1, height_tiles=3)
    block2 = strip2[:, : n * cell + 8].copy()
    for i, fr in enumerate(wf):
        blit(block2, fr, 4 + i * cell, 46 - 24)

    one_x = blank(block1.shape[1], block1.shape[0] * 2 + 4)
    one_x[:, :] = palette.OUTLINE
    blit(one_x, block1, 0, 0)
    blit(one_x, block2, 0, block1.shape[0] + 4)

    four_x = scale(one_x, 4)
    sheet = blank(four_x.shape[1], four_x.shape[0] + one_x.shape[0] + 12)
    sheet[:, :] = hex_rgba("#101014")
    blit(sheet, one_x, 4, 4)
    blit(sheet, four_x, 0, one_x.shape[0] + 12)
    return sheet


def _load_frames(category: str, name: str):
    """Load a generated strip back and split it using its manifest."""
    import json

    png = out_path(category, f"{name}.png")
    with open(os.path.splitext(png)[0] + ".json") as f:
        man = json.load(f)
    img = load_png(png)
    fw, fh = man["frameWidth"], man["frameHeight"]
    return [img[:, i * fw : (i + 1) * fw] for i in range(len(man["frames"]))]


def build_props_sheet() -> np.ndarray:
    rows = []
    for cat, name in [
        ("Props", "lava"),
        ("Props", "bridge"),
        ("Props", "lever"),
        ("Props", "crystal_slab"),
        ("Props", "exit_portal"),
        ("Props", "checkpoint_torch"),
    ]:
        rows.append(_load_frames(cat, name))
    ui = [
        _load_frames("UI", n)[0]
        for n in ["dpad_left", "dpad_right", "dpad_down", "btn_a", "btn_b", "btn_swap", "btn_pause"]
    ]
    rows.append(ui)
    fxs = [
        _load_frames("FX", n)[0]
        for n in ["dust_puff", "spark", "impact_ring", "lava_bubble"]
    ] + _load_frames("FX", "death_poof_strip")
    rows.append(fxs)

    pad = 6
    zoom = 4
    row_imgs = []
    for frames in rows:
        h = max(f.shape[0] for f in frames)
        w = sum(f.shape[1] for f in frames) + pad * (len(frames) + 1)
        row = blank(w, h + 2 * pad)
        row[:, :] = hex_rgba("#0a2735")
        x = pad
        for f in frames:
            blend(row, f, x, pad + (h - f.shape[0]))
            x += f.shape[1] + pad
        row_imgs.append(scale(row, zoom))

    # backgrounds at half res, tiled x2 horizontally to show the wrap seam
    for theme in ["grotto", "depths"]:
        stack = None
        for layer in ["far", "mid", "near"]:
            img = load_png(out_path("Backgrounds", f"{theme}_{layer}.png"))
            half = img[::4, ::4]  # quarter size (was drawn 2x upscaled)
            if stack is None:
                stack = blank(half.shape[1] * 2, half.shape[0])
                stack[:, :] = hex_rgba("#000000")
            blend(stack, half, 0, 0)
            blend(stack, half, half.shape[1], 0)
        row_imgs.append(stack)

    width = max(r.shape[1] for r in row_imgs)
    height = sum(r.shape[0] for r in row_imgs) + pad * (len(row_imgs) + 1)
    sheet = blank(width + 2 * pad, height)
    sheet[:, :] = hex_rgba("#101014")
    y = pad
    for r in row_imgs:
        blit(sheet, r, pad, y)
        y += r.shape[0] + pad
    return sheet


def generate():
    save_png(build_hero_sheet(), os.path.join(HERE, "contact_sheet.png"))
    save_png(build_props_sheet(), os.path.join(HERE, "contact_sheet_props.png"))


if __name__ == "__main__":
    generate()
