"""Pixel-art redraw of the McHunter Studios logo for the trailer.

The orange mark is sampled from the original artwork (art/mchunter_logo.webp) on a 22 px grid,
mirrored so it stays perfectly symmetric top to bottom; the blue dot and the small orange dot are
clean pixel circles; the wordmark is hand-drawn (cap height 10, 2 px strokes) and "STUDIOS" is
set in 3x5 caps. Writes art/mchunter_pixel.png (1 px = 1 art pixel) and art/mchunter_pixel_x8.png.

Usage: python Tools/Trailer/studio_logo.py
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "art", "mchunter_logo.webp")

ORANGE = (238, 116, 23)
BLUE = (59, 99, 172)
GRAY = (231, 231, 231)
SUBGRAY = (196, 196, 196)
P = 22.0      # source pixels per art pixel
ROWS = 32
OY = 1.0      # rows cover y 1..705, so the mark's axis (y = 353) falls between rows 15 and 16

WORD = {
    "M": ["###....###", "####..####", "####..####", "##.####.##", "##.####.##",
          "##..##..##", "##..##..##", "##......##", "##......##", "##......##"],
    "c": ["......", "......", ".####.", "##..##", "##....", "##....", "##....", "##....", "##..##", ".####."],
    "H": ["##...##", "##...##", "##...##", "##...##", "#######", "#######", "##...##", "##...##", "##...##", "##...##"],
    "u": ["......", "......", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##", "######", ".#####"],
    "n": ["......", "......", "#####.", "######", "##..##", "##..##", "##..##", "##..##", "##..##", "##..##"],
    "t": [".##..", ".##..", "#####", "#####", ".##..", ".##..", ".##..", ".##..", ".##..", "..###"],
    "e": ["......", "......", ".####.", "##..##", "##..##", "######", "##....", "##....", "##..##", ".####."],
    "r": [".....", ".....", "##.##", "#####", "###..", "##...", "##...", "##...", "##...", "##..."],
}
GAPS = {"M": 1, "c": 1, "H": 2, "u": 1, "n": 1, "t": 1, "e": 1}
TINY = {
    "S": ["###", "#..", "###", "..#", "###"],
    "T": ["###", ".#.", ".#.", ".#.", ".#."],
    "U": ["#.#", "#.#", "#.#", "#.#", "###"],
    "D": ["##.", "#.#", "#.#", "#.#", "##."],
    "I": ["###", ".#.", ".#.", ".#.", "###"],
    "O": ["###", "#.#", "#.#", "#.#", "###"],
}
CIRCLE8 = ["..####..", ".######.", "########", "########", "########", "########", ".######.", "..####.."]
CIRCLE6 = [".####.", "######", "######", "######", "######", ".####."]

# Palette indices of the result.
EMPTY, MARK, DOT, WORDMARK, SUB = 0, 1, 2, 3, 4
COLORS = {MARK: ORANGE, DOT: BLUE, WORDMARK: GRAY, SUB: SUBGRAY}


def masks():
    a = np.array(Image.open(SRC).convert("RGBA")).astype(int)
    rgb, al = a[..., :3], a[..., 3]
    orange = (rgb[..., 0] > 180) & (rgb[..., 1] > 70) & (rgb[..., 1] < 170) & (rgb[..., 2] < 110) & (al > 128)
    blue = (rgb[..., 2] > 120) & (rgb[..., 0] < 120) & (al > 128)
    return orange, blue


def sample(mask, x0, x1):
    m = mask.astype(np.float32)
    m = (m + m[::-1]) / 2
    cols = int(np.ceil((x1 - x0) / P))
    out = np.zeros((ROWS, cols))
    for r in range(ROWS):
        for c in range(cols):
            out[r, c] = m[int(OY + r * P):int(OY + (r + 1) * P), int(x0 + c * P):int(x0 + (c + 1) * P)].mean()
    return out


def stamp(grid, pattern, top, left, value):
    for r, row in enumerate(pattern):
        for c, ch in enumerate(row):
            if ch == "#":
                grid[top + r, left + c] = value


def word(text, glyphs, gaps, height):
    parts = []
    for i, ch in enumerate(text):
        g = glyphs[ch]
        parts.append(np.array([[1 if g[r][c] == "#" else 0 for c in range(len(g[0]))] for r in range(height)], np.uint8))
        if i < len(text) - 1:
            parts.append(np.zeros((height, gaps.get(ch, 1) if isinstance(gaps, dict) else gaps), np.uint8))
    return np.concatenate(parts, axis=1)


def build():
    orange, _ = masks()
    cover = sample(orange, 0, 650)
    mark = np.where(cover >= 0.5, MARK, EMPTY).astype(np.uint8)
    # The blue dot (centre x 396 → between cols 17|18) and the small dot (x 583 → 26|27) become
    # clean pixel circles centred on the mark's axis (between rows 15|16).
    mark[:, 22:] = EMPTY                               # drop the sampled small dot
    mark[mark == DOT] = EMPTY
    stamp(mark, CIRCLE8, 12, 14, DOT)
    stamp(mark, CIRCLE6, 13, 24, MARK)
    w = mark.shape[1]
    while not mark[:, w - 1].any():
        w -= 1
    mark = mark[:, :w]

    wm = word("McHunter", WORD, GAPS, 10)
    sub = word("STUDIOS", TINY, 2, 5)
    x_wm = w + 2
    logo = np.zeros((ROWS, x_wm + wm.shape[1]), np.uint8)
    logo[:, :w] = mark
    top = 9
    logo[top:top + 10, x_wm:x_wm + wm.shape[1]][wm == 1] = WORDMARK
    logo[top + 13:top + 18, x_wm + 1:x_wm + 1 + sub.shape[1]][sub == 1] = SUB
    return logo


def to_rgba(idx):
    rgba = np.zeros(idx.shape + (4,), np.uint8)
    for k, c in COLORS.items():
        rgba[idx == k] = c + (255,)
    return rgba


def main():
    idx = build()
    out = os.path.join(HERE, "art")
    np.save(os.path.join(out, "mchunter_pixel_idx.npy"), idx)
    rgba = to_rgba(idx)
    Image.fromarray(rgba).save(os.path.join(out, "mchunter_pixel.png"))
    Image.fromarray(np.repeat(np.repeat(rgba, 8, 0), 8, 1)).save(os.path.join(out, "mchunter_pixel_x8.png"))
    print("McHunter pixel logo", idx.shape[1], "x", idx.shape[0], "art px")
    for r in range(idx.shape[0]):
        print("".join(".#oHs"[v] for v in idx[r]))


if __name__ == "__main__":
    main()
