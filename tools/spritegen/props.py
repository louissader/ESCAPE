"""Props and touch-UI sprites for ESCAPE.

All props use environment accents sampled from the Grotto tileset plus the
hero palettes (crystal slab = WISP palette so it reads as P2's platform).
"""
from __future__ import annotations

from . import palette
from .core import (
    blank,
    blit,
    grid,
    hex_rgba,
    outline,
    out_path,
    save_single,
    save_strip,
    set_opacity,
)

K = palette.OUTLINE
ENV = palette.ENV


# ------------------------------------------------------------------ lava

def gen_lava():
    """16x16, 4-frame animated glow ramp. Bright crusting top, dark body."""
    hot = hex_rgba("#ffd23f")            # near-white-hot highlight
    o = ENV["lava_orange"]               # #ff8d07 from the tileset
    deep = hex_rgba("#c23a10")           # deep molten red
    dark = hex_rgba("#7a1a08")           # darkest body
    c = {"H": hot, "O": o, "R": deep, "D": dark}

    # Body rows are static; the surface waves + embedded blobs pulse per frame.
    frames = []
    surface_cycles = [
        "HOHHOOHHHOOHHOHO",
        "OHHOHOHHOHHOHHOH",
        "HHOOHHOHHOHOHHHO",
        "OHOHOHHHOOHHOHHO",
    ]
    blob_rows = [
        [(3, 4, "O"), (10, 6, "O"), (6, 9, "R"), (13, 11, "O"), (2, 12, "R")],
        [(4, 4, "O"), (11, 6, "R"), (5, 9, "O"), (12, 11, "O"), (2, 13, "R")],
        [(3, 5, "O"), (10, 7, "O"), (6, 10, "R"), (13, 12, "O"), (1, 12, "R")],
        [(4, 5, "R"), (11, 7, "O"), (5, 10, "O"), (12, 12, "R"), (2, 11, "O")],
    ]
    for f in range(4):
        img = blank(16, 16)
        for y in range(16):
            if y == 0:
                row = surface_cycles[f]
                for x, ch in enumerate(row):
                    img[y, x] = c[ch]
            elif y == 1:
                img[y, :] = o
            elif y <= 4:
                img[y, :] = deep
            else:
                img[y, :] = dark
        # rising hot blobs
        for x, y, ch in blob_rows[f]:
            img[y, x] = c[ch]
            if x + 1 < 16:
                img[y, x + 1] = c[ch]
        frames.append(img)
    save_strip(frames, [f"lava_{i}" for i in range(4)], out_path("Props", "lava.png"))


# ------------------------------------------------------------------ bridge

def gen_bridge():
    """16x16 stone bridge segment, 3-frame extend animation (left to right)."""
    s = ENV["stone"]
    sl = ENV["stone_light"]
    sd = ENV["stone_dark"]
    edge = ENV["cave_edge"]
    c = {"S": s, "L": sl, "D": sd, "K": K}

    def slab(width):
        img = blank(16, 16)
        top = 5
        seg = grid(
            [
                "L" * width,
                "L" * width,
                "S" * width,
                "S" * width,
                "S" * width,
                "D" * width,
            ],
            c,
        )
        # brick seams (staggered)
        for x in range(3, width, 4):
            seg[2, x] = sd
            seg[3, x] = sd
        for x in range(1, width, 4):
            seg[4, x] = sd
        # underside support nubs
        for x in range(2, width - 1, 5):
            seg[5, x] = sd
        blit(img, seg, 0, top)
        # glowing leading edge while extending (reads as magic masonry)
        if width < 16:
            for y in range(top, top + 6):
                img[y, width - 1] = edge
        return outline(img, K)

    frames = [slab(6), slab(11), slab(16)]
    save_strip(frames, [f"bridge_{i}" for i in range(3)], out_path("Props", "bridge.png"))


# ------------------------------------------------------------------ lever

