"""Core helpers for the ESCAPE sprite pipeline.

Everything is deterministic: no wall-clock, no unseeded randomness.
Images are RGBA numpy arrays (H, W, 4) uint8. Transparent = alpha 0.
"""
from __future__ import annotations

import json
import os

import numpy as np
from PIL import Image

REPO_ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT_ROOT = os.path.join(REPO_ROOT, "Assets", "Art", "Generated")


# ---------------------------------------------------------------- colors

def hex_rgba(h: str, a: int = 255) -> tuple:
    h = h.lstrip("#")
    return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)


# ---------------------------------------------------------------- canvases

def blank(w: int, h: int) -> np.ndarray:
    return np.zeros((h, w, 4), dtype=np.uint8)


def grid(lines, colors) -> np.ndarray:
    """Build a small RGBA image from ASCII art.

    lines: list of strings, one per row. '.' and ' ' are transparent.
    colors: dict mapping char -> RGBA tuple.
    """
    h = len(lines)
    w = max(len(line) for line in lines)
    img = blank(w, h)
    for y, line in enumerate(lines):
        for x, ch in enumerate(line):
            if ch in (".", " "):
                continue
            img[y, x] = colors[ch]
    return img


def blit(dst: np.ndarray, src: np.ndarray, x: int, y: int) -> None:
    """1-bit-alpha blit: src pixels with alpha > 0 overwrite dst. Clips at edges."""
    sh, sw = src.shape[:2]
    dh, dw = dst.shape[:2]
    sx0 = max(0, -x)
    sy0 = max(0, -y)
    sx1 = min(sw, dw - x)
    sy1 = min(sh, dh - y)
    if sx1 <= sx0 or sy1 <= sy0:
        return
    sub = src[sy0:sy1, sx0:sx1]
    mask = sub[:, :, 3] > 0
    region = dst[y + sy0 : y + sy1, x + sx0 : x + sx1]
    region[mask] = sub[mask]


def blend(dst: np.ndarray, src: np.ndarray, x: int, y: int) -> None:
    """Proper alpha-over blit (for semi-transparent layers)."""
    sh, sw = src.shape[:2]
    dh, dw = dst.shape[:2]
    sx0 = max(0, -x)
    sy0 = max(0, -y)
    sx1 = min(sw, dw - x)
    sy1 = min(sh, dh - y)
    if sx1 <= sx0 or sy1 <= sy0:
        return
    sub = src[sy0:sy1, sx0:sx1].astype(np.float32) / 255.0
    region = dst[y + sy0 : y + sy1, x + sx0 : x + sx1].astype(np.float32) / 255.0
    sa = sub[:, :, 3:4]
    da = region[:, :, 3:4]
    oa = sa + da * (1 - sa)
    safe = np.where(oa == 0, 1, oa)
    rgb = (sub[:, :, :3] * sa + region[:, :, :3] * da * (1 - sa)) / safe
    out = np.concatenate([rgb, oa], axis=2)
    dst[y + sy0 : y + sy1, x + sx0 : x + sx1] = np.round(out * 255).astype(np.uint8)


# ---------------------------------------------------------------- transforms

def hflip(img: np.ndarray) -> np.ndarray:
    return img[:, ::-1].copy()


def vflip(img: np.ndarray) -> np.ndarray:
    return img[::-1, :].copy()


def rot90cw(img: np.ndarray, k: int = 1) -> np.ndarray:
    return np.rot90(img, -k).copy()


def scale(img: np.ndarray, f: int) -> np.ndarray:
    return np.repeat(np.repeat(img, f, axis=0), f, axis=1)


def outline(img: np.ndarray, color: tuple, connectivity: int = 8) -> np.ndarray:
    """Add a 1px outline: transparent pixels adjacent to opaque become `color`.

    Grows the silhouette by 1px, so art must keep a 1px margin in its canvas.
    """
    a = img[:, :, 3] > 0
    grown = np.zeros_like(a)
    shifts4 = [(-1, 0), (1, 0), (0, -1), (0, 1)]
    shifts8 = shifts4 + [(-1, -1), (-1, 1), (1, -1), (1, 1)]
    for dy, dx in (shifts8 if connectivity == 8 else shifts4):
        shifted = np.zeros_like(a)
        ys = slice(max(0, dy), a.shape[0] + min(0, dy))
        yd = slice(max(0, -dy), a.shape[0] + min(0, -dy))
        xs = slice(max(0, dx), a.shape[1] + min(0, dx))
        xd = slice(max(0, -dx), a.shape[1] + min(0, -dx))
        shifted[yd, xd] = a[ys, xs]
        grown |= shifted
    rim = grown & ~a
    out = img.copy()
    out[rim] = color
    return out


def set_opacity(img: np.ndarray, alpha: float) -> np.ndarray:
    """Multiply the alpha channel by `alpha` (0..1)."""
    out = img.copy()
    out[:, :, 3] = np.round(out[:, :, 3].astype(np.float32) * alpha).astype(np.uint8)
    return out


# ---------------------------------------------------------------- output

def _ensure_dir(path: str) -> None:
    os.makedirs(os.path.dirname(path), exist_ok=True)


def save_png(img: np.ndarray, path: str) -> None:
    _ensure_dir(path)
    Image.fromarray(img, "RGBA").save(path, optimize=True)


def write_manifest(path_png: str, frame_w: int, frame_h: int, names) -> None:
    manifest = {
        "frameWidth": frame_w,
        "frameHeight": frame_h,
        "frames": [{"name": n, "index": i} for i, n in enumerate(names)],
    }
    path_json = os.path.splitext(path_png)[0] + ".json"
    _ensure_dir(path_json)
    with open(path_json, "w") as f:
        json.dump(manifest, f, indent=2)
        f.write("\n")


def save_strip(frames, names, path_png: str) -> None:
    """Save a horizontal strip PNG plus its JSON manifest."""
    assert len(frames) == len(names), "frame/name count mismatch"
    fh, fw = frames[0].shape[:2]
    for fr in frames:
        assert fr.shape[:2] == (fh, fw), "all frames must share dimensions"
    strip = blank(fw * len(frames), fh)
    for i, fr in enumerate(frames):
        strip[:, i * fw : (i + 1) * fw] = fr
    save_png(strip, path_png)
    write_manifest(path_png, fw, fh, names)


def save_single(img: np.ndarray, name: str, path_png: str) -> None:
    """Save a single-frame sprite with a 1-frame manifest (uniform loader)."""
    h, w = img.shape[:2]
    save_png(img, path_png)
    write_manifest(path_png, w, h, [name])


def out_path(category: str, filename: str) -> str:
    return os.path.join(OUT_ROOT, category, filename)


def load_png(path: str) -> np.ndarray:
    return np.array(Image.open(path).convert("RGBA"))
