"""Hero sprite sheets for ESCAPE: hero1_ember (P1) and hero2_wisp (P2).

Both heroes are 16x24, chibi proportions (head >= 40% of height), 1px
#2a1a22 outline, max 5 palette colors + outline + transparency.

Every frame is composed from a shared base-body template (head / torso /
arms / legs encoded once as paletted grids) plus per-frame offsets, part
variants (legs poses, arm poses) and tiny overlays (eye states, sparkles).
The torso and head grids are never redrawn per frame.
"""
from __future__ import annotations

from . import palette
from .core import blank, blit, grid, hflip, outline, out_path, save_strip

FRAME_W, FRAME_H = 16, 24

# ------------------------------------------------------------------ helpers


def _compose(placements):
    """placements: list of (part_img, x, y). Returns outlined 16x24 frame."""
    canvas = blank(FRAME_W, FRAME_H)
    for img, x, y in placements:
        blit(canvas, img, x, y)
    return outline(canvas, palette.OUTLINE)


# =====================================================================
# HERO 1 — "EMBER" (P1): angular hooded hero, scarf, visible boots.
# =====================================================================

_E = {
    "R": palette.EMBER["primary"],
    "O": palette.EMBER["secondary"],
    "G": palette.EMBER["highlight"],
    "S": palette.EMBER["skin"],
    "D": palette.EMBER["dark"],
    "K": palette.OUTLINE,
}

# Head with bent pointed hood (12x11). Eyes are 1x2 outline-color pixels.
EMBER_HEAD = grid(
    [
        ".......RR...",
        "......RRR...",
        "....RRRRR...",
        "...RRRRRRR..",
        "..RRRRRRRR..",
        "..RRRRRRRR..",
        "..RSSSSSSR..",
        "..RSKSSKSR..",
        "..RSKSSKSR..",
        "...RSSSSR...",
        "....RRRR....",
    ],
    _E,
)
# Head-relative eye anchor (x, y of the eye rows' top-left).
EMBER_EYES = (4, 7)

# Scarf wrap under the chin (8x2).
EMBER_SCARF = grid(
    [
        "OOOOOOOO",
        ".OOOOOO.",
    ],
    _E,
)

# Scarf tail flying off to the side (4x3), animated by offset.
EMBER_SCARF_TAIL = grid(
    [
        "OO..",
        ".OOO",
        "..OO",
    ],
    _E,
)

# Torso: red tunic with gold clasp + belt (8x6).
EMBER_TORSO = grid(
    [
        ".RRRRRR.",
        "RRRGGRRR",
        "RRRRRRRR",
        "RGGGGGGR",
        "RRRRRRRR",
        ".RRRRRR.",
    ],
    _E,
)

# Arms.
EMBER_ARM = grid(["RR", "RR", "RR", "SS"], _E)          # hanging (2x4)
EMBER_ARM_UP = grid(["SS", "RR", "RR", "RR"], _E)        # raised (2x4)
EMBER_ARM_OUT = grid(["RRRRS", "RRRR."], _E)             # extended fwd (5x2)

# Leg poses (boot cuff orange, boots dark).
EMBER_LEGS_STAND = grid(
    [
        ".OO..OO.",
        ".DD..DD.",
        ".DD..DD.",
        ".DD..DD.",
    ],
    _E,
)
EMBER_LEGS_STEP_L = grid(  # right foot lifted
    [
        ".OO..OO.",
        ".DD..DD.",
        ".DD..DD.",
        ".DD.....",
    ],
    _E,
)
EMBER_LEGS_STEP_R = grid(  # left foot lifted
    [
        ".OO..OO.",
        ".DD..DD.",
        ".DD..DD.",
        ".....DD.",
    ],
    _E,
)
EMBER_LEGS_TUCK = grid(  # jump: knees bent
    [
        ".OO..OO.",
        ".DD..DD.",
    ],
    _E,
)
EMBER_LEGS_SPREAD = grid(  # fall: feet apart
    [
        ".OO..OO.",
        ".DD..DD.",
        "DD....DD",
        "DD....DD",
    ],
    _E,
)

