"""LEGIT ENOUGH, the hype cut: 20 s for socials, vertical (1080x1920: Reels, Shorts, TikTok) and
horizontal (1920x1080) from one timeline.

Same takes as the trailer (trailer.ps1), same instruments, key and title cards, but at 120 BPM: the noir
caper played as a chase. A beat is exactly 30 frames, so every cut lands on a beat. Every card holds for
1-2 s (2-4 beats) so it can be read while the shots keep moving under it.

Vertical shots are crops of the 1920x1080 takes at whole-number scales (2x: 540x960, 3x: 360x640,
4x: 270x480), so the 4-pixel art grid stays sharp; a crop may run off the bottom of a take into black,
which the Reels caption covers anyway. A speech bubble that doesn't fit a vertical crop is painted out
(from a frame of the same take just before the line) and redrawn above the speaker, big, in the game's
fonts. Horizontal shots are the full frame or the trailer's 1.5x/2x nearest-neighbour punch-ins.

Usage:
  python Tools/Trailer/make_hype.py --capture <capture folder> [--out Steam/trailer] [--preview]
Writes legit_enough_hype_vertical_1080x1920.mp4 and legit_enough_hype_1080p60.mp4
(--preview: one contact sheet per format, a frame from every shot, instead of the movies).
"""
import argparse
import os
import subprocess
import sys
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import make_trailer as T  # noqa: E402  (takes, ambience beds, the score mix, colours)
import studio_logo  # noqa: E402
from audio import dsp, inst, sfx  # noqa: E402
from audio import score as SC  # noqa: E402
from audio.dsp import n_of  # noqa: E402

FPS = 60
BPM = 120.0
BEAT = 60.0 / BPM               # 0.5 s
FPB = 30                        # frames per beat
TOTAL_BEATS = 40                # 20 s; the last beat fades to black
TAIL = 0.0
SECONDS = TOTAL_BEATS * BEAT + TAIL
SRC_W, SRC_H = 1920, 1080
FORMATS = {"vertical": (1080, 1920), "horizontal": (1920, 1080)}
CREAM, INK, RED = T.CREAM, T.INK, T.RED
AMBER = (232, 168, 64)
STAMP_TTF = os.path.join(T.FONTS, "LegitStamp.ttf")
PIXEL_TTF = os.path.join(T.FONTS, "LegitPixel.ttf")

# The score's timing lives in module globals, read at render time: play it faster.
SC.BPM, SC.BEAT, SC.BAR, SC.SWING = BPM, BEAT, 4 * BEAT, 0.62


def bf(b):
    return int(round(b * FPB))


def bt(b):
    return b * BEAT


def S(b, n, take, t, v, h=None, **kw):
    """A shot on beat b, n beats long, from `take` at t s. v = vertical crop (scale, x0, y0) in take
    pixels; h = horizontal punch-in (scale, x0, y0), or None for the whole frame."""
    d = dict(b=b, n=n, take=take, t=t, v=v, h=h)
    d.update(kw)
    return d


# ====================================================================== the edit
DOOR = (2, 492, 0)          # the door, the sign, the clock
SAMIR = (2, 689, 0)         # whoever is at the counter
CAMERA = (2, 208, 40)       # the camera's live view
PHOTO = (2, 133, 40)        # the photo review
EDL = [
    # hook: the shop opens (FREE DEMO / OUT NOW.), a man walks in
    S(0, 2, "night", 2.00, DOOR, fade_in=6),
    S(2, 2, "samir", 1.00, DOOR),                                   # the sign flips (2.56), the bell, Samir
    S(4, 4, "samir", 9.50, SAMIR, (1.5, 320, 0), bubble=5.0),       # "I need a French passport."
    # the photo: the drop
    S(8, 2, "photo", 4.40, CAMERA, (1.5, 0, 0), shake=True),        # TAKE THEIR PHOTO. Turn his head.
    S(10, 1, "photo", 8.20, CAMERA),                                # glasses off
    S(11, 1, "photo", 14.97, CAMERA),                               # "Like a statue." Shutter.
    S(12, 2, "photo", 15.37, PHOTO),                                # flash: he blinked. The band stops.
    S(14, 2, "photo", 18.57, PHOTO, shake=True),                    # flash: eyes open
    # the papers
    S(16, 2, "editor", 5.35, (2, 560, 60), (1.5, 400, 240), shake=True),   # MAKE THEIR PAPERS. Typing.
    S(18, 1, "editor", 13.30, (2, 560, 100), (1.5, 400, 240)),             # the photo goes in
    S(19, 1, "editor", 14.62, (2, 1096, 100), (1.5, 400, 240)),            # PRINT (19.5)
    S(20, 1, "print", 5.97, (3, 1328, 600), (1.5, 640, 360)),              # it jams (20.5)
    S(21, 1, "print", 7.83, (3, 1328, 600), (1.5, 640, 360), shake=True),  # hit it, not too hard
    S(22, 1, "print", 19.25, (3, 930, 600), (1.5, 640, 360), shake=True),  # bind it: stamp
    S(23, 1, "print", 20.70, (2, 256, 30)),                                # the passport
    S(24, 3, "handover", 9.92, (4, 1650, 700), (2, 960, 540)),             # cash (24.2): GET PAID. PROBABLY.
    # the world turns
    S(27, 1, "night_busy", 8.20, (2, 560, 0)),                             # word gets around
    S(28, 2, "day100card", 1.0, (2, 690, 60), shake=True),                 # DAY 100
    # finale, framed past the door so the CLOSED sign stays out of the end card
    S(30, 4, "night", 5.00, (2, 830, 0), (2, 880, 0), dim=0.55),
    S(34, 2, "night", 7.40, (2, 830, 0), (2, 880, 0), dim=0.72),
    S(36, 4, "night", 0.50, (2, 830, 0), (2, 880, 0), dim=0.62, fade_out=(39.0, 40.0)),
]

