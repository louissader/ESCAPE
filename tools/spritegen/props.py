"""Props and touch-UI sprites for ESCAPE.

All props use environment accents sampled from the Grotto tileset plus the
hero palettes (crystal slab = WISP palette so it reads as P2's platform).
"""
from __future__ import annotations

import json
import math
import os

from . import palette
from .core import (
    blank,
    blend,
    blit,
    grid,
    hex_rgba,
    load_png,
    outline,
    out_path,
    save_png,
    save_single,
    save_strip,
    scale,
    set_opacity,
)

K = palette.OUTLINE
ENV = palette.ENV


# ------------------------------------------------------------------ lava

def gen_lava():
    """16x16, 6-frame seamless lava loop. Bright crusting top, dark body.

    Rolling surface waves are sums of integer-harmonic sinusoids of the tile
    width, so the left edge continues the right edge; bubbles ride fixed
    wrapped tracks. Every term is a function of `f mod 6`, so frame 5 flows
    straight back into frame 0.
    """
    hot = hex_rgba("#ffd23f")            # near-white-hot highlight
    o = ENV["lava_orange"]               # #ff8d07 from the tileset
    deep = hex_rgba("#c23a10")           # deep molten red
    dark = hex_rgba("#7a1a08")           # darkest body

    n = 6
    # rising ember bubbles: (column, phase offset into the 6-frame rise cycle)
    tracks = [(2, 0), (7, 2), (12, 4), (5, 3), (14, 1)]
    frames = []
    for f in range(n):
        t = 2 * math.pi * f / n
        img = blank(16, 16)
        for x in range(16):
            w1 = math.sin(2 * math.pi * x / 16 + t)
            w2 = math.sin(2 * math.pi * 2 * x / 16 - t)
            crust = 1.0 + 0.9 * w1 + 0.5 * w2                     # hot crust
            band = crust + 2.2 + 0.8 * math.sin(2 * math.pi * x / 16 + t + 1.7)
            glow = band + 4.0 + 1.2 * math.sin(2 * math.pi * 2 * x / 16 + t + 0.6)
            for y in range(16):
                if y < crust:
                    img[y, x] = hot if w1 + 0.5 * w2 > 0.55 else o
                elif y < band:
                    img[y, x] = o
                elif y < glow:
                    img[y, x] = deep
                else:
                    img[y, x] = dark
        # bubbles rise 13 -> 3 over the cycle, drifting one column per frame
        for x0, off in tracks:
            ph = (f + off) % n
            y = 13 - 2 * ph
            bx = (x0 + ph) % 16
            col = deep if y >= 11 else (o if y >= 7 else hot)
            img[y, bx] = col
            img[y, (bx + 1) % 16] = col
            if ph == 5:  # bubble bursts at the crust: occasional bright fleck
                img[y - 1, bx] = hot
        frames.append(img)
    save_strip(frames, [f"lava_{i}" for i in range(n)], out_path("Props", "lava.png"))


# ------------------------------------------------------------------ bridge

def gen_bridge():
    """16x16 stone bridge segment, 6-frame 'magically forming' extend animation.

    Frame 0 is nearly empty (two anchor foundation stubs); frames 1-5 grow a
    chunky, two-course masonry deck left-to-right, with a glowing warm leading
    edge that reads as the stone materialising. The final frame is a solid,
    seamless walkway (the deck fills columns 0..15 so a row of these tiles into
    one continuous bridge). Underside carries a 2px cast shadow.
    """
    S = ENV["stone"]           # (48,83,78)  main block body
    L = ENV["stone_light"]     # (74,132,124) top-lit face
    D = ENV["stone_dark"]      # (36,48,46)  mortar seams / recesses
    W = hex_rgba("#e07438")    # ember accent — bright warm forming glow
    Wd = hex_rgba("#9a6e46")   # dim warm — settled warm top edge
    Sh = hex_rgba("#141e1c")   # underside cast shadow (darker than stone_dark)

    def make_deck():
        """The full, final masonry deck (columns 0..15, tiles horizontally)."""
        deck = blank(16, 16)
        for x in range(16):
            deck[3, x] = W if x % 2 == 0 else Wd          # warm glowing top edge
            deck[4, x] = L                                 # lit walking surface
            deck[5, x] = D if x % 4 == 2 else S            # upper course
            deck[6, x] = D if x % 4 == 2 else S            #  seams @ x=2,6,10,14
            deck[7, x] = D                                 # horizontal mortar line
            deck[8, x] = D if x % 4 == 0 else S            # lower course (offset)
            deck[9, x] = D if x % 4 == 0 else S            #  seams @ x=0,4,8,12
            deck[10, x] = S                                # solid base row
            deck[11, x] = Sh                               # cast shadow 1
            deck[12, x] = Sh                               # cast shadow 2
        return deck

    deck = make_deck()
    reveal = [0, 4, 7, 10, 13, 16]   # columns materialised per frame

    def stub(img, x0):
        """A rough foundation block (no lit top) at columns x0, x0+1."""
        for x in (x0, x0 + 1):
            for y in range(6, 13):
                img[y, x] = deck[y, x]

    frames = []
    for f in range(6):
        img = blank(16, 16)
        r = reveal[f]
        prev = reveal[f - 1] if f > 0 else 0
        if r > 0:
            img[:, 0:r] = deck[:, 0:r]
        # far anchor: destination foundation, until the deck reaches it
        if r < 14:
            stub(img, 14)
        # near anchor seed on the empty first frame
        if f == 0:
            stub(img, 0)
        # magic: the freshly-formed frontier glows bright warm
        if 0 < f < 5 and r > 0:
            for x in range(prev, r):
                img[3, x] = W
            lead = r - 1
            for y in range(3, 8):
                img[y, lead] = W
        frames.append(outline(img, K))

    save_strip(
        frames, [f"extend_{i}" for i in range(6)], out_path("Props", "bridge.png")
    )


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