# Overlays. Closed eyes: skin over the open eyes, flat dashes below.
EYES_CLOSED_E = grid(["S..S", "K..K"], _E)
MUZZLE_SMALL = grid([".G.", "GGG", ".G."], _E)
MUZZLE_BIG = grid(["G.G.G", ".GGG.", "GGGGG", ".GGG.", "G.G.G"], _E)
EMBER_POOF = grid(
    [
        "...OO...",
        ".OGGGGO.",
        "OGGRRGGO",
        ".OGGGGO.",
        "..O..O..",
    ],
    _E,
)
SPARKLE_G = grid([".G.", "GGG", ".G."], _E)


def _ember_frame(
    head_dy=0,
    torso_dy=0,
    legs=EMBER_LEGS_STAND,
    legs_dy=0,
    arms="side",
    tail_dx=0,
    tail_dy=0,
    show_tail=True,
    eyes="open",
    overlays=(),
    head_dx=0,
    body_dx=0,
):
    """Compose one hero1 frame from the shared parts."""
    hx, hy = 2 + head_dx + body_dx, 2 + head_dy
    tx, ty = 4 + body_dx, 13 + torso_dy
    lx, ly = 4 + body_dx, 19 + legs_dy
    p = []
    if show_tail:
        p.append((EMBER_SCARF_TAIL, tx - 4 + tail_dx, 9 + torso_dy + tail_dy))
    p.append((legs, lx, ly))
    p.append((EMBER_TORSO, tx, ty))
    if arms == "side":
        p.append((EMBER_ARM, tx - 2, ty + 1))
        p.append((EMBER_ARM, tx + 8, ty + 1))
    elif arms == "up":
        p.append((EMBER_ARM_UP, tx - 2, ty - 2))
        p.append((EMBER_ARM_UP, tx + 8, ty - 2))
    elif arms == "out":  # right arm extended for shooting
        p.append((EMBER_ARM, tx - 2, ty + 1))
        p.append((EMBER_ARM_OUT, tx + 7, ty + 1))
    elif arms == "none":
        pass
    p.append((EMBER_SCARF, tx, ty - 1))
    p.append((EMBER_HEAD, hx, hy))
    if eyes == "closed":
        p.append((EYES_CLOSED_E, hx + EMBER_EYES[0], hy + EMBER_EYES[1]))
    for img, x, y in overlays:
        p.append((img, x, y))
    return _compose(p)


def build_ember():
    frames, names = [], []

    def add(name, frame):
        names.append(name)
        frames.append(frame)

    # idle: subtle bob, scarf sway
    add("idle_0", _ember_frame())
    add("idle_1", _ember_frame(head_dy=1, torso_dy=1, tail_dx=-1, tail_dy=1))

    # walk: foot shuffle + bob
    add("walk_0", _ember_frame(legs=EMBER_LEGS_STEP_L, tail_dx=-1))
    add("walk_1", _ember_frame(head_dy=1, torso_dy=1, legs_dy=1, legs=EMBER_LEGS_STAND))
    add("walk_2", _ember_frame(legs=EMBER_LEGS_STEP_R, tail_dx=-1, tail_dy=1))
    add("walk_3", _ember_frame(head_dy=1, torso_dy=1, legs_dy=1, legs=EMBER_LEGS_STAND))

    # jump / fall
    add("jump_0", _ember_frame(head_dy=-1, torso_dy=-1, legs=EMBER_LEGS_TUCK, legs_dy=-1,
                               arms="up", tail_dy=2))
    add("fall_0", _ember_frame(legs=EMBER_LEGS_SPREAD, arms="up", tail_dy=-2))

    # crouch: head sinks over torso
    add("crouch_0", _ember_frame(head_dy=4, torso_dy=2, legs=EMBER_LEGS_TUCK, legs_dy=2,
                                 arms="side", tail_dy=3))

    # hurt: recoil, wince
    add("hurt_0", _ember_frame(head_dx=-1, head_dy=1, torso_dy=1, legs=EMBER_LEGS_SPREAD,
                               arms="up", eyes="closed"))

    # death: crumple then poof
    add("death_0", _ember_frame(head_dx=-1, head_dy=1, torso_dy=1, arms="up", eyes="closed"))
    add("death_1", _ember_frame(head_dy=4, torso_dy=2, legs=EMBER_LEGS_TUCK, legs_dy=2,
                                arms="side", eyes="closed"))
    add("death_2", _ember_frame(head_dy=8, torso_dy=5, legs=EMBER_LEGS_TUCK, legs_dy=2,
                                arms="none", show_tail=False, eyes="closed"))
    death3 = _compose([(EMBER_POOF, 4, 12), (SPARKLE_G, 2, 6), (SPARKLE_G, 11, 9)])
    add("death_3", death3)

    # win: cheer
    add("win_0", _ember_frame(arms="up", tail_dx=-1))
    add("win_1", _ember_frame(head_dy=-1, torso_dy=-1, legs_dy=-1, arms="up", tail_dy=1,
                              overlays=[(SPARKLE_G, 1, 2), (SPARKLE_G, 12, 4)]))

    # shoot (hero1 only)
    add("shoot_0", _ember_frame(arms="out", overlays=[(MUZZLE_SMALL, 13, 14)]))
    add("shoot_1", _ember_frame(body_dx=-1, arms="out", overlays=[(MUZZLE_BIG, 11, 12)]))

    return frames, names