# Title cards: (key, first beat, last beat, vertical lines, horizontal line, centre y per format).
CARDS = [
    ("demo", 0, 2, ["FREE DEMO"], "FREE DEMO", (1240, 760)),
    ("out", 2, 4, ["OUT NOW."], "OUT NOW.", (1240, 760)),
    ("photo", 8, 11, ["TAKE THEIR", "PHOTO."], "TAKE THEIR PHOTO.", (1150, 540)),
    ("papers", 16, 19, ["MAKE THEIR", "PAPERS."], "MAKE THEIR PAPERS.", (820, 540)),     # clear of the typing
    ("paid", 24, 27, ["GET PAID.", "PROBABLY."], "GET PAID. PROBABLY.", (520, 540)),       # clear of the cash
    ("perfect", 30, 34, ["IT DOESN'T HAVE", "TO BE PERFECT."], "IT DOESN'T HAVE TO BE PERFECT.", (960, 540)),
    ("just", 34, 36, ["JUST..."], "JUST...", (960, 540)),
]
LOGO_AT = 36.0
END_TEXT_AT = 36.5              # stamped on whole, not typed, so it reads at once
STUDIO_AT = 37.5
END_TEXT = {"vertical": [("FREE DEMO OUT NOW", CREAM, 1084, 72), ("ON STEAM", AMBER, 1188, 72)],
            "horizontal": [("FREE DEMO OUT NOW ON STEAM", CREAM, 760, 60)]}

# The arrangement, one entry per bar (audio/score.py kinds).
MUSIC = [
    dict(kind="sneak"),                                              # 0 FREE DEMO, OUT NOW., the bell
    dict(kind="sneak", brushes=True, stabs=True, fill=True),         # 1 "I need a French passport."
    dict(kind="groove_b", m=0, hit=True, drive=True),                # 2 DROP: the photo
    dict(kind="groove_b", m=1, drive=True),                          # 3 he blinked (stop), eyes open
    dict(kind="groove_b", m=2, hit=True, drive=True),                # 4 the papers
    dict(kind="groove_b", m=3, drive=True, fill=True),               # 5 jam (stop), whack, stamp, passport
    dict(kind="groove", phrase=0, m=0, drive=True, snaps=True, hit=True),                # 6 GET PAID
    dict(kind="groove", phrase=0, m=1, drive=True, snaps=True, double=True, fill=True),  # 7 Day 100, PERFECT
    dict(kind="hits"),                                               # 8 ...PERFECT, JUST...
    dict(kind="end", tag=False),                                     # 9 LOGO
]
HITS = [14.0, 21.0, 22.0, 28.0]              # extra band hits: eyes open, the whack, the stamp, Day 100
MUTES = [(12.4, 13.9), (20.5, 20.95)]        # the band stops: he blinked, the printer jammed
SNOWY = {"news", "tier1", "tier2", "tier3", "dusk", "night_busy", "power", "camera_t3", "os_t3"}


# ====================================================================== pictures
def blend(fr, img, x, y, alpha=1.0):
    H, W = fr.shape[:2]
    h, w = img.shape[:2]
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return
    src = img[y0 - y:y1 - y, x0 - x:x1 - x].astype(np.float32)
    a = src[..., 3:4] / 255.0 * alpha
    dst = fr[y0:y1, x0:x1].astype(np.float32)
    fr[y0:y1, x0:x1] = (dst * (1 - a) + src[..., :3] * a).astype(np.uint8)


def shift(fr, dx, dy):
    if dx == 0 and dy == 0:
        return fr
    H, W = fr.shape[:2]
    out = np.zeros_like(fr)
    ys, yd = (slice(0, H - dy), slice(dy, H)) if dy >= 0 else (slice(-dy, H), slice(0, H + dy))
    xs, xd = (slice(0, W - dx), slice(dx, W)) if dx >= 0 else (slice(-dx, W), slice(0, W + dx))
    out[yd, xd] = fr[ys, xs]
    return out