# ------------------------------------------------------------------ per-hero exit doors

def gen_door(hero):
    """16x32 stone-arch cave door (1x2 tiles) for one hero, 7 frames.

    closed_0          dark sealed slab inside the arch
    half_0 / half_1   soft 2-frame glow pulse in the hero's palette
    open_0..open_3    glow burst -> slab dissolves -> settled open portal
    """
    pal = palette.EMBER if hero == "ember" else palette.WISP
    white = hex_rgba("#fff0c8" if hero == "ember" else "#dffcf0")
    seal = hex_rgba("#1b1016")     # sealed slab
    seam = hex_rgba("#120a10")     # slab seams
    c = {
        "S": ENV["stone"],
        "L": ENV["stone_light"],
        "D": ENV["stone_dark"],
        "I": seal,
    }
    # 14x30 arch placed at (1,1) so the outline keeps its 1px margin.
    rows = (
        [
            "....LLLLLL....",
            "..LLSSSSSSLL..",
            ".LSSSSSSSSSSL.",
            ".LSSIIIIIISSL.",
            "LSSIIIIIIIISSL",
        ]
        + ["LSIIIIIIIIIISL"] * 23
        + [
            "LDDDDDDDDDDDDL",
            ".LLLLLLLLLLLL.",
        ]
    )
    # mortar seams up the jambs so the arch reads as coursed masonry
    rows = [list(r) for r in rows]
    for r in range(7, 27, 5):
        rows[r][1] = "D"
        rows[r][12] = "D"
    rows = ["".join(r) for r in rows]
    arch = grid(rows, c)
    inner = [(x, y) for y, row in enumerate(rows) for x, ch in enumerate(row) if ch == "I"]
    inner_set = set(inner)

    # interior distance field, squashed vertically so bands read as an oval
    def dist(gx, gy):
        return math.hypot(gx - 6.5, (gy - 14.5) * 0.55)

    def base():
        img = blank(16, 32)
        blit(img, arch, 1, 1)
        return img

    def sealed():
        """Sealed slab: center split + block seams behind everything."""
        img = base()
        for gx, gy in inner:
            if gx == 7 or gy in (9, 15, 21):
                img[gy + 1, gx + 1] = seam
        return img

    def bands(img, stops):
        """Fill the interior by distance: stops = [(radius, color), ...]."""
        for gx, gy in inner:
            d = dist(gx, gy)
            for r, col in stops:
                if d <= r:
                    img[gy + 1, gx + 1] = col
                    break

    def glow(img, r_core, r_halo, f):
        """Soft dithered glow over the sealed slab (the half-lit pulse)."""
        for gx, gy in inner:
            d = dist(gx, gy)
            if d <= r_core:
                img[gy + 1, gx + 1] = pal["secondary"]
            elif d <= r_halo and (gx + gy + f) % 2 == 0:
                img[gy + 1, gx + 1] = pal["primary"]

    def swirl(img, phase, col_in, col_out):
        """Two spiral arms inside the open portal (as in the exit portal)."""
        for arm in range(2):
            a0 = phase + arm * math.pi
            for i in range(10):
                t = i / 9.0
                ang = a0 + t * 2.4
                r = 1.0 + t * 5.0
                gx = int(round(6.5 + math.cos(ang) * r))
                gy = int(round(14.5 + math.sin(ang) * r / 0.55))
                if (gx, gy) in inner_set:
                    img[gy + 1, gx + 1] = col_in if t < 0.4 else col_out

    frames = []
    # closed: dark sealed arch
    frames.append(sealed())
    # half-lit: 2-frame soft pulse in the hero's colors
    for f in range(2):
        img = sealed()
        glow(img, 1.0 + 0.8 * f, 3.4 + 1.4 * f, f)
        if f == 1:
            img[15, 8] = pal["highlight"]
        frames.append(img)
    # open_0: the glow bursts and floods the doorway
    img = base()
    bands(img, [(2.5, white), (5.0, pal["highlight"]), (99, pal["secondary"])])
    frames.append(img)
    # open_1: slab dissolving into portal energy
    img = base()
    bands(img, [(1.8, white), (4.5, pal["secondary"]), (99, pal["primary"])])
    swirl(img, 0.0, white, pal["highlight"])
    frames.append(img)
    # open_2 / open_3: settled open portal, swirl rotates between the two
    for f in range(2):
        img = base()
        bands(
            img,
            [(1.2, white), (3.0, pal["highlight"]), (5.5, pal["secondary"]), (99, pal["primary"])],
        )
        swirl(img, math.pi / 2 + f * math.pi, white, pal["highlight"])
        frames.append(img)

    save_strip(
        [outline(fr, K) for fr in frames],
        ["closed_0", "half_0", "half_1", "open_0", "open_1", "open_2", "open_3"],
        out_path("Props", f"door_{hero}.png"),
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


# ------------------------------------------------------------------ powerup orbs

def _orb_frames(pal, white_hex):
    """4-frame pulsing power-up orb (32x32) in a hero palette.

    Deep `primary` core, `secondary` mid body, a rotating two-arm `highlight`
    energy swirl, near-white specular, 1px dark outline. The pulse cycles core
    brightness (a growing/shrinking bloom) and a soft dithered halo. Drawn at
    32x32 so the orb stays crisp when scaled to ~1.8 tiles in-game.
    """
    core = pal["primary"]
    mid = pal["secondary"]
    hi = pal["highlight"]
    Kp = pal["outline"]
    white = hex_rgba(white_hex)

    cx, cy = 15.5, 15.5
    R = 11.0
    bloom_r = [0.0, 2.6, 4.4, 2.6]     # brightness pulse (trough -> peak -> back)
    halo_r = [11.6, 13.0, 14.4, 13.0]  # aura ring grows then shrinks

    frames = []
    for f in range(4):
        img = blank(32, 32)
        # solid orb: deep inner core -> bright mid body
        for y in range(32):
            for x in range(32):
                d = math.hypot(x - cx, y - cy)
                if d <= R:
                    col = core if d <= 4.6 else mid
                    if d <= bloom_r[f]:
                        col = hi          # brightening core bloom at the peak
                    img[y, x] = col
        # rotating two-arm energy swirl inside the body
        phase = f * math.pi / 2
        for arm in range(2):
            a0 = phase + arm * math.pi
            for i in range(12):
                t = i / 11.0
                ang = a0 + t * 2.6
                r = 3.2 + t * 6.4
                col = white if t < 0.25 else hi
                x = int(round(cx + math.cos(ang) * r))
                y = int(round(cy + math.sin(ang) * r))
                for dx in (0, 1):  # 2px-thick so the swirl survives scaling
                    if math.hypot(x + dx - cx, y - cy) <= R - 0.5:
                        img[y, x + dx] = col
        # upper-left specular highlight (reads as a glassy gem)
        img[10, 12] = white
        img[10, 13] = white
        img[11, 12] = white
        img[11, 13] = hi
        img[11, 11] = hi
        img[12, 11] = hi
        img[10, 14] = hi
        img = outline(img, Kp)
        # soft halo AFTER outlining so the aura stays a 1px dithered glow
        hr = halo_r[f]
        for y in range(32):
            for x in range(32):
                d = math.hypot(x - cx, y - cy)
                if hr - 0.6 <= d <= hr + 0.6 and (x + y + f) % 2 == 0:
                    if img[y, x, 3] == 0:
                        img[y, x] = hi
        frames.append(img)
    return frames


def gen_powerups():
    """Blue (WISP / P2) and red (EMBER / P1) pulsing power-up orbs."""
    wisp = _orb_frames(palette.WISP, "#dffcf0")
    save_strip(
        wisp, [f"pulse_{i}" for i in range(4)], out_path("Props", "powerup_wisp.png")
    )
    ember = _orb_frames(palette.EMBER, "#fff0c8")
    save_strip(
        ember, [f"pulse_{i}" for i in range(4)], out_path("Props", "powerup_ember.png")
    )


# ------------------------------------------------------------------ QA review sheet

def _load_frames(category, name):
    """Read a generated strip back and split it using its JSON manifest."""
    png = out_path(category, f"{name}.png")
    with open(os.path.splitext(png)[0] + ".json") as fh:
        man = json.load(fh)
    img = load_png(png)
    fw = man["frameWidth"]
    return [img[:, i * fw : (i + 1) * fw].copy() for i in range(len(man["frames"]))]


def gen_props_review():
    """QA contact sheet for the new bridge + powerups (next to the generator).

    Shows: the bridge build sequence, six copies of the finished bridge tiled
    into one continuous span, and both powerup orbs — each at 1x and 6x over a
    dark cave background. Not written into Assets/.
    """
    bg = hex_rgba("#0b2230")
    outer = hex_rgba("#101014")
    bridge = _load_frames("Props", "bridge")
    wisp = _load_frames("Props", "powerup_wisp")
    ember = _load_frames("Props", "powerup_ember")

    def lay(frames, gap):
        fh, fw = frames[0].shape[:2]
        n = len(frames)
        row = blank(n * fw + gap * (n + 1), fh + 2 * gap)
        row[:, :] = bg
        x = gap
        for fr in frames:
            blend(row, fr, x, gap)
            x += fw + gap
        return row

    def tile(frame, count):
        fh, fw = frame.shape[:2]
        row = blank(fw * count, fh)
        row[:, :] = bg
        for i in range(count):
            blend(row, frame, i * fw, 0)
        return row

    rows = [
        scale(lay(bridge, 1), 1),          # bridge build sequence, 1x
        scale(lay(bridge, 1), 6),          # bridge build sequence, 6x
        scale(tile(bridge[-1], 6), 1),     # finished bridge tiled x6, 1x (seam check)
        scale(tile(bridge[-1], 6), 6),     # finished bridge tiled x6, 6x
        scale(lay(wisp + ember, 1), 1),    # both orbs, 1x
        scale(lay(wisp + ember, 1), 6),    # both orbs, 6x
    ]

    pad = 8
    width = max(r.shape[1] for r in rows) + 2 * pad
    height = sum(r.shape[0] for r in rows) + pad * (len(rows) + 1)
    sheet = blank(width, height)
    sheet[:, :] = outer
    y = pad
    for r in rows:
        blit(sheet, r, pad, y)
        y += r.shape[0] + pad
    save_png(sheet, os.path.join(os.path.dirname(__file__), "props_review.png"))


# ------------------------------------------------------------------ entry

def generate():
    gen_lava()
    gen_bridge()
    gen_lever()
    gen_crystal_slab()
    gen_exit_portal()
    gen_door("ember")
    gen_door("wisp")
    gen_checkpoint_torch()
    gen_ui_buttons()
    gen_star_coin()
    gen_powerups()
    gen_props_review()


if __name__ == "__main__":
    generate()


# ------------------------------------------------------------------ star coin

def _star_points(cx, cy, r_outer, r_inner):
    """Integer pixel set for a 5-point star (for 16x16 pixel art)."""
    pts = []
    for i in range(10):
        ang = -math.pi / 2 + i * math.pi / 5
        r = r_outer if i % 2 == 0 else r_inner
        pts.append((cx + r * math.cos(ang), cy + r * math.sin(ang)))
    # rasterize by point-in-polygon over the tile
    inside = set()
    for py in range(16):
        for px in range(16):
            x, y = px + 0.5, py + 0.5
            hit = False
            j = len(pts) - 1
            for i in range(len(pts)):
                xi, yi = pts[i]
                xj, yj = pts[j]
                if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi + 1e-9) + xi:
                    hit = not hit
                j = i
            if hit:
                inside.add((px, py))
    return inside

