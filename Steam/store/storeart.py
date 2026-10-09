"""Builds the Steam store icons, description images and screenshots for LEGIT ENOUGH from the
store-harness captures in src/. (Capsules, library art and the page background are key art made
by keyart/compose.py.)

The shop is drawn at 480x270 and shown at 4x, so the captures are cut back to native pixels
(one sample per 4x4 block) and scaled up again by whole numbers: every capsule stays pixel-exact.
The logo is the game's own stamp (UiArt.Logo, exported natively as art_logo_*.png).

Usage: python storeart.py            (writes into this folder; needs Pillow + numpy)
Refresh src/ first with harness/store-shots.ps1 when the game's look changes.
"""
import os

import numpy as np
from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, "src")
OUT = HERE

CREAM = (236, 227, 200, 255)
CREAM_EDGE = (201, 189, 154, 255)
TAPE = (214, 203, 160, 210)
INK = (22, 18, 15, 255)


def load(name):
    return Image.open(os.path.join(SRC, name + ".png")).convert("RGBA")


def native(name):
    """A 1920x1080 capture cut back to the 480x270 art grid."""
    a = np.asarray(load(name))
    return Image.fromarray(np.ascontiguousarray(a[2::4, 2::4]))


def up(img, s):
    return img.resize((img.width * s, img.height * s), Image.NEAREST)


def trim(img):
    return img.crop(img.getbbox())


def logo(dark):
    return trim(load("art_logo_dark" if dark else "art_logo_light"))


def slip(pad_x=6, pad_y=5):
    """The light logo on a taped paper slip, at native pixel size (with its shadow)."""
    lg = logo(False)
    w, h = lg.width + pad_x * 2, lg.height + pad_y * 2
    out = Image.new("RGBA", (w + 2, h + 2), (0, 0, 0, 0))
    shadow = Image.new("RGBA", (w, h), (10, 8, 6, 150))
    out.alpha_composite(shadow, (2, 2))
    paper = Image.new("RGBA", (w, h), CREAM)
    px = paper.load()
    for x in range(w):
        px[x, h - 1] = CREAM_EDGE
    for y in range(h):
        px[w - 1, y] = CREAM_EDGE
    # Faint ruled lines, like a form.
    for y in range(pad_y + 2, h - 2, 6):
        for x in range(2, w - 2):
            if (x + y) % 3:
                px[x, y] = (226, 216, 186, 255)
    out.alpha_composite(paper, (0, 0))
    out.alpha_composite(lg, (pad_x, pad_y))
    tape = Image.new("RGBA", (9, 4), TAPE)
    out.alpha_composite(tape, (2, -1 + 0))
    out.alpha_composite(tape, (w - 11, 0))
    return out


def scene(name, size, scale, center):
    """Crop the native scene so `center` (native px) sits mid-frame, clamped to the room."""
    src = native(name)
    w, h = size
    nw, nh = -(-w // scale), -(-h // scale)
    x0 = int(round(center[0] - nw / 2))
    y0 = int(round(center[1] - nh / 2))
    x0 = max(0, min(src.width - nw, x0))
    y0 = max(0, min(src.height - nh, y0))
    crop = src.crop((x0, y0, x0 + nw, y0 + nh))
    big = up(crop, scale)
    return big.crop((0, 0, w, h))


def shade(img, k):
    a = np.asarray(img).astype(np.float32)
    a[..., :3] *= k
    return Image.fromarray(a.clip(0, 255).astype(np.uint8))


def place(canvas, item, scale, x, y):
    canvas.alpha_composite(up(item, scale), (x, y))


def save(img, name, rgb=True):
    path = os.path.join(OUT, name)
    (img.convert("RGB") if rgb else img).save(path)
    print(name, img.size)


def build():
    # Text-free renders (harness NoText): Steam allows no text on capsules but the logo.
    key = "c01_samir_counter"     # Day 1, rain, Samir at the counter
    night = "c05_busy_night"      # Day 100 kit, night, a queue by the window

    # The capsules, the library art and the page background are key art now: they come from
    # keyart/compose.py (export.ps1 renders its pieces from the game), not from these captures.
    # Don't write them here, or a run of this script puts the old screenshot crops back.

    # Icons from the "LE" mark.
    mark = trim(load("art_mark"))
    icon = Image.new("RGBA", (184, 184), INK)
    m = up(mark, 184 // mark.width)
    icon.alpha_composite(m, ((184 - m.width) // 2, (184 - m.height) // 2))
    icon.convert("RGB").save(os.path.join(OUT, "community_icon_184x184.jpg"), quality=95)
    print("community_icon_184x184.jpg", icon.size)
    sh = Image.new("RGBA", (256, 256), (0, 0, 0, 0))
    m = up(mark, 256 // mark.width)
    sh.alpha_composite(m, ((256 - m.width) // 2, (256 - m.height) // 2))
    save(sh, "shortcut_icon_256.png", rgb=False)
    sh.save(os.path.join(OUT, "client_icon.ico"), sizes=[(16, 16), (32, 32), (48, 48), (64, 64), (128, 128), (256, 256)])
    print("client_icon.ico")

    # Description images: 616 wide (the About column), halved captures so the 4x art lands on 2x.
    os.makedirs(os.path.join(OUT, "description"), exist_ok=True)

    def half(name, box):
        a = np.asarray(load(name))
        x0, y0, x1, y1 = box
        return Image.fromarray(np.ascontiguousarray(a[y0:y1:2, x0:x1:2]))

    desc = {
        "about_shop.png": up(native(key).crop((86, 40, 394, 172)), 2),
        "about_photo.png": half("10_crop", (40, 20, 1272, 980)),
        "about_editor.png": half("14_editor", (380, 60, 1612, 760)),
        "about_business.png": up(native(night).crop((86, 40, 394, 172)), 2),
    }
    for n, img in desc.items():
        img.convert("RGB").save(os.path.join(OUT, "description", n))
        print("description/" + n, img.size)

    # Screenshots, in carousel order.
    shots = [
        "03_samir_walks_in", "08_camera_posed", "14_editor", "19_passport", "44_busy_night",
        "04_negotiation", "09_review", "20_handbook_france", "37_news_day2", "41_day100_customer",
        "36_handbook_outdated", "27_day_report",
    ]
    os.makedirs(os.path.join(OUT, "screenshots"), exist_ok=True)
    for i, n in enumerate(shots, 1):
        load(n).convert("RGB").save(os.path.join(OUT, "screenshots", f"{i:02d}_{n.split('_', 1)[1]}.png"))
    print(len(shots), "screenshots")


if __name__ == "__main__":
    build()