def gen_lever():
    """16x16 lever: left, tween, tween, right."""
    c = {
        "S": ENV["stone"],
        "L": ENV["stone_light"],
        "D": ENV["stone_dark"],
        "G": hex_rgba("#f4c05a"),  # gold knob (matches EMBER highlight)
        "W": hex_rgba("#7a5a3a"),  # wood arm
        "K": K,
    }
    base = grid(
        [
            "...LLLLLLLL...",
            "..LSSSSSSSSD..",
            "..DSSDSSDSSD..",
            "..DDDDDDDDDD..",
        ],
        c,
    )
    # handle: (tip dx, tip dy) relative to pivot at (7.5, 11); draw as chunky line
    poses = [(-5, -4), (-2, -6), (2, -6), (5, -4)]
    frames = []
    for tipx, tipy in poses:
        img = blank(16, 16)
        pivot = (7, 11)
        # draw handle as 2px-thick stepped line from pivot to tip
        steps = 4
        for i in range(steps + 1):
            x = pivot[0] + round(tipx * i / steps)
            y = pivot[1] + round(tipy * i / steps)
            img[y, x] = c["W"]
            img[y, min(15, x + 1)] = c["W"]
        # knob at tip (2x2 gold)
        kx = pivot[0] + tipx
        ky = pivot[1] + tipy
        for dy in range(2):
            for dx in range(2):
                img[max(0, ky - 1 + dy), max(0, kx + dx)] = c["G"]
        blit(img, base, 1, 11)
        frames.append(outline(img, K))
    save_strip(
        frames,
        ["lever_left", "lever_tween_a", "lever_tween_b", "lever_right"],
        out_path("Props", "lever.png"),
    )


# ------------------------------------------------------------------ crystal slab (P2 platform form)

def gen_crystal_slab():
    """32x16 crystal platform in the WISP palette, 4-frame shimmer.

    Flat walkable top, crystal-point zigzag underside so it reads as a
    slab of fused crystals (P2's platform form), not a pill.
    """
    B = palette.WISP["primary"]
    C = palette.WISP["secondary"]
    M = palette.WISP["highlight"]
    c = {"B": B, "C": C, "M": M, "K": K}
    body = grid(
        [
            "CCCCCCCCCCCCCCCCCCCCCCCCCCCCCC",
            "CBBBBBBCBBBBBBBCBBBBBBBCBBBBBC",
            "BBBBBBBCBBBBBBBCBBBBBBBCBBBBBB",
            "CBBBBBBBBBBBBBBBBBBBBBBBBBBBBC",
            ".BBBBBB..BBBBBB..BBBBBB..BBBB.",
            "..BBBB....BBBB....BBBB....BB..",
            "...CB......CB......CB......C..",
        ],
        c,
    )
    # shimmer: a mint gleam sweeps across the top edge + body sparkles
    gleams = [2, 10, 18, 26]
    sparkles = [
        [(4, 2), (19, 3), (27, 2)],
        [(9, 3), (23, 2), (2, 2)],
        [(13, 2), (28, 3), (6, 3)],
        [(16, 3), (11, 2), (24, 2)],
    ]
    frames = []
    for f in range(4):
        img = blank(32, 16)
        blit(img, body, 1, 5)
        gx = gleams[f]
        for dx in range(4):
            img[5, 1 + (gx + dx) % 30] = c["M"]
        for x, y in sparkles[f]:
            img[5 + y, 1 + x] = c["M"]
        frames.append(outline(img, K))
    save_strip(
        frames, [f"crystal_slab_{i}" for i in range(4)], out_path("Props", "crystal_slab.png")
    )


# ------------------------------------------------------------------ exit portal

