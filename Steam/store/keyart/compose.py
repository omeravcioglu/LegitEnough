"""Steam store + library art for LEGIT ENOUGH, composed from pieces the game itself draws.

export.ps1 renders the kit from the game's own drawing code: the shop at night with no text,
Samir's head and shoulders big and lit by a key-art rig (warm desk lamp low on the left, cold
window rim on the right), Samir at the counter at in-game scale, and small props. This script
stages them per format on the art-pixel grid (the game's 4x pixels), lights the scene in the
game's banded style, places the logo, upscales with nearest neighbour and writes every asset at
its exact Steam size, plus a review sheet of everything at the size Steam shows it.

The idea of the art: a nervous man at the counter at night, his brand-new passport in the lamp
light in front of him, and the title stamped beside him. Is it legit? Enough.

Usage: python Steam/store/keyart/compose.py --kit <export>/kit --logo <folder with logo_dark.png>
"""
import argparse
import json
import os

import numpy as np
from PIL import Image, ImageDraw

HERE = os.path.dirname(os.path.abspath(__file__))
STORE = os.path.dirname(HERE)

BAYER4 = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]]) / 16.0
INK = np.array([22, 18, 15], np.float32)
WARM = (255, 168, 86)
COLD = (80, 120, 220)
NEON = (255, 70, 150)

SIZES = {
    "main_capsule_1232x706.png": (1232, 706),
    "header_capsule_920x430.png": (920, 430),
    "small_capsule_462x174.png": (462, 174),
    "vertical_capsule_748x896.png": (748, 896),
    "library_capsule_600x900.png": (600, 900),
    "library_header_920x430.png": (920, 430),
    "library_hero_3840x1240.png": (3840, 1240),
    "page_background_1438x810.png": (1438, 810),
}


class Kit:
    def __init__(self, kit, logo_dir):
        self.dir = kit
        self.logo_dir = logo_dir
        with open(os.path.join(kit, "meta.json"), encoding="utf-8") as f:
            self.meta = json.load(f)

    def img(self, name):
        return load(os.path.join(self.dir, name + ".png"))

    def logo(self, dark=True):
        return load(os.path.join(self.logo_dir, ("logo_dark" if dark else "logo_light") + ".png"))


def load(path):
    return np.array(Image.open(path).convert("RGBA")).astype(np.float32)


# ---------------------------------------------------------------- pixel helpers

def over(dst, src, x, y):
    """Alpha-composite src onto dst (float RGBA) at (x, y), clipped."""
    x, y = int(round(x)), int(round(y))
    h, w = src.shape[:2]
    H, W = dst.shape[:2]
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return dst
    s = src[y0 - y:y1 - y, x0 - x:x1 - x]
    d = dst[y0:y1, x0:x1]
    a = s[..., 3:4] / 255.0
    d[..., :3] = d[..., :3] * (1 - a) + s[..., :3] * a
    d[..., 3:4] = np.maximum(d[..., 3:4], s[..., 3:4])
    return dst


def crop(img, x, y, w, h):
    out = np.zeros((h, w, 4), np.float32)
    return over(out, img, -x, -y)


def scale(img, k):
    return np.repeat(np.repeat(img, k, 0), k, 1)