def gen_star_coin():
    """16x16 Mario-style level star: star.png = filled gold star, 4-frame
    twinkle; star_empty.png = hollow outline star (uncollected state).
    One star per level — the level-select icon transitions empty -> filled."""
    gold = hex_rgba("#f4c05a")
    gold_dark = hex_rgba("#c98a2e")
    gold_hot = hex_rgba("#fff3c4")
    grey = hex_rgba("#9aa7ad")

    star_px = _star_points(8, 8, 7, 3)
    inner_px = _star_points(8, 8, 5, 2)

    frames = []
    shine_spots = [(6, 4), (10, 5), (5, 9), (9, 10)]
    for f in range(4):
        img = blank(16, 16)
        for (px, py) in star_px:
            img[py, px] = gold_dark
        for (px, py) in inner_px:
            img[py, px] = gold
        # roving twinkle highlight
        sx, sy = shine_spots[f]
        for (px, py) in [(sx, sy), (sx + 1, sy), (sx, sy + 1)]:
            if (px, py) in star_px:
                img[py, px] = gold_hot
        frames.append(outline(img, K))
    save_strip(frames, [f"twinkle_{i}" for i in range(4)], out_path("Props", "star.png"))

    # Empty star: outline ring of the same silhouette, hollow center.
    img = blank(16, 16)
    for (px, py) in star_px:
        if (px, py) not in inner_px:
            img[py, px] = grey
    save_single(outline(img, K), "empty_0", out_path("Props", "star_empty.png"))