def gen_exit_portal():
    """16x32 mystical green/white swirl portal, 4-frame rotation."""
    P = palette.PORTAL
    c = {"G": P["green"], "D": P["green_deep"], "W": P["white"], "V": P["void"], "K": K}
    # Static oval frame (portal rim) with dark void inside.
    rim = grid(
        [
            ".....GGGG.....",
            "...GGDDDDGG...",
            "..GDDVVVVDDG..",
            ".GDVVVVVVVVDG.",
            ".GDVVVVVVVVDG.",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            "GDVVVVVVVVVVDG",
            ".GDVVVVVVVVDG.",
            ".GDVVVVVVVVDG.",
            "..GDDVVVVDDG..",
            "...GGDDDDGG...",
            ".....GGGG.....",
        ],
        c,
    )
    # Swirl: two connected spiral arms rotating through 4 phases.
    import math

    frames = []
    cx, cy = 7.5, 15.5  # center inside the 14x28 rim placed at (1, 2)
    for f in range(4):
        img = blank(16, 32)
        blit(img, rim, 1, 2)
        phase = f * math.pi / 2
        for arm in range(2):
            a0 = phase + arm * math.pi
            for i in range(14):
                t = i / 13.0
                ang = a0 + t * 2.6
                r = 1.5 + t * 8.5
                x = int(round(cx + math.cos(ang) * r * 0.55))
                y = int(round(cy + math.sin(ang) * r))
                if 3 <= x <= 12 and 5 <= y <= 26:
                    ch = "W" if t < 0.35 else "G"
                    img[y, x] = c[ch]
        # center glow pulse
        img[15, 7] = c["W"] if f % 2 == 0 else c["G"]
        img[16, 8] = c["G"] if f % 2 == 0 else c["W"]
        frames.append(outline(img, K))
    save_strip(
        frames, [f"portal_{i}" for i in range(4)], out_path("Props", "exit_portal.png")
    )


# ------------------------------------------------------------------ checkpoint torch

def gen_checkpoint_torch():
    """16x24 torch: 2 unlit frames + 4 lit flame frames."""
    c = {
        "S": ENV["stone"],
        "L": ENV["stone_light"],
        "D": ENV["stone_dark"],
        "W": hex_rgba("#7a5a3a"),
        "V": hex_rgba("#5a4028"),
        "O": ENV["lava_orange"],
        "H": hex_rgba("#ffd23f"),
        "R": hex_rgba("#c23a10"),
        "K": K,
    }
    sconce = grid(
        [
            "..LSSL..",
            "..SDDS..",
            "...WW...",
            "...WV...",
            "...WW...",
            "...VW...",
            "..LSSL..",
            ".LSDDSL.",
        ],
        c,
    )

    def base_img():
        img = blank(16, 24)
        blit(img, sconce, 4, 12)
        return img

    frames = []
    # unlit: cold bowl, tiny ember wink on frame 2
    u0 = base_img()
    frames.append(outline(u0, K))
    u1 = base_img()
    u1[13, 8] = c["R"]
    frames.append(outline(u1, K))
    # lit: 4-frame flame flicker
    flames = [
        [
            "....O...",
            "...HO...",
            "...OHO..",
            "..OHHO..",
            "..OHHHO.",
            "..RHHR..",
        ],
        [
            "...O....",
            "...OH...",
            "..OHHO..",
            "..OHHO..",
            ".OHHHO..",
            "..RHHR..",
        ],
        [
            "....O...",
            "...OHO..",
            "...HHO..",
            "..OHHHO.",
            "..OHHO..",
            "..RHHR..",
        ],
        [
            "...O....",
            "..OHO...",
            "..OHH...",
            ".OHHHO..",
            "..HHHO..",
            "..RHHR..",
        ],
    ]
    for fl in flames:
        img = base_img()
        blit(img, grid(fl, c), 4, 6)
        frames.append(outline(img, K))
    save_strip(
        frames,
        ["unlit_0", "unlit_1", "lit_0", "lit_1", "lit_2", "lit_3"],
        out_path("Props", "checkpoint_torch.png"),
    )


# ------------------------------------------------------------------ touch UI