def dither(h, w):
    return np.tile(BAYER4, (h // 4 + 1, w // 4 + 1))[:h, :w]


def outline(img, color=INK):
    a = img[..., 3] > 127
    grow = a.copy()
    grow[1:, :] |= a[:-1, :]
    grow[:-1, :] |= a[1:, :]
    grow[:, 1:] |= a[:, :-1]
    grow[:, :-1] |= a[:, 1:]
    ring = grow & ~a
    out = img.copy()
    out[ring, :3] = color
    out[ring, 3] = 255
    return out


def shadow(img, dx=1, dy=1, color=INK, alpha=235.0):
    h, w = img.shape[:2]
    out = np.zeros((h + dy, w + dx, 4), np.float32)
    sh = np.zeros_like(img)
    sh[..., :3] = color
    sh[..., 3] = np.where(img[..., 3] > 127, alpha, 0)
    over(out, sh, dx, dy)
    over(out, img, 0, 0)
    return out


def banded(canvas, light, bands=8):
    """Multiply by a light field quantized to bands with ordered dither (the game's lighting)."""
    h, w = light.shape
    q = np.floor(np.clip(light, 0, 2) * bands + dither(h, w)) / bands
    canvas[..., :3] = np.clip(canvas[..., :3] * q[..., None], 0, 255)
    return canvas


def glow(canvas, cx, cy, rx, ry, color, strength, bands=6, mask=None):
    """Additive pool of light, banded and dithered."""
    h, w = canvas.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2)
    f = np.clip(1 - d, 0, 1) ** 1.6
    q = np.floor(f * bands + dither(h, w)) / bands
    if mask is not None:
        q = q * mask
    canvas[..., :3] = np.clip(canvas[..., :3] + q[..., None] * np.array(color, np.float32) * strength, 0, 255)
    return canvas


def vignette(canvas, cx, cy, rx, ry, floor=0.45, power=1.8):
    h, w = canvas.shape[:2]
    yy, xx = np.mgrid[0:h, 0:w]
    d = np.sqrt(((xx - cx) / rx) ** 2 + ((yy - cy) / ry) ** 2)
    return banded(canvas, 1 - (1 - floor) * np.clip(d, 0, 1) ** power)


def grade(canvas, shadows=(14, 18, 34), sat=0.9):
    rgb = canvas[..., :3]
    lum = rgb.mean(axis=2, keepdims=True)
    rgb = lum + (rgb - lum) * sat
    k = np.clip(1 - lum / 140.0, 0, 1)
    canvas[..., :3] = np.clip(rgb + k * np.array(shadows, np.float32) * 0.6, 0, 255)
    return canvas


def rim(img, color=COLD, side=1, amount=0.55, width=2):
    """Cold rim light along one side of a figure's silhouette."""
    a = img[..., 3] > 127
    out = img.copy()
    edge = np.zeros_like(a)
    shifted = a.copy()
    for k in range(1, width + 1):
        nb = np.zeros_like(a)
        if side > 0:
            nb[:, :-k] = a[:, k:]
        else:
            nb[:, k:] = a[:, :-k]
        edge |= a & ~nb
    col = np.array(color, np.float32)
    out[edge, :3] = out[edge, :3] * (1 - amount) + col * amount * 1.4
    return out


# ---------------------------------------------------------------- the shop

def room_back(kit, window=1, fix_neon=True):
    back = kit.img("night1_back")
    over(back, kit.img(f"night1_window{window}"), 18, 30)
    if fix_neon:
        neon_panel(back)
    return back


def neon_panel(img, x0=172, y0=55, x1=206, y1=73):
    """The door sign has no letters in text-free renders; paint the empty frame out as door."""
    wood = np.median(img[y1 + 3:y1 + 9, x0:x1 + 1, :3].reshape(-1, 3), axis=0)
    img[y0 - 1:y1 + 2, x0:x1 + 1, :3] = wood
    return img


def passport(kit, w=34, h=46):
    """A closed passport: dark red cover, gold emblem and gold title bars (no letters)."""
    p = np.zeros((h + 2, w + 2, 4), np.float32)
    red = np.array([118, 30, 38], np.float32)
    dark = np.array([72, 16, 24], np.float32)
    hi = np.array([160, 56, 58], np.float32)
    gold = np.array([214, 172, 92], np.float32)
    p[2:h + 2, 2:w + 2, :3] = INK
    p[2:h + 2, 2:w + 2, 3] = 170
    p[0:h, 0:w, :3] = red
    p[0:h, 0:w, 3] = 255
    p[0:h, 1:4, :3] = dark
    p[1, 4:w - 1, :3] = hi
    p[1:h - 1, w - 2, :3] = dark
    p[h - 2, 1:w - 1, :3] = dark
    p[0, :w, :3] = INK
    p[h - 1, :w, :3] = INK
    p[:h, 0, :3] = INK
    p[:h, w - 1, :3] = INK
    em = kit.img("emblem")
    over(p, em, (w - em.shape[1]) // 2 + 1, 7)
    p[36:38, 9:w - 7, :3] = gold
    p[39:40, 12:w - 10, :3] = gold * 0.8
    return p


def cash_sprite(kit):
    """The game's banknote stack with its printed amount wiped (no text on capsules)."""
    c = kit.img("cash")
    rgb = c[..., :3]
    light = (rgb.mean(axis=2) > 170) & (c[..., 3] > 0)
    ys, xs = np.nonzero(light)
    if len(ys):
        y0, y1, x0, x1 = ys.min(), ys.max(), xs.min(), xs.max()
        band = np.median(rgb[light], axis=0)
        box = c[y0:y1 + 1, x0:x1 + 1]
        dark = box[..., :3].mean(axis=2) < 150
        box[dark, :3] = band
    return c


def counter(canvas, top, lit_x=None):
    """The counter top running across the bottom: dark wood, a lit front edge, plank seams."""
    H, W = canvas.shape[:2]
    wood = np.array([50, 33, 26], np.float32)
    canvas[top:, :, :3] = wood
    canvas[top:, :, 3] = 255
    canvas[top, :, :3] = np.array([128, 90, 60], np.float32)
    canvas[top + 1, :, :3] = np.array([92, 62, 43], np.float32)
    canvas[top + 2, :, :3] = wood * 0.7
    for x in range(7, W, 27):
        canvas[top + 4:, x, :3] = wood * 0.72
    t = dither(H - top, W)
    grain = (t > 0.8) & (np.arange(H - top)[:, None] % 3 == 1)
    canvas[top:, :, :3][grain] *= 0.88
    return canvas


SWEAT = [
    "..#..",
    ".#o#.",
    "#ooo#",
    "#oow#",
    ".###.",
]


def sweat_drop():
    d = np.zeros((5, 5, 4), np.float32)
    for y, row in enumerate(SWEAT):
        for x, ch in enumerate(row):
            if ch == "#":
                d[y, x] = (40, 60, 90, 255)
            elif ch == "o":
                d[y, x] = (150, 200, 235, 255)
            elif ch == "w":
                d[y, x] = (245, 252, 255, 255)
    return d


def samir_big(kit, name, side_rim=1):
    img = kit.img(name + "_keyL")
    img = rim(img, (120, 150, 225), side_rim, 0.32, 1)
    # A bead of sweat at the temple away from the lamp.
    h = 96 if "_h96_" in name else 64
    ax, ay = head_anchor(kit, name)
    over(img, sweat_drop(), ax + h * 0.36, ay - h * 0.2)
    return img


def head_anchor(kit, name):
    m = kit.meta[name]
    return m[0], m[1]


def title(kit, k=2):
    return shadow(outline(scale(kit.logo(True), k)), k // 2 or 1, k // 2 or 1)


def close_scene(kit, W, H, bgx, bgy, head, hx, hy, top, pp=None, cash=None, darken=0.6):
    """The close-up: the dark shop behind, Samir big and lamp-lit, the counter in front."""
    c = crop(room_back(kit), bgx, bgy, W, H)
    banded(c, np.full((H, W), darken))
    grade(c)
    glow(c, 70 - bgx + 40, 40 - bgy + 8, 90, 70, COLD, 0.22)                 # window light
    glow(c, 189 - bgx, 40 - bgy, 30, 40, NEON, 0.07)                          # neon through the door glass
    hd = samir_big(kit, head)
    ax, ay = head_anchor(kit, head)
    over(c, hd, hx - ax, hy - ay)
    if top < H:
        counter(c, top)
    if cash is not None:
        over(c, cash_sprite(kit), cash[0], cash[1])
    if pp is not None:
        over(c, passport(kit), pp[0], pp[1])
    return c


def finish(c, lamp, rimlight=None, vig=None):
    glow(c, lamp[0], lamp[1], lamp[2], lamp[3], WARM, lamp[4])
    if rimlight:
        glow(c, *rimlight, COLD, 0.10)
    if vig:
        vignette(c, *vig)
    return c


def save(canvas_art, k, name, flatten=True):
    w, h = SIZES[name]
    big = scale(canvas_art, k)[:h, :w]
    if big.shape[0] < h or big.shape[1] < w:
        raise ValueError(f"{name}: composed {big.shape[1]}x{big.shape[0]}, needs {w}x{h}")
    img = Image.fromarray(np.clip(big, 0, 255).astype(np.uint8), "RGBA")
    (img.convert("RGB") if flatten else img).save(os.path.join(OUT, name))
    return img


# ---------------------------------------------------------------- formats

def main_capsule(kit):
    """1232x706 (4x → 308x177): title left, Samir right, his passport and the cash between."""
    c = close_scene(kit, 308, 177, 40, 8, "samirhead_h96_nervous_side", 226, 58, 151,
                    pp=(150, 119), cash=(104, 151))
    finish(c, (162, 158, 120, 42, 0.32), (300, 30, 60, 80), (190, 86, 225, 140, 0.55))
    over(c, title(kit, 2), 8, 12)
    save(c, 4, "main_capsule_1232x706.png")


def header_capsule(kit, name="header_capsule_920x430.png"):
    """920x430 (4x → 230x108)."""
    c = close_scene(kit, 230, 108, 70, 14, "samirhead_h64_nervous_side", 186, 38, 100,
                    cash=(150, 100))
    finish(c, (168, 98, 80, 28, 0.30), (226, 18, 40, 50), (160, 54, 170, 90, 0.55))
    over(c, title(kit, 2), 5, 11)
    save(c, 4, name)


def lockup(kit, k=2):
    """Horizontal logo lockup for the small capsule: the LEGIT stamp, then ENOUGH beside it
    (dropped a little, like the stacked logo's offset), so the title can span the strip."""
    lg = kit.logo(True)
    stamp = lg[2:23, 9:57]
    word = lg[23:34, 20:62]
    w = stamp.shape[1] + 3 + word.shape[1]
    h = stamp.shape[0] + 3
    out = np.zeros((h, w, 4), np.float32)
    over(out, stamp, 0, 0)
    over(out, word, stamp.shape[1] + 3, h - word.shape[0])
    return shadow(outline(scale(out, k)), max(1, k // 2), max(1, k // 2))


def small_capsule(kit):
    """462x174 (2x → 231x87): Steam wants the logo to nearly fill this one, so the title runs
    across the strip over the shop at night."""
    W, H = 231, 87
    c = crop(room_back(kit), 4, 26, W, H)
    banded(c, np.full((H, W), 0.5))
    grade(c)
    glow(c, 160, 80, 120, 34, WARM, 0.22)
    glow(c, 20, 20, 70, 60, COLD, 0.14)
    vignette(c, 115, 44, 150, 70, 0.45)
    lg = lockup(kit, 2)
    over(c, lg, (W - lg.shape[1]) // 2, (H - lg.shape[0]) // 2)
    save(c, 2, "small_capsule_462x174.png")


def tall(kit, W, H, name):
    """Vertical formats (4x): title on top, Samir's face in the middle, passport on the counter."""
    cx = W // 2
    c = close_scene(kit, W, H, 240 - cx, 0, "samirhead_h64_nervous_cam", cx, 126, H - 24,
                    pp=(cx - 58, H - 58), cash=(cx + 24, H - 24))
    finish(c, (cx - 30, H - 26, W * 0.75, 40, 0.30), (W - 10, 90, 40, 80), (cx, 120, W * 0.9, H * 0.75, 0.5))
    lg = title(kit, 2)
    over(c, lg, (W - lg.shape[1]) // 2, 8)
    save(c, 4, name)


def library_hero(kit):
    """3840x1240 (8x → 480x155): the shop at night, wide; Samir close and lamp-lit on the right,
    glancing toward the bottom-left where Steam places the logo. No text or logo in the art."""
    # Steam keeps only the centre 860x380 px (art 107x47.5 around 240,77.5) on every window
    # size: Samir's face and the passport sit in it; the logo goes bottom-left, outside it.
    c = close_scene(kit, 480, 155, 0, 12, "samirhead_h64_nervous_side", 266, 64, 133,
                    pp=(196, 101), cash=(156, 133))
    finish(c, (214, 140, 170, 42, 0.30), (420, 30, 90, 90), (250, 70, 300, 120, 0.5))
    save(c, 8, "library_hero_3840x1240.png")


def page_background(kit):
    """1438x810 (3x → the whole 480x270 room), dark and quiet behind the store page."""
    c = room_back(kit)
    over(c, kit.img("night1_front"), 0, 0)
    vignette(c, 240, 120, 300, 190, 0.3)
    grade(c, sat=0.7)
    c[..., :3] *= 0.5
    save(c, 3, "page_background_1438x810.png")


def library_logo(kit):
    """Transparent logo cropped tight to the art (Steam anchors the image's edges, so no empty
    margin), scaled by a whole number so its pixels stay square, and 720px tall."""
    lg = title(kit, 1)
    ys, xs = np.nonzero(lg[..., 3] > 0)
    lg = lg[ys.min():ys.max() + 1, xs.min():xs.max() + 1]
    k = min(1280 // lg.shape[1], 720 // lg.shape[0])
    img = scale(lg, k)
    canvas = np.zeros((720, img.shape[1], 4), np.float32)
    over(canvas, img, 0, (720 - img.shape[0]) // 2)
    Image.fromarray(np.clip(canvas, 0, 255).astype(np.uint8), "RGBA").save(os.path.join(OUT, "library_logo_transparent.png"))


def review_sheet():
    """Every asset at the size Steam shows it, on Steam's dark blue."""
    show = [
        ("main_capsule_1232x706.png", 616), ("header_capsule_920x430.png", 460),
        ("small_capsule_462x174.png", 231), ("vertical_capsule_748x896.png", 374),
        ("library_capsule_600x900.png", 300), ("library_hero_3840x1240.png", 1280),
        ("page_background_1438x810.png", 480), ("library_logo_transparent.png", 320),
    ]
    sheet = Image.new("RGB", (1940, 1420), (27, 40, 56))
    d = ImageDraw.Draw(sheet)
    x, y, row = 10, 10, 0
    for name, wd in show:
        im = Image.open(os.path.join(OUT, name)).convert("RGBA")
        hh = int(im.height * wd / im.width)
        im = im.resize((wd, hh), Image.LANCZOS)
        if x + wd > 1930:
            x, y, row = 10, y + row + 26, 0
        bg = Image.new("RGBA", im.size, (27, 40, 56, 255))
        bg.alpha_composite(im)
        sheet.paste(bg.convert("RGB"), (x, y + 16))
        d.text((x, y), f"{name} (shown {wd}px wide)", fill=(200, 210, 220))
        x += wd + 14
        row = max(row, hh)
    sheet.save(os.path.join(OUT, "_review.png"))


def main():
    global OUT
    ap = argparse.ArgumentParser()
    ap.add_argument("--kit", required=True)
    ap.add_argument("--logo", required=True, help="folder with logo_dark.png (with letters)")
    ap.add_argument("--out", default=os.path.join(HERE, "out"))
    args = ap.parse_args()
    OUT = args.out
    os.makedirs(OUT, exist_ok=True)
    kit = Kit(args.kit, args.logo)
    main_capsule(kit)
    header_capsule(kit)
    header_capsule(kit, "library_header_920x430.png")
    small_capsule(kit)
    tall(kit, 187, 224, "vertical_capsule_748x896.png")
    tall(kit, 150, 225, "library_capsule_600x900.png")
    library_hero(kit)
    page_background(kit)
    library_logo(kit)
    for name, (w, h) in SIZES.items():
        im = Image.open(os.path.join(OUT, name))
        assert im.size == (w, h), (name, im.size)
    lw, lh = Image.open(os.path.join(OUT, "library_logo_transparent.png")).size
    assert (lw == 1280 or lh == 720) and lw <= 1280 and lh <= 720, (lw, lh)
    review_sheet()
    print("wrote", OUT)


OUT = ""

if __name__ == "__main__":
    main()
