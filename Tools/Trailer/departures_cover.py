"""The Reel cover for the Departures short: the logo, the full departures board and a client's passport photo,
laid out inside the profile grid's crops (3:4 = y 240-1680, 4:5 = y 285-1635).

Usage: python Tools/Trailer/departures_cover.py --capture <folder>/capture
Writes Marketing/social/out/departures_reel_cover_1080x1920.png
"""
import argparse
import os

import numpy as np
from PIL import Image

import make_day1 as D
import make_departures as M
import make_trailer as T


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True)
    args = ap.parse_args()
    fr = np.zeros((1920, 1080, 3), np.uint8)
    fr[:] = M.INK
    logo = D.nearest(np.array(Image.open(os.path.join(args.capture, "art", "logo_dark.png")).convert("RGBA")), 5)
    D.blend(fr, logo, (1080 - logo.shape[1]) // 2, 290)
    # The board as it stands after the last client: every row settled.
    board = M.Board("vertical")
    for k, (b, dest, remark, color, clock) in enumerate(M.ROWS):
        board.set_text(k, dest.ljust(7) + " " + remark, M.CREAM, 0)
        for c in range(8, board.lines[k][2]):
            t0, ch, _ = board.cells[(k, c)][-1]
            board.cells[(k, c)][-1] = (t0, ch, color)
    board.set_clock(M.ROWS[-1][4], 0)
    shift = 380                      # the board sits under the logo
    canvas = np.zeros((1920, 1080, 3), np.uint8)
    board.draw(canvas, 10 ** 6)
    x0, y0, x1, y1 = board.box
    fr[y0 + shift:y1 + shift, x0:x1] = canvas[y0:y1, x0:x1]
    # Viktor in the viewfinder, at 1.5x like the short.
    take = T.Take(os.path.join(args.capture, "takes"), "viktor_photo")
    src = next(take.frames(270, 1))
    top = y1 + shift + 24
    h = 1560 - top
    rows = h * 2 // 3
    region = src[230:230 + rows, 120:840]
    pic = np.repeat(np.repeat(region, 3, 0), 3, 1)[::2, ::2]
    fr[top - 4:top] = M.EDGE
    fr[top:top + pic.shape[0]] = pic[:, :1080]
    fr[top + pic.shape[0]:top + pic.shape[0] + 4] = M.EDGE
    cta = D.text_image("FREE DEMO OUT NOW ON STEAM", 48, M.AMBER)
    D.blend(fr, cta, (1080 - cta.shape[1]) // 2, top + pic.shape[0] + 18)
    out = os.path.join(M.ROOT, "Marketing", "social", "out", "departures_reel_cover_1080x1920.png")
    Image.fromarray(fr).save(out)
    print(out)


if __name__ == "__main__":
    main()