def darken(fr, a):
    return (fr.astype(np.float32) * (1 - a)).astype(np.uint8)


def whiten(fr, a):
    return (fr.astype(np.float32) * (1 - a) + 255 * a).astype(np.uint8)


def crop_int(src, s, x0, y0, w, h):
    """A w x h window of the take (black where it runs off the edge), scaled by whole number s."""
    out = np.zeros((h, w, 3), np.uint8)
    sx0, sy0, sx1, sy1 = max(0, x0), max(0, y0), min(SRC_W, x0 + w), min(SRC_H, y0 + h)
    out[sy0 - y0:sy1 - y0, sx0 - x0:sx1 - x0] = src[sy0:sy1, sx0:sx1]
    return np.repeat(np.repeat(out, s, 0), s, 1)


def picture(src, shot, fmt):
    W, H = FORMATS[fmt]
    if fmt == "vertical":
        s, x0, y0 = shot["v"]
        return crop_int(src, s, x0, y0, W // s, H // s)
    z = shot.get("h")
    if z is None:
        return src.copy()
    s, x0, y0 = z
    rows = ((np.arange(H) / s).astype(int) + y0).clip(0, SRC_H - 1)
    cols = ((np.arange(W) / s).astype(int) + x0).clip(0, SRC_W - 1)
    return src[rows][:, cols]


def find_bubble(src):
    """The speech bubble over the counter: its cream box plus the dark outline, in take pixels."""
    y0, x0 = 110, 960
    reg = src[y0:360, x0:1560].astype(int)
    cream = (reg[..., 0] > 215) & (reg[..., 1] > 205) & (reg[..., 2] > 165)
    rows = np.where(cream.sum(1) > 250)[0]          # bubble rows are ~400 px of cream; the calendar ~120
    if len(rows) == 0:
        return None
    cols = np.where(cream[rows[0]:rows[-1] + 1].sum(0) > 30)[0]
    if len(cols) == 0:
        return None
    m = 4
    return (x0 + cols[0] - m, y0 + rows[0] - m, x0 + cols[-1] + 1 + m, y0 + rows[-1] + 1 + m)


def crisp_text(img):
    a = np.array(img)
    a[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
    return a


def card_image(lines, max_w):
    """The trailer's big card (INK plate, double red border, cream text, hard shadow), at the
    biggest whole-pixel size whose widest line fits max_w."""
    for size in (144, 120, 96, 72, 60):
        f = ImageFont.truetype(STAMP_TTF, size)
        px, border = size // 12, max(6, size // 12)
        pad = (int(size * 0.42), int(size * 0.29))
        boxes = [f.getbbox(t) for t in lines]
        tw = max(b[2] - b[0] for b in boxes)
        if tw + 2 * pad[0] + 2 * border <= max_w:
            break
    lh = max(b[3] - b[1] for b in boxes)
    gap = size // 5
    bw = tw + 2 * pad[0] + 2 * border
    bh = lh * len(lines) + gap * (len(lines) - 1) + 2 * pad[1] + 2 * border
    bw += (-bw) % 4
    bh += (-bh) % 4
    plate = Image.new("RGBA", (bw, bh), INK + (226,))
    d = ImageDraw.Draw(plate)
    d.rectangle([0, 0, bw - 1, bh - 1], outline=RED + (255,), width=border)
    d.rectangle([border * 2, border * 2, bw - 1 - border * 2, bh - 1 - border * 2], outline=RED + (110,), width=max(2, border // 4))
    txt = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
    td = ImageDraw.Draw(txt)
    for i, (t, b) in enumerate(zip(lines, boxes)):
        x = (bw - (b[2] - b[0])) // 2 - b[0]
        y = border + pad[1] + i * (lh + gap) - b[1]
        td.text((x + px, y + px), t, font=f, fill=(0, 0, 0, 255))
        td.text((x, y), t, font=f, fill=CREAM + (255,))
    ta = crisp_text(txt)
    base = np.array(plate).astype(np.float32)
    a = ta[..., 3:4].astype(np.float32) / 255.0
    base[..., :3] = base[..., :3] * (1 - a) + ta[..., :3] * a
    base[..., 3:4] = np.maximum(base[..., 3:4], ta[..., 3:4])
    return base.astype(np.uint8)


SHAKE = [(6, -4), (-8, 6), (6, 4), (-4, -6), (4, 2), (-2, 4), (2, -2)]


def shake_at(age, length, amp):
    if not 0 <= age < length:
        return (0, 0)
    k = (length - age) / length * amp
    dx, dy = SHAKE[age % len(SHAKE)]
    return (int(dx * k) // 2 * 2, int(dy * k) // 2 * 2)


class Card:
    def __init__(self, spec, fmt):
        key, b0, b1, vlines, hline, cy = spec
        W, H = FORMATS[fmt]
        self.f0, self.f1 = bf(b0), bf(b1)
        self.img = card_image(vlines if fmt == "vertical" else [hline], 860 if fmt == "vertical" else 1720)
        self.cx, self.cy = W // 2, cy[0] if fmt == "vertical" else cy[1]

    def active(self, f):
        return self.f0 <= f < self.f1

    def draw(self, fr, f):
        """Slams in over 4 frames, then holds; returns the shake for this frame."""
        if not self.active(f):
            return (0, 0)
        age = f - self.f0
        img = self.img
        alpha = 1.0
        if age < 4:
            s = 1.3 - 0.3 * age / 4.0
            alpha = 0.55 + 0.45 * age / 4.0
            h, w = img.shape[:2]
            ri = (np.arange(int(h * s)) / s).astype(int).clip(0, h - 1)
            ci = (np.arange(int(w * s)) / s).astype(int).clip(0, w - 1)
            img = img[ri][:, ci]
        h, w = img.shape[:2]
        blend(fr, img, self.cx - w // 2, self.cy - h // 2, alpha)
        return shake_at(age - 4, 8, 1.0)


class LogoSlam:
    """The stamp logo falls onto the shop on LOGO_AT: five frames of fall, dust, shake, a white pop."""
    def __init__(self, path, fmt):
        self.src = np.array(Image.open(path).convert("RGBA"))
        W, H = FORMATS[fmt]
        self.W, self.H = W, H
        self.scale = 12 if fmt == "vertical" else 10
        self.cx, self.cy = W // 2, (700 if fmt == "vertical" else 470)
        self.f0 = bf(LOGO_AT) - 5
        rng = np.random.default_rng(5)
        h, w = self.src.shape[:2]
        bw, bh = w * self.scale, h * self.scale
        self.parts = []
        for _ in range(40):
            x = self.cx + (rng.random() - 0.5) * bw * 1.05
            y = self.cy + bh * 0.5 - rng.random() * 30
            vx = (x - self.cx) / (bw / 2) * (5 + 9 * rng.random()) + rng.normal(0, 2)
            vy = -(4 + 10 * rng.random())
            self.parts.append([x, y, vx, vy, 4 * int(rng.integers(1, 4))])

    def draw(self, fr, f):
        age = f - self.f0
        if age < 0:
            return (0, 0)
        s, alpha = (self.scale * (1.55 - 0.55 * (age / 5.0) ** 2), 0.35 + 0.65 * age / 5.0) if age < 5 else (self.scale, 1.0)
        h0, w0 = self.src.shape[:2]
        hh, ww = int(round(h0 * s)), int(round(w0 * s))
        img = self.src[(np.arange(hh) / s).astype(int).clip(0, h0 - 1)][:, (np.arange(ww) / s).astype(int).clip(0, w0 - 1)]
        x, y = self.cx - ww // 2, self.cy - hh // 2
        blend(fr, img, x - x % 2, y - y % 2, alpha)
        t = age - 5
        if 0 <= t < 40:
            for p in self.parts:
                px, py = p[0] + p[2] * t, p[1] + p[3] * t + 0.45 * t * t
                a = max(0.0, 1.0 - t / 40.0) * 0.85
                sz = p[4]
                xi, yi = int(px) // 4 * 4, int(py) // 4 * 4
                if 0 <= xi < self.W - sz and 0 <= yi < self.H - sz:
                    reg = fr[yi:yi + sz, xi:xi + sz].astype(np.float32)
                    fr[yi:yi + sz, xi:xi + sz] = (reg * (1 - a) + np.array([150, 134, 110]) * a).astype(np.uint8)
        return shake_at(t, 14, 2.0)


def slam(img, age, frames=4, start=1.25):
    """Nearest-neighbour scale-down from `start` to 1 over the first frames, as a stamp lands."""
    if age >= frames:
        return img, 1.0
    s = start - (start - 1.0) * age / frames
    h, w = img.shape[:2]
    ri = (np.arange(int(h * s)) / s).astype(int).clip(0, h - 1)
    ci = (np.arange(int(w * s)) / s).astype(int).clip(0, w - 1)
    return img[ri][:, ci], 0.55 + 0.45 * age / frames


class EndLine:
    """A line of the end card, stamped on whole so it reads at once."""

    def __init__(self, text, color, y, f0, fmt, size=60):
        f = ImageFont.truetype(STAMP_TTF, size)
        W, _ = FORMATS[fmt]
        px = size // 12
        b = f.getbbox(text)
        w, h = b[2] - b[0] + 2 * px, b[3] - b[1] + 2 * px
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.text((-b[0] + px, -b[1] + px), text, font=f, fill=(0, 0, 0, 255))
        d.text((-b[0], -b[1]), text, font=f, fill=color + (255,))
        self.img = crisp_text(img)
        self.f0 = f0
        self.cx, self.cy = W // 2, y + h // 2

    def draw(self, fr, f):
        if f < self.f0:
            return
        img, alpha = slam(self.img, f - self.f0)
        blend(fr, img, self.cx - img.shape[1] // 2, self.cy - img.shape[0] // 2, alpha)


BUBBLE_FILL, BUBBLE_EDGE = (244, 238, 219), (52, 42, 34)
NAME_RED, TEXT_INK = (176, 48, 40), (40, 32, 26)


def bubble_image(who, text, tail_at=0.42, max_w=960):
    """A speech bubble in the game's style, drawn big for phones: the speaker's name in red stamp
    letters over the line in the pixel font, a cream box, a dark edge and a tail along the bottom
    (tail_at: its position as a share of the width)."""
    fn, ft = ImageFont.truetype(STAMP_TTF, 48), ImageFont.truetype(PIXEL_TTF, 72)
    lines, cur = [], ""
    for word in text.split():
        t = (cur + " " + word).strip()
        if ft.getlength(t) <= max_w - 80 or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = word
    lines.append(cur)
    edge, padx, pady, gap, lh = 6, 36, 28, 18, 84
    nb = fn.getbbox(who)
    tw = max([int(ft.getlength(t)) for t in lines] + [nb[2] - nb[0]])
    w = tw + 2 * (padx + edge)
    h = (nb[3] - nb[1]) + gap + lh * len(lines) + 2 * (pady + edge)
    w += (-w) % 4
    h += (-h) % 4
    tail = 28
    img = Image.new("RGBA", (w, h + tail), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    d.fontmode = "1"
    d.rectangle([0, 0, w - 1, h - 1], fill=BUBBLE_FILL + (255,), outline=BUBBLE_EDGE + (255,), width=edge)
    tx = int(w * tail_at) // 4 * 4
    d.polygon([(tx, h - edge), (tx + 56, h - edge), (tx - 4, h + tail)], fill=BUBBLE_EDGE + (255,))
    d.polygon([(tx + 6, h - edge), (tx + 46, h - edge), (tx + 2, h + tail - 9)], fill=BUBBLE_FILL + (255,))
    d.text((edge + padx - nb[0], edge + pady - nb[1]), who, font=fn, fill=NAME_RED + (255,))
    y = edge + pady + (nb[3] - nb[1]) + gap
    for t in lines:
        tb = ft.getbbox(t)
        d.text((edge + padx - tb[0], y - tb[1] + (lh - (tb[3] - tb[1])) // 2), t, font=ft, fill=TEXT_INK + (255,))
        y += lh
    return crisp_text(img)


# Where the big bubble goes: (x or None to centre, y, tail position). Vertical: centred above the
# speaker's head; horizontal: to the right of it, where the game draws its own bubble.
BUBBLE_AT = {"vertical": (None, 186, 0.42), "horizontal": (980, 110, 0.12)}


def line_at(take, f):
    """(speaker, text) of the last speech bubble that went up before frame f of a take."""
    said = [e for e in take.events if e["t"] == "line" and e["f"] <= f]
    return (said[-1]["who"], said[-1]["text"]) if said else None


class StudioEnd:
    def __init__(self, fmt):
        rgba = studio_logo.to_rgba(studio_logo.build())
        self.img = np.repeat(np.repeat(rgba, 4, 0), 4, 1)
        W, _ = FORMATS[fmt]
        self.x = (W - self.img.shape[1]) // 2 // 4 * 4
        self.y = 1380 if fmt == "vertical" else 900
        self.f0 = bf(STUDIO_AT)

    def draw(self, fr, f):
        if f >= self.f0:
            blend(fr, self.img, self.x, self.y, min(1.0, (f - self.f0) / 18.0))


def end_lines(fmt):
    return [EndLine(text, color, y, bf(END_TEXT_AT), fmt, size) for text, color, y, size in END_TEXT[fmt]]


def render_video(fmt, takes, capture, path=None, preview=None):
    W, H = FORMATS[fmt]
    total = bf(TOTAL_BEATS) + int(TAIL * FPS)
    cards = [Card(c, fmt) for c in CARDS]
    logo = LogoSlam(os.path.join(capture, "art", "logo_dark.png"), fmt)
    texts = end_lines(fmt)
    studio = StudioEnd(fmt)
    enc = None
    if preview is None:
        enc = subprocess.Popen(["ffmpeg", "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                                "-framerate", str(FPS), "-i", "-", "-c:v", "libx264rgb", "-preset", "veryfast", "-crf", "0", path],
                               stdin=subprocess.PIPE)
    shots, f, t0 = [], 0, time.time()
    for shot in EDL:
        take = takes[shot["take"]]
        n = bf(shot["n"])
        f_in = int(round(shot["t"] * FPS))
        flashes = [e["f"] - f_in for e in take.events if e["t"] == "flash" and f_in <= e["f"] < f_in + n]
        lift = shot.get("bubble")
        # The game's own bubble is too small to read on a phone: paint it out (and its tail) with a
        # frame of the same take from just before the line, and redraw the line big.
        plate = next(take.frames(int(round(shot["bubble"] * FPS)), 1)) if lift else None
        said = line_at(take, f_in) if lift else None
        bub_x, bub_y, tail_at = BUBBLE_AT[fmt]
        big = bubble_image(*said, tail_at=tail_at) if said else None
        want = range(n) if preview is None else [min(n - 1, 10)]
        gen = take.frames(f_in, n) if preview is None else take.frames(f_in + want[0], 1)
        for k, src in zip(want, gen):
            ff = f if preview is None else f + k
            bubble = find_bubble(src) if lift else None
            if bubble:
                x0, y0, x1, y1 = bubble
                clean = src.copy()
                clean[y0:y1 + 20, x0:x1] = plate[y0:y1 + 20, x0:x1]
                fr = picture(clean, shot, fmt)
            else:
                fr = picture(src, shot, fmt)
            if big is not None:
                img, alpha = slam(big, k, 5, 0.8)
                bx = (W - img.shape[1]) // 2 if bub_x is None else bub_x + (big.shape[1] - img.shape[1]) // 2
                by = bub_y + (big.shape[0] - img.shape[0]) // 2
                shadow = np.zeros(img.shape[:2] + (4,), np.uint8)
                shadow[..., 3] = (img[..., 3] > 0) * 140
                blend(fr, shadow, bx + 10, by + 10)
                blend(fr, img, bx, by, alpha)
            dim = shot.get("dim", 0.0)
            if any(c.active(ff) for c in cards):
                dim = max(dim, 0.42)
            if dim:
                fr = darken(fr, dim)
            for fl in flashes:
                if 0 <= k - fl < 10:
                    fr = whiten(fr, 0.92 * np.exp(-(k - fl) / 2.6))
            dx = dy = 0
            if ff >= logo.f0:
                dx, dy = logo.draw(fr, ff)
                ia = ff - logo.f0 - 5
                if 0 <= ia < 3:
                    fr = whiten(fr, [0.35, 0.18, 0.07][ia])
            for line in texts:
                line.draw(fr, ff)
            studio.draw(fr, ff)
            for c in cards:
                cdx, cdy = c.draw(fr, ff)
                dx, dy = dx + cdx, dy + cdy
            if shot.get("shake"):
                sdx, sdy = shake_at(k, 8, 1.2)
                dx, dy = dx + sdx, dy + sdy
            fr = shift(fr, dx, dy)
            if shot.get("fade_in") and k < shot["fade_in"]:
                fr = darken(fr, 1 - k / shot["fade_in"])
            fo = shot.get("fade_out")
            if fo and ff >= bf(fo[0]):
                fr = darken(fr, min(1.0, (ff - bf(fo[0])) / (bf(fo[1]) - bf(fo[0]))))
            if preview is None:
                enc.stdin.write(fr.tobytes())
                f += 1
            else:
                shots.append((shot, fr))
        if preview is not None:
            f += n
        else:
            print(f"  {fmt:10s} {shot['take']:11s} beat {shot['b']:5.1f}  frame {f:5d}/{total}  {time.time() - t0:5.1f}s", flush=True)
    if preview is not None:
        tw, th = (216, 384) if fmt == "vertical" else (384, 216)
        cols = 9 if fmt == "vertical" else 5
        rows = (len(shots) + cols - 1) // cols
        sheet = Image.new("RGB", (tw * cols, (th + 16) * rows))
        d = ImageDraw.Draw(sheet)
        for i, (shot, fr) in enumerate(shots):
            x, y = (i % cols) * tw, (i // cols) * (th + 16)
            sheet.paste(Image.fromarray(fr).resize((tw, th), Image.LANCZOS), (x, y + 16))
            d.text((x + 3, y + 2), f"b{shot['b']} {shot['take']}@{shot['t']}", fill=(255, 255, 0))
        sheet.save(preview)
        return
    black = np.zeros((H, W, 3), np.uint8)
    while f < total:
        enc.stdin.write(black.tobytes())
        f += 1
    enc.stdin.close()
    enc.wait()


# ====================================================================== sound
def band_hit(score, b):
    bar, beat = int(b // 4), b % 4
    score.add("kit", bar, beat, inst.kick(0.9, score.rng), 1.0, exact=True)
    score.add("kit", bar, beat, inst.brush_tap(0.95, score.rng), 1.0, exact=True)
    score.add("ride", bar, beat, inst.crash(0.6), 0.6, p=0.3, exact=True)
    for i, name in enumerate(SC.VOICINGS["Dm9"]):
        score.add("rhodes", bar, beat, inst.rhodes(inst.hz(inst.midi(name)), 0.5 * BEAT, 0.85), 0.5, p=-0.25 + 0.17 * i, exact=True)


def render_music():
    score = SC.Score(MUSIC, SECONDS)
    stems = score.render()
    for b in HITS:
        band_hit(score, b)

    def vib(b, note, beats, v):
        score.add("vibes", int(b // 4), b % 4, inst.vibes(inst.hz(inst.midi(note)), beats * BEAT, v, score.rng), 1.0, exact=True)

    vib(24.5, "A5", 0.5, 0.75)      # the cash: the vibes' two-note "ding" from the trailer's hand-over
    vib(25.0, "D6", 2.0, 0.8)
    vib(38.0, "A5", 0.4, 0.6)       # a wink under the end card
    vib(38.5, "D6", 3.0, 0.7)
    music = T.mix_music(stems, SECONDS)
    env = np.ones(music.shape[1])
    r = n_of(0.012)
    for a, b in MUTES:
        i0, i1 = n_of(bt(a)), n_of(bt(b))
        env[i0:i1] = 0.0
        env[i0 - r:i0] = np.minimum(env[i0 - r:i0], np.linspace(1, 0, r))
        env[i1:i1 + r] = np.minimum(env[i1:i1 + r], np.linspace(0, 1, r))
    return music * env


def render_sound(takes):
    mx = T.Mixer(SECONDS)
    beds = T.beds(SECONDS + 1.0)
    seed = [5000]

    def sd():
        seed[0] += 1
        return seed[0]

    for shot in EDL:
        take = takes[shot["take"]]
        n = bf(shot["n"])
        f_in = int(round(shot["t"] * FPS))
        t_shot = bt(shot["b"])
        snowy = shot["take"] in SNOWY
        for e in take.events:
            if not f_in <= e["f"] < f_in + n:
                continue
            t = t_shot + (e["f"] - f_in) / FPS
            kind = e["t"]
            sid = e.get("id")
            if kind == "sfx":
                if sid == "DoorBell":
                    mx.put("sfx", sfx.bell(sd()), t, 0.9, -0.3)
                elif sid == "DoorOpen":
                    mx.put("sfx", sfx.door(sd()), t, 0.7, -0.3)
                    outside = sfx.wind(1.6, sd(), 2.2) if snowy else sfx.rain(1.6, sd(), inside=False, intensity=0.9)
                    mx.put("door", dsp.fade(outside, 0.15, 0.8), t + 0.05, 0.9)
                elif sid == "Shutter":
                    mx.put("sfx", sfx.shutter(sd()), t, 1.0, -0.1)
                elif sid == "Stamp":
                    mx.put("sfx", sfx.whack(sd()) if e["p"] < 0.9 else sfx.stamp(sd()), t, 1.1, 0.3)
                elif sid == "Cash":
                    mx.put("sfx", sfx.cash(sd()), t, 1.1, 0.3)
                elif sid == "Jam":
                    mx.put("sfx", sfx.jam(sd()), t, 1.1, 0.4)
                elif sid == "Voice":
                    mx.put("voice", sfx.voice(e["v"], e["p"], sd()), t, 0.85)
                elif sid in ("Click", "Pop"):
                    mx.put("sfx", sfx.click(sd()) if sid == "Click" else sfx.pop(sd()), t, 0.5)
                elif sid == "Key":
                    mx.put("sfx", sfx.key(sd()), t, 0.55)
            elif kind == "key":
                mx.put("sfx", sfx.key(sd()), t, 0.5)
            elif kind == "flash":
                mx.put("sfx", sfx.flash(sd()), t, 0.85)
            elif kind == "zoom":
                mx.put("sfx", sfx.zoom_motor(sd()), t + 0.02, 0.8)
            elif kind == "backdrop":
                mx.put("sfx", sfx.roller(sd(), 1.0), t, 0.9, -0.2)
            elif kind == "power":
                if e["on"] == 0:
                    mx.put("sfx", sfx.power_down(sd()), t, 1.0)
                else:
                    mx.put("sfx", sfx.power_up(sd()), t, 0.9)
        # The printer motor, where it runs inside this shot.
        start = f_in if take.printer_on_at(f_in - 1) else None
        for e in [e for e in take.events if e["t"] == "printer" and f_in <= e["f"] < f_in + n] + [dict(f=f_in + n, t="end")]:
            if e["t"] == "printer" and e["on"] == 1 and start is None:
                start = e["f"]
            elif (e["t"] == "end" or e["on"] == 0) and start is not None:
                if e["f"] - start > 3:
                    mx.put("sfx", sfx.printer_run((e["f"] - start) / FPS + 0.05, sd(), take.tier), t_shot + (start - f_in) / FPS, 0.7)
                start = None
        a, b = n_of(t_shot), n_of(t_shot + shot["n"] * BEAT)
        for bed, g in (({"wind": 0.6, "hum": 0.5} if snowy else {"rain_in": 0.8, "hum": 0.5})).items():
            y = dsp.fade(beds[bed][:, a:b] * g, 0.01, 0.01)
            mx.bus["amb"][:, a:a + y.shape[1]] += y

    for key, b0, *_ in CARDS:
        mx.put("fx", sfx.stamp(sd(), big=True), bt(b0), 0.9)
    mx.put("fx", sfx.stamp(sd()), bt(END_TEXT_AT), 0.9)
    mx.put("fx", inst.boom(), bt(LOGO_AT), 1.0)
    mx.put("fx", sfx.stamp(sd(), big=True), bt(LOGO_AT), 1.25)
    mx.put("fx", inst.reverse_cymbal(1.6), bt(LOGO_AT) - 1.6, 0.6)
    mx.put("fx", inst.riser(1.6), bt(LOGO_AT) - 1.6, 0.3)
    mx.put("fx", inst.riser(1.0), bt(8.0) - 1.0, 0.35)
    mx.put("fx", dsp.pan(inst.bass_slide(110.0, 73.4, 0.6), 0.0), bt(12.4), 0.55)
    mx.put("fx", sfx.shimmer(sd()), bt(STUDIO_AT), 0.5)
    return mx


def master(mx, music):
    sfx_bus = mx.bus["sfx"]
    voice = dsp.compressor(dsp.peak_eq(dsp.hp(mx.bus["voice"], 120.0, 2), 2200.0, 3.0, 0.9), -24.0, 3.0, 0.003, 0.08, makeup_db=3.0)
    amb = mx.bus["amb"] + mx.bus["door"]
    fx = mx.bus["fx"]
    sfx_wet = dsp.wet(sfx_bus * 0.5 + voice * 0.35, dsp.reverb_ir(0.5, predelay=0.006, damp=0.75, width=0.6, seed=41), 0.3)
    fx_wet = dsp.wet(fx, dsp.reverb_ir(2.6, predelay=0.03, damp=0.5, seed=42), 0.3)
    music = music * dsp.undb(-16.0 - dsp.lufs(music))
    music = dsp.duck(music, voice * 2.0 + sfx_bus, depth_db=-3.0, thresh_db=-34.0, attack=0.01, release=0.25)
    mix = music + (sfx_bus + sfx_wet) * dsp.undb(-1.0) + voice * dsp.undb(4.0) + amb * dsp.undb(-7.0) + (fx + fx_wet) * dsp.undb(-3.0)
    mix = dsp.compressor(dsp.hp(mix, 25.0, 2), -16.0, 2.0, 0.01, 0.2)
    for _ in range(3):
        mix = mix * dsp.undb(-14.0 - dsp.lufs(dsp.limiter(mix, -1.5)))
    return dsp.limiter(mix, -1.5)


# ====================================================================== main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True, help="the capture folder written by trailer.ps1")
    ap.add_argument("--out", default=os.path.join(ROOT, "Steam", "trailer"))
    ap.add_argument("--work", default=None, help="folder for intermediates (default: <capture>/../work_hype)")
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--only", choices=sorted(FORMATS), help="render one format")
    args = ap.parse_args()
    work = args.work or os.path.join(os.path.dirname(os.path.abspath(args.capture)), "work_hype")
    os.makedirs(work, exist_ok=True)
    os.makedirs(args.out, exist_ok=True)
    folder = os.path.join(args.capture, "takes")
    takes = {nm: T.Take(folder, nm) for nm in sorted({s["take"] for s in EDL})}
    fmts = [args.only] if args.only else ["vertical", "horizontal"]
    if args.preview:
        for fmt in fmts:
            p = os.path.join(work, f"preview_{fmt}.png")
            render_video(fmt, takes, args.capture, preview=p)
            print("preview:", p)
        return

    t0 = time.time()
    print("audio", flush=True)
    mx = render_sound(takes)
    out = master(mx, render_music())
    # The sound fades out with the picture over the last beat.
    i0 = n_of(bt(TOTAL_BEATS - 1))
    out[:, i0:] *= np.linspace(1.0, 0.0, out.shape[1] - i0) ** 1.5
    wav = os.path.join(work, "hype_audio.wav")
    T.write_wav(wav, out)
    print(f"  {dsp.lufs(out):.1f} LUFS, true peak {dsp.true_peak_db(out):.1f} dBTP ({time.time() - t0:.0f}s)", flush=True)
    names = {"vertical": "legit_enough_hype_vertical_1080x1920.mp4", "horizontal": "legit_enough_hype_1080p60.mp4"}
    for fmt in fmts:
        video = os.path.join(work, f"hype_{fmt}.mkv")
        render_video(fmt, takes, args.capture, path=video)
        final = os.path.join(args.out, names[fmt])
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-i", video, "-i", wav,
                        "-vf", "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,format=yuv420p",
                        "-c:v", "libx264", "-preset", "slow", "-tune", "animation", "-crf", "14", "-profile:v", "high",
                        "-g", "120", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
                        "-c:a", "aac", "-b:a", "320k", "-ar", "48000", "-movflags", "+faststart", "-shortest", final], check=True)
        print("wrote", final, flush=True)
    print(f"done in {time.time() - t0:.0f}s")


if __name__ == "__main__":
    main()
