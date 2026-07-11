"""Particle / FX sprites for ESCAPE."""
from __future__ import annotations

from . import palette
from .core import grid, hex_rgba, out_path, save_single, save_strip

K = palette.OUTLINE

_C = {
    "W": hex_rgba("#e8f4f8"),   # near-white
    "L": hex_rgba("#9fb8c4"),   # light gray-blue dust
    "D": hex_rgba("#5a707c"),   # dark dust
    "G": hex_rgba("#f4c05a"),   # gold spark
    "O": palette.ENV["lava_orange"],
    "R": hex_rgba("#c23a10"),
    "M": palette.WISP["highlight"],
    "K": K,
}


def gen_dust_puff():
    img = grid(
        [
            "..LL....",
            ".LWWL...",
            "LWWLWL..",
            ".LLWWLL.",
            "..LDLL..",
            "...L....",
            "........",
            "........",
        ],
        _C,
    )
    save_single(img, "dust_puff", out_path("FX", "dust_puff.png"))


def gen_spark():
    img = grid(
        [
            "...G....",
            "...G....",
            ".GGWGG..",
            "..GWG...",
            ".GGWGG..",
            "...G....",
            "...G....",
            "........",
        ],
        _C,
    )
    save_single(img, "spark", out_path("FX", "spark.png"))


def gen_impact_ring():
    img = grid(
        [
            "................",
            "......WWWW......",
            "....WW....WW....",
            "...W........W...",
            "..W..........W..",
            "..W..........W..",
            ".W............W.",
            ".W............W.",
            ".W............W.",
            ".W............W.",
            "..W..........W..",
            "..W..........W..",
            "...W........W...",
            "....WW....WW....",
            "......WWWW......",
            "................",
        ],
        _C,
    )
    save_single(img, "impact_ring", out_path("FX", "impact_ring.png"))


_POOF_FRAMES = [
    [
        "................",
        "................",
        "................",
        "......LL........",
        ".....LWWL.......",
        "....LWWWWL......",
        "....LWWWWL......",
        ".....LWWL.......",
        "......LL........",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
    ],
    [
        "................",
        "................",
        "....LLLL........",
        "...LWWWWL.......",
        "..LWWLLWWL......",
        "..LWLLLLWL......",
        "..LWWLLWWL......",
        "...LWWWWL.......",
        "....LLLL........",
        "......L.........",
        "................",
        "................",
        "................",
        "................",
        "................",
        "................",
    ],
    [
        "................",
        "...LL....LL.....",
        "..LWWL..LWWL....",
        "..LWL....LWL....",
        "...L......L.....",
        "................",
        "..L........L....",
        ".LWL......LWL...",
        "..L........L....",
        "...LL....LL.....",
        "..LWL....LWL....",
        "...L......L.....",
        "................",
        "................",
        "................",
        "................",
    ],
    [
        "................",
        "..L..........L..",
        ".LL....LL....LL.",
        "........L.......",
        "................",
        ".L............L.",
        "................",
        "......L..L......",
        "................",
        "................",
        "..L........L....",
        "................",
        "................",
        "................",
        "................",
        "................",
    ],
]


def gen_death_poof():
    frames = [grid(f, _C) for f in _POOF_FRAMES]
    # single-sprite version = the fullest frame
    save_single(frames[1], "death_poof", out_path("FX", "death_poof.png"))
    save_strip(
        frames, [f"death_poof_{i}" for i in range(4)], out_path("FX", "death_poof_strip.png")
    )


def gen_lava_bubble():
    img = grid(
        [
            "...OO...",
            "..OGGO..",
            ".OGWGGO.",
            ".OGGGGO.",
            ".ROGGOR.",
            "..ROOR..",
            "...RR...",
            "........",
        ],
        _C,
    )
    save_single(img, "lava_bubble", out_path("FX", "lava_bubble.png"))


def generate():
    gen_dust_puff()
    gen_spark()
    gen_impact_ring()
    gen_death_poof()
    gen_lava_bubble()


if __name__ == "__main__":
    generate()
