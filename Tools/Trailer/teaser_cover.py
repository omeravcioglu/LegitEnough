"""The teaser's covers, both built around Elif's Turkish passport:
- the Reel cover (1080x1920): the logo, REAL FACES. FAKE PAPERS. and the passport, laid out inside the profile
  grid's crops (3:4 = y 240-1680, 4:5 = y 285-1635);
- the thumbnail (1920x1080, YouTube and other horizontal posts): the open passport on the left, the logo and
  FREE DEMO OUT NOW on the right, clear of the duration badge in the bottom right corner.

Usage: python Tools/Trailer/teaser_cover.py --capture <folder>/capture  (trailer.ps1 -Script day2jobs)
Writes Marketing/social/out/teaser_reel_cover_1080x1920.png and teaser_thumbnail_1920x1080.png
"""
import argparse
import os

import numpy as np
from PIL import Image

import make_day1 as D
import make_teaser as M
import make_trailer as T

OUT = os.path.join(M.ROOT, "Marketing", "social", "out")


def booklet(capture):
    """Frame 1300 of elif_print: her bound passport, open on the navy cover and her page."""
    return next(T.Take(os.path.join(capture, "takes"), "elif_print").frames(1300, 1))


def x15(region):
    """1.5x, nearest, like the teaser: the art's 4 px grid becomes 6 px and the UI's 2 px becomes 3."""
    return np.repeat(np.repeat(region, 3, 0), 3, 1)[::2, ::2]


def logo(capture, k):
    return D.nearest(np.array(Image.open(os.path.join(capture, "art", "logo_dark.png")).convert("RGBA")), k)


def reel_cover(capture):
    fr = np.zeros((1920, 1080, 3), np.uint8)
    fr[:] = M.INK
    lg = logo(capture, 6)
    D.blend(fr, lg, (1080 - lg.shape[1]) // 2, 290)
    pic = x15(booklet(capture)[200:680, 320:1040])
    top = 760
    fr[top - 4:top] = M.EDGE
    fr[top:top + pic.shape[0]] = pic
    fr[top + pic.shape[0]:top + pic.shape[0] + 4] = M.EDGE
    words = M.card_image([[("REAL", M.CREAM), ("FACES.", M.AMBER)], [("FAKE", M.RED), ("PAPERS.", M.CREAM)]], 900)
    D.blend(fr, words, (1080 - words.shape[1]) // 2, top - words.shape[0] // 2 - 20)
    cta = M.card_image([[("FREE", M.CREAM), ("DEMO", M.AMBER), ("OUT NOW", M.CREAM)]], 860)
    D.blend(fr, cta, (1080 - cta.shape[1]) // 2, top + pic.shape[0] + 26)
    return fr


def thumbnail(capture):
    W, H = 1920, 1080
    fr = np.zeros((H, W, 3), np.uint8)
    fr[:] = M.INK
    fr[90:990, :1080] = x15(booklet(capture)[200:800, 320:1040])     # the booklet alone (rows 200-799), on the ink
    fr[:, 1080:1084] = M.EDGE
    # The right panel, centred as one block: the logo, FREE DEMO OUT NOW, ON STEAM.
    cx = (1084 + W) // 2
    lg = logo(capture, 8)
    words = M.card_image([[("FREE", M.CREAM), ("DEMO", M.AMBER)], [("OUT NOW", M.CREAM)]], 800)
    steam = M.outlined("ON STEAM", 84, M.CREAM)
    y = (H - (lg.shape[0] + 70 + words.shape[0] + 40 + steam.shape[0])) // 2
    for img, gap in ((lg, 70), (words, 40), (steam, 0)):
        D.blend(fr, img, cx - img.shape[1] // 2, y)
        y += img.shape[0] + gap
    return fr


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True)
    args = ap.parse_args()
    for name, fr in (("teaser_reel_cover_1080x1920.png", reel_cover(args.capture)),
                     ("teaser_thumbnail_1920x1080.png", thumbnail(args.capture))):
        out = os.path.join(OUT, name)
        Image.fromarray(fr).save(out)
        print(out, os.path.getsize(out))


if __name__ == "__main__":
    main()