# =====================================================================
# HERO 2 — "WISP" (P2): round cloak, dome hood, trailing wisp motes.
# =====================================================================

_W = {
    "B": palette.WISP["primary"],
    "C": palette.WISP["secondary"],
    "M": palette.WISP["highlight"],
    "S": palette.WISP["skin"],
    "D": palette.WISP["dark"],
    "K": palette.OUTLINE,
}

# Round dome head (12x10) with cyan sheen. Eyes are 1x2 outline pixels.
WISP_HEAD = grid(
    [
        "....BBBB....",
        "..BBCCBBBB..",
        ".BBCBBBBBBB.",
        ".BBBBBBBBBB.",
        ".BBSSSSSSBB.",
        ".BBSKSSKSBB.",
        ".BBSKSSKSBB.",
        ".BBSSSSSSBB.",
        "..BSSSSSSB..",
        "...BBBBBB...",
    ],
    _W,
)
WISP_EYES = (4, 5)  # head-relative eye anchor

# Bell cloak (12x8) with cyan hem.
WISP_CLOAK = grid(
    [
        "...BBBBBB...",
        "...BBMMBB...",
        "..BBBMMBBB..",
        "..BBBBBBBB..",
        ".BBBBBBBBBB.",
        ".BBBBBBBBBB.",
        "BBBBBBBBBBBB",
        "BCCBBBBBBCCB",
    ],
    _W,
)

WISP_FEET = grid([".DD..DD."], _W)
WISP_ARM = grid(["BB", "BB", "BS"], _W)         # side nub (2x3)
WISP_ARM_UP = grid(["BS", "BB", "BB"], _W)      # raised
WISP_MOTE_BIG = grid(["MM", "MM"], _W)
WISP_MOTE = grid(["M"], _W)
EYES_CLOSED_W = grid(["S..S", "K..K"], _W)
WISP_GLOW_RING = grid(
    [
        ".M....M.",
        "M......M",
        ".M....M.",
    ],
    _W,
)
WISP_POOF = grid(
    [
        "...CC...",
        ".CMMMMC.",
        "CMMBBMMC",
        ".CMMMMC.",
        "..C..C..",
    ],
    _W,
)
SPARKLE_M = grid([".M.", "MMM", ".M."], _W)

# Wisp-tail mote offsets per animation tick (deterministic table).
_MOTE_CYCLE = [
    [(1, 9, True), (0, 14, False)],
    [(1, 11, True), (2, 6, False)],
    [(0, 13, True), (1, 8, False)],
    [(2, 7, True), (0, 12, False)],
]


