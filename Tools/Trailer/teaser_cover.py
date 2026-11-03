"""The Reel cover for the teaser: the logo, REAL FACES. FAKE PAPERS. and Elif's Turkish passport, laid out
inside the profile grid's crops (3:4 = y 240-1680, 4:5 = y 285-1635).

Usage: python Tools/Trailer/teaser_cover.py --capture <folder>/capture  (trailer.ps1 -Script day2jobs)
Writes Marketing/social/out/teaser_reel_cover_1080x1920.png
"""
import argparse
import os

import numpy as np
from PIL import Image

import make_day1 as D
import make_teaser as M
import make_trailer as T


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True)
    args = ap.parse_args()
    fr = np.zeros((1920, 1080, 3), np.uint8)
    fr[:] = M.INK
    logo = D.nearest(np.array(Image.open(os.path.join(args.capture, "art", "logo_dark.png")).convert("RGBA")), 6)
    D.blend(fr, logo, (1080 - logo.shape[1]) // 2, 290)
    # The booklet, open: the navy cover and her page, at 1.5x like the teaser.
    src = next(T.Take(os.path.join(args.capture, "takes"), "elif_print").frames(1300, 1))
    region = src[200:680, 320:1040]
    pic = np.repeat(np.repeat(region, 3, 0), 3, 1)[::2, ::2]
    top = 760
    fr[top - 4:top] = M.EDGE
    fr[top:top + pic.shape[0]] = pic
    fr[top + pic.shape[0]:top + pic.shape[0] + 4] = M.EDGE
    words = M.card_image([[("REAL", M.CREAM), ("FACES.", M.AMBER)], [("FAKE", M.RED), ("PAPERS.", M.CREAM)]], 900)
    D.blend(fr, words, (1080 - words.shape[1]) // 2, top - words.shape[0] // 2 - 20)
    cta = M.card_image([[("FREE", M.CREAM), ("DEMO", M.AMBER), ("OUT NOW", M.CREAM)]], 860)
    D.blend(fr, cta, (1080 - cta.shape[1]) // 2, top + pic.shape[0] + 26)
    out = os.path.join(M.ROOT, "Marketing", "social", "out", "teaser_reel_cover_1080x1920.png")
    Image.fromarray(fr).save(out)
    print(out)


if __name__ == "__main__":
    main()