def _ui_button(glyph_lines, glyph_colors):
    """32x32 chunky button: dark plate, light rim, glyph, at ~70% opacity."""
    plate = hex_rgba("#16323e")
    rim = ENV["cave_wall"]
    img = blank(32, 32)
    # rounded square plate 28x28 at (2,2)
    for y in range(2, 30):
        for x in range(2, 30):
            corner = (x < 5 or x > 26) and (y < 5 or y > 26)
            if corner:
                # cut the very corner pixels for a rounded look
                cx = 4 if x < 5 else 27
                cy = 4 if y < 5 else 27
                if abs(x - cx) + abs(y - cy) > 3:
                    continue
            img[y, x] = plate
    # rim: light top edge, darker bottom
    for x in range(5, 27):
        img[2, x] = rim
        img[29, x] = ENV["cave_navy"]
    for y in range(5, 27):
        img[y, 2] = rim
        img[y, 29] = ENV["cave_navy"]
    g = grid(glyph_lines, glyph_colors)
    gh, gw = g.shape[:2]
    blit(img, g, (32 - gw) // 2, (32 - gh) // 2)
    img = outline(img, K)
    return set_opacity(img, 0.7)


def gen_ui_buttons():
    edge = ENV["cave_edge"]
    gold = hex_rgba("#f4c05a")
    mint = palette.WISP["highlight"]
    white = hex_rgba("#e8f4f8")
    ec = {"E": edge, "G": gold, "M": mint, "W": white}

    arrow_left = [
        ".....EE.......",
        "...EEEE.......",
        ".EEEEEEEEEEEEE",
        "EEEEEEEEEEEEEE",
        ".EEEEEEEEEEEEE",
        "...EEEE.......",
        ".....EE.......",
    ]
    arrow_right = [r[::-1] for r in arrow_left]
    arrow_down = [
        "....EEEE....",
        "....EEEE....",
        "....EEEE....",
        "....EEEE....",
        "....EEEE....",
        "EEEEEEEEEEEE",
        ".EEEEEEEEEE.",
        "...EEEEEE...",
        ".....EE.....",
    ]
    arrow_up_a = [  # jump glyph (gold)
        ".....GG.....",
        "....GGGG....",
        "...GGGGGG...",
        "..GGGGGGGG..",
        ".GGGGGGGGGG.",
        "....GGGG....",
        "....GGGG....",
        "....GGGG....",
    ]
    star_b = [
        ".....WW.....",
        ".....WW.....",
        "....WWWW....",
        "WWWWWWWWWWWW",
        ".WWWWWWWWWW.",
        "...WWWWWW...",
        "..WWW..WWW..",
        ".WW......WW.",
    ]
    swap = [
        "...MM.........",
        "..MMMMMMMMMM..",
        ".MMMMMMMMMMMM.",
        "..MMMMMMMMMM..",
        "...MM.........",
        ".........GG...",
        "..GGGGGGGGGG..",
        ".GGGGGGGGGGGG.",
        "..GGGGGGGGGG..",
        ".........GG...",
    ]
    pause = [
        "WWW..WWW",
        "WWW..WWW",
        "WWW..WWW",
        "WWW..WWW",
        "WWW..WWW",
        "WWW..WWW",
        "WWW..WWW",
        "WWW..WWW",
    ]
    buttons = [
        ("dpad_left", arrow_left),
        ("dpad_right", arrow_right),
        ("dpad_down", arrow_down),
        ("btn_a", arrow_up_a),
        ("btn_b", star_b),
        ("btn_swap", swap),
        ("btn_pause", pause),
    ]
    for name, glyph in buttons:
        img = _ui_button(glyph, ec)
        save_single(img, name, out_path("UI", f"{name}.png"))


# ------------------------------------------------------------------ entry

def generate():
    gen_lava()
    gen_bridge()
    gen_lever()
    gen_crystal_slab()
    gen_exit_portal()
    gen_checkpoint_torch()
    gen_ui_buttons()


if __name__ == "__main__":
    generate()