def _wisp_frame(
    head_dy=0,
    cloak_dy=0,
    show_feet=True,
    feet_dy=0,
    arms="none",
    motes=0,
    eyes="open",
    overlays=(),
    head_dx=0,
    body_dx=0,
):
    hx, hy = 2 + head_dx + body_dx, 3 + head_dy
    cx, cy = 2 + body_dx, 12 + cloak_dy
    p = []
    for mx, my, big in _MOTE_CYCLE[motes % len(_MOTE_CYCLE)]:
        p.append((WISP_MOTE_BIG if big else WISP_MOTE, mx, my))
    if show_feet:
        p.append((WISP_FEET, 4 + body_dx, 20 + feet_dy))
    p.append((WISP_CLOAK, cx, cy))
    if arms == "side":
        p.append((WISP_ARM, cx - 1, cy + 2))
        p.append((WISP_ARM, cx + 11, cy + 2))
    elif arms == "up":
        p.append((WISP_ARM_UP, cx - 1, cy - 1))
        p.append((WISP_ARM_UP, cx + 11, cy - 1))
    p.append((WISP_HEAD, hx, hy))
    if eyes == "closed":
        p.append((EYES_CLOSED_W, hx + WISP_EYES[0], hy + WISP_EYES[1]))
    for img, x, y in overlays:
        p.append((img, x, y))
    return _compose(p)


def build_wisp():
    frames, names = [], []

    def add(name, frame):
        names.append(name)
        frames.append(frame)

    # idle: hover bob (feet stay, body floats)
    add("idle_0", _wisp_frame(motes=0))
    add("idle_1", _wisp_frame(head_dy=1, cloak_dy=1, motes=1))

    # walk: glide with mote trail + bob
    add("walk_0", _wisp_frame(motes=0))
    add("walk_1", _wisp_frame(head_dy=1, cloak_dy=1, feet_dy=1, motes=1))
    add("walk_2", _wisp_frame(motes=2))
    add("walk_3", _wisp_frame(head_dy=1, cloak_dy=1, feet_dy=1, motes=3))

    # jump / fall
    add("jump_0", _wisp_frame(head_dy=-1, cloak_dy=-1, show_feet=False, arms="up", motes=1))
    add("fall_0", _wisp_frame(arms="up", motes=2))

    # crouch: dome sinks into cloak
    add("crouch_0", _wisp_frame(head_dy=4, cloak_dy=2, show_feet=False, motes=0))

    # hurt
    add("hurt_0", _wisp_frame(head_dx=-1, head_dy=1, cloak_dy=1, arms="up", motes=3,
                              eyes="closed"))

    # death: sink into cloak then poof
    add("death_0", _wisp_frame(head_dx=-1, head_dy=1, cloak_dy=1, arms="up", eyes="closed"))
    add("death_1", _wisp_frame(head_dy=4, cloak_dy=2, show_feet=False, eyes="closed"))
    add("death_2", _wisp_frame(head_dy=8, cloak_dy=5, show_feet=False, eyes="closed"))
    death3 = _compose([(WISP_POOF, 4, 12), (SPARKLE_M, 2, 6), (SPARKLE_M, 11, 9)])
    add("death_3", death3)

    # win
    add("win_0", _wisp_frame(arms="up", motes=0))
    add("win_1", _wisp_frame(head_dy=-1, cloak_dy=-1, arms="up", motes=1,
                             overlays=[(SPARKLE_M, 1, 2), (SPARKLE_M, 12, 4)]))

    # transform (hero2 only): squash into a glowing slab
    add("transform_0", _wisp_frame(motes=0, overlays=[(SPARKLE_M, 1, 4), (SPARKLE_M, 12, 7)]))
    add("transform_1", _wisp_frame(head_dy=3, cloak_dy=2, show_feet=False, motes=1,
                                   overlays=[(WISP_GLOW_RING, 4, 19)]))
    add("transform_2", _wisp_frame(head_dy=7, cloak_dy=4, show_feet=False, motes=2,
                                   overlays=[(WISP_GLOW_RING, 4, 17)]))
    transform_3 = _compose(
        [
            (WISP_CLOAK, 2, 15),
            (WISP_GLOW_RING, 4, 14),
            (SPARKLE_M, 1, 10),
            (SPARKLE_M, 12, 12),
        ]
    )
    add("transform_3", transform_3)

    return frames, names


# ------------------------------------------------------------------ output


def generate():
    frames, names = build_ember()
    save_strip(frames, names, out_path("Characters", "hero1_ember.png"))
    frames, names = build_wisp()
    save_strip(frames, names, out_path("Characters", "hero2_wisp.png"))


if __name__ == "__main__":
    generate()
