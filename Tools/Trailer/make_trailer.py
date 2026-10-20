"""Builds the LEGIT ENOUGH trailer from the 60 fps takes recorded by trailer.ps1.

Everything lives on one beat grid (100 BPM: a beat is exactly 36 frames, a bar 2.4 s). The edit
list (EDL) places slices of the takes on that grid; the score plays the matching arrangement;
every sound the game made during a take is re-synthesized at the exact frame it happened.

Usage:
  python Tools/Trailer/make_trailer.py --capture <out>/capture --out Steam/trailer
  (--preview renders a contact sheet of every cut instead of the movie; --uhd adds a 4K copy;
   --lang zh-CN|tr|ja|ko makes a localized version from the same takes, see locales.py)
"""
import argparse
import json
import os
import subprocess
import sys
import time

import numpy as np
from PIL import Image, ImageDraw, ImageFont

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, HERE)

import locales  # noqa: E402
from audio import dsp, inst, sfx  # noqa: E402
from audio.dsp import SR, n_of  # noqa: E402
from audio.score import BAR, BEAT, Score  # noqa: E402

LOC = None                     # the locales.LOCALES entry for --lang; None is the English original

FPS = 60
W, H = 1920, 1080
FPB = 36                       # frames per beat
SPF = SR // FPS                # audio samples per video frame (800)
PRE = 4                        # one bar in front for the McHunter Studio ident
TOTAL_BEATS = 128 + PRE
TAIL = 1.2                     # black + ring-out after the last beat
FONTS = os.path.join(ROOT, "Assets", "LegitEnough", "Resources", "LegitEnough", "Fonts")
FFMPEG = "ffmpeg"

CREAM = (241, 233, 210)
INK = (20, 16, 13)
RED = (184, 51, 42)


def Z(scale, x0, y0):
    return (scale, x0, y0)


def S(beat, beats, take, t, zoom=None, **kw):
    d = dict(beat=beat, beats=beats, take=take, t=t, zoom=zoom)
    d.update(kw)
    return d


# ====================================================================== the edit
# Beats below are written as if the trailer started at the cold open; everything is pushed back
# by PRE so the studio ident (night take 0.5-2.9 s, darkened) plays first.
EDL_MAIN = [
    # cold open: the shop at night (continuing from the ident), then the morning Samir walks in
    S(0, 8, "night", 2.90),
    S(8, 4, "samir", 0.95),                          # click the sign: OPEN. The bell lands on beat 9.
    S(12, 4, "samir", 5.05, Z(1.5, 320, 0)),         # "Is this... the place?"
    S(16, 4, "samir", 11.62, Z(1.5, 320, 0)),        # "My wife and daughter are in Lyon..."
    # the photo (the drop)
    S(20, 4, "photo", 1.55),                         # pull down the backdrop
    S(24, 4, "photo", 3.95, Z(1.5, 0, 0)),           # grab the head, turn it around
    S(28, 3, "photo", 8.12),                         # glasses off: "I can't see a thing without them."
    S(31, 1, "photo", 9.72),                         # zoom, zoom
    S(32, 4, "photo", 12.97),                        # "Like a statue." shutter
    S(36, 4, "photo", 15.37),                        # ...he blinked. The band stops.
    S(40, 4, "photo", 17.70),                        # retake, eyes open
    # the document
    S(44, 4, "editor", 0.40),                        # boot OFFICIAL-ISH 98
    S(48, 2, "editor", 2.85, Z(1.5, 400, 240)),      # template, type, cover
    S(50, 2, "editor", 5.17, Z(1.5, 400, 240)),      # typing
    S(52, 2, "editor", 13.05, Z(1.5, 400, 240)),     # the photo drops into the preview
    S(54, 2, "editor", 14.37, Z(1.5, 400, 240)),     # PRINT
    S(56, 4, "print", 5.02),                         # jam on beat 58. The band stops again.
    S(60, 4, "print", 7.80),                         # hit it, not too hard: the band is back
    S(64, 2, "print", 18.10),                        # bind it (stamp on the cut)
    S(66, 2, "print", 20.60),                        # the passport
    # the hand-over
    S(68, 2, "handover", 0.45),                      # drag the booklet to him
    S(70, 2, "handover", 4.10, Z(1.5, 320, 0)),      # "Hmm."
    S(72, 4, "handover", 8.22),                      # "Here. As agreed." Cash on beat 75.
    S(76, 4, "handover", 10.40, Z(1.5, 320, 0)),     # "If anyone asks, I was never here."
    # the world turns
    S(80, 2, "news", 1.70, Z(1.5, 320, 160)),        # headlines
    S(82, 2, "news", 10.40, Z(1.5, 0, 360)),         # your handbook is OUTDATED
    S(84, 2, "night_busy", 3.90),                    # snow, the door, another one
    S(86, 2, "night_busy", 5.20, Z(1.5, 320, 0)),    # "Please. You're my last option."
    S(88, 4, "power", 0.87),                         # power cut on beat 90: tape stop
    S(92, 4, "power", 8.28),                         # in the dark... lights back on beat 95
    # Day 1 → Day 100
    S(96, 1, "day1card", 1.00),
    S(97, 1, "tier1", 1.00),
    S(98, 1, "tier2", 1.00),
    S(99, 1, "tier3", 1.00),
    S(100, 1, "day100card", 1.00),
    S(101, 1, "dusk", 1.00),
    S(102, 2, "camera_t3", 1.00, Z(1.5, 0, 0)),
    S(104, 2, "os_t3", 1.25),
    S(106, 2, "night_busy", 12.00),
    # finale
    S(108, 4, "night", 5.00, dim=0.55),
    S(112, 4, "night", 7.40, dim=0.72),
    S(116, 12, "night", 0.50, dim=0.62, logo=True, fade_out=(126.5, 128.0)),
]

# `key` names the card for the translations in locales.py.
CARDS_MAIN = [
    dict(key="october", text="OCTOBER 2008.", at=1.0, until=3.9, style="caption"),
    dict(key="sign", text="THE SIGN SAYS PASSPORT PHOTOS.", at=4.2, until=7.85, style="caption"),
    dict(key="photo", text="TAKE THEIR PHOTO.", at=20.0, until=22.7, style="big"),
    dict(key="blink", text="PEOPLE BLINK.", at=37.2, until=39.85, style="caption"),
    dict(key="papers", text="MAKE THEIR PAPERS.", at=44.0, until=46.7, style="big"),
    dict(key="hit", text="YOU HIT IT. NOT TOO HARD.", at=60.25, until=63.85, style="caption"),
    dict(key="paid", text="GET PAID. PROBABLY.", at=72.25, until=75.9, style="caption"),
    dict(key="rules", text="RULES CHANGE.", at=80.2, until=81.9, style="caption"),
    dict(key="word", text="WORD GETS AROUND.", at=84.2, until=87.9, style="caption"),
    dict(key="studio", text="FROM BEDSHEET TO STUDIO.", at=97.0, until=101.9, style="caption"),
    dict(key="perfect", text="IT DOESN'T HAVE TO BE PERFECT.", at=108.0, until=111.95, style="big"),
    dict(key="just", text="JUST...", at=113.0, until=115.85, style="big"),
    dict(key="wishlist", text="WISHLIST ON STEAM", at=120.0, until=128.0, style="end"),
]

MUSIC_MAIN = [
    dict(kind="intro", i=0),                                   # 0  night
    dict(kind="intro", i=1),                                   # 1
    dict(kind="sneak", snaps=False),                           # 2  sign, bell, Samir walks in
    dict(kind="sneak"),                                        # 3  "Is this... the place?"
    dict(kind="sneak", brushes=True, stabs=True, fill=True),   # 4  Lyon
    dict(kind="groove", phrase=0, m=0, hit=True),              # 5  DROP: the photo
    dict(kind="groove", phrase=0, m=1),                        # 6
    dict(kind="groove", phrase=0, m=2),                        # 7
    dict(kind="groove", phrase=0, m=3),                        # 8  shutter
    dict(kind="rest", fill_from=3.0),                          # 9  he blinked (silence)
    dict(kind="groove", phrase=1, m=0, hit=True),              # 10 retake
    dict(kind="groove_b", m=0, hit=True),                      # 11 the computer
    dict(kind="groove_b", m=1),                                # 12
    dict(kind="groove_b", m=2),                                # 13
    dict(kind="groove_b", m=3, half=True),                     # 14 jam on beat 58
    dict(kind="groove_b", m=0, hit=True),                      # 15 whack
    dict(kind="groove_b", m=1, fill=True),                     # 16 bind, passport
    dict(kind="break", spy_here=True),                         # 17 the hand-over
    dict(kind="break", ding=3.0, spy_here=False),              # 18 cash on beat 75
    dict(kind="sneak", stabs=True, fill=True),                 # 19 "never here"
    dict(kind="groove", phrase=1, m=0, drive=True, snaps=True),  # 20 news
    dict(kind="groove", phrase=1, m=1, drive=True, snaps=True),  # 21 busy night
    dict(kind="groove", phrase=1, m=2, drive=True, half=True),   # 22 power cut on beat 90
    dict(kind="rest", fill_from=3.0),                          # 23 dark; lights on 95
    dict(kind="groove", phrase=0, m=0, drive=True, snaps=True, double=True, hit=True),  # 24 Day 1 → 100
    dict(kind="groove", phrase=0, m=1, drive=True, snaps=True, double=True),            # 25
    dict(kind="groove", phrase=0, m=2, drive=True, snaps=True, double=True, fill=True),  # 26
    dict(kind="hits"),                                         # 27 PERFECT
    dict(kind="rest"),                                         # 28 JUST...
    dict(kind="end"),                                          # 29 LOGO
    dict(kind="rest"),                                         # 30
    dict(kind="rest"),                                         # 31
]

# Beats where the band is choked (comic stops). The fill that brings it back is allowed through.
MUTES_MAIN = [(36.0, 38.97), (58.0, 59.97), (112.0, 115.97)]


def _later(seg):
    d = dict(seg, beat=seg["beat"] + PRE)
    if "fade_out" in d:
        d["fade_out"] = (d["fade_out"][0] + PRE, d["fade_out"][1] + PRE)
    return d


EDL = [S(0, PRE, "night", 0.50, fade_in=18, ident=True)] + [_later(s) for s in EDL_MAIN]
CARDS = [dict(c, at=c["at"] + PRE, until=c["until"] + PRE) for c in CARDS_MAIN]
MUSIC = [dict(kind="ident")] + MUSIC_MAIN
MUTES = [(a + PRE, b + PRE) for a, b in MUTES_MAIN]
TAPE_STOP = (90.0 + PRE, 0.62, 94.97 + PRE)    # start beat, slow-down seconds, silent until
LOGO_AT = 116.0 + PRE
STUDIO_END_AT = LOGO_AT + 5.0                   # the small studio logo joins the end card

AMB = {
    "night": dict(rain_in=1.0, neon=1.0, hum=0.5, traffic=1.0),
    "title": dict(rain_in=1.0, neon=1.0, hum=0.5, traffic=1.0),
    "samir": dict(rain_in=0.75, hum=1.0, traffic=0.55),
    "photo": dict(rain_in=0.45, hum=0.65, traffic=0.35),
    "editor": dict(rain_in=0.45, hum=0.8, pc=1.0, traffic=0.35),
    "print": dict(rain_in=0.7, hum=1.0, traffic=0.55),
    "handover": dict(rain_in=0.75, hum=1.0, traffic=0.55),
    "news": dict(wind=0.6, hum=0.8, pc=0.8),
    "tier1": dict(wind=0.7, hum=0.8, traffic=0.4),
    "tier2": dict(wind=0.7, hum=0.8, traffic=0.4),
    "tier3": dict(wind=0.7, hum=0.8, traffic=0.4),
    "dusk": dict(wind=0.7, hum=0.8, traffic=0.8),
    "night_busy": dict(wind=0.8, neon=1.0, hum=0.8, traffic=0.5),
    "power": dict(wind=0.9, neon=1.0, hum=0.8, traffic=0.5),
    "camera_t3": dict(wind=0.4, neon=0.5, hum=0.6),
    "os_t3": dict(wind=0.4, hum=0.6, pc=1.0),
    "day1card": dict(hum=0.25),
    "day100card": dict(hum=0.25),
    "report": dict(hum=0.25),
}
SNOWY = {"news", "tier1", "tier2", "tier3", "dusk", "night_busy", "power", "camera_t3", "os_t3"}


def beat_t(b):
    return b * BEAT


def beat_f(b):
    return int(round(b * FPB))


# ====================================================================== takes
class Take:
    def __init__(self, folder, name):
        self.name = name
        self.path = os.path.join(folder, name + ".mkv")
        self.events = []
        with open(os.path.join(folder, name + ".jsonl"), encoding="utf-8") as f:
            for line in f:
                line = line.strip()
                if line:
                    self.events.append(json.loads(line))
        self.tier = next((e.get("tier", 1) for e in self.events if e["t"] == "take"), 1)

    def frames(self, start, count):
        """Yields `count` RGB frames starting at frame `start` (frame-accurate)."""
        cmd = [FFMPEG, "-v", "error", "-ss", f"{(start - 0.5) / FPS:.6f}", "-i", self.path,
               "-frames:v", str(count), "-f", "rawvideo", "-pix_fmt", "rgb24", "-"]
        p = subprocess.Popen(cmd, stdout=subprocess.PIPE)
        size = W * H * 3
        got = 0
        last = None
        while got < count:
            buf = p.stdout.read(size)
            if len(buf) < size:
                break
            last = np.frombuffer(buf, np.uint8).reshape(H, W, 3)
            got += 1
            yield last
        p.stdout.close()
        p.wait()
        while got < count:          # pad by holding the last frame
            got += 1
            yield last

    def printer_on_at(self, frame):
        on = False
        for e in self.events:
            if e["f"] > frame:
                break
            if e["t"] == "printer":
                on = e["on"] == 1
        return on

    def power_on_at(self, frame):
        on = True
        for e in self.events:
            if e["f"] > frame:
                break
            if e["t"] == "power":
                on = e["on"] == 1
        return on

    def line_spans(self, speakers=None):
        """(first frame, end frame, text) of each speech bubble, by the same timing rule as
        Visit.Say: 1.1 s + 0.05 s per character, held 1.7-5.2 s, replaced by that speaker's next
        line. Lines logged at frame 0 were already up when the take began, so their end is unknown
        and they're left out. `speakers` limits it to the bubbles this view shows."""
        by_who = {}
        for e in self.events:
            if e["t"] == "line" and (speakers is None or e["who"] in speakers):
                by_who.setdefault(e["who"], []).append((e["f"], e["text"]))
        out = []
        for lines in by_who.values():
            lines.sort()
            for i, (f0, text) in enumerate(lines):
                f1 = f0 + int(max(1.7, min(5.2, 1.1 + len(text) * 0.05)) * FPS)
                if i + 1 < len(lines):
                    f1 = min(f1, lines[i + 1][0])
                if f0 > 0:
                    out.append((f0, f1, text))
        return sorted(out)


# ====================================================================== text
def font(name, size):
    return ImageFont.truetype(os.path.join(FONTS, name), size)


def card_font(style):
    """(font, font-pixel size, padding, border) for a text style. English and Turkish set every card
    in the game's own fonts; Chinese, Japanese and Korean use the locale's 12 px pixel font at the
    same pixel scales (one font pixel = 8, 5 or 4 screen pixels)."""
    cjk = LOC["font"] if LOC else None
    if style == "big":
        return (ImageFont.truetype(cjk, 96) if cjk else font("LegitStamp.ttf", 96)), 8, (40, 28), 8
    if style in ("end", "tag"):
        return (ImageFont.truetype(cjk, 60) if cjk else font("LegitStamp.ttf", 60)), 5, (30, 20), 0
    if style == "sub":
        return (ImageFont.truetype(cjk, 48) if cjk else font("LegitPixel.ttf", 48)), 4, (20, 10), 0
    return (ImageFont.truetype(cjk, 60) if cjk else font("LegitPixel.ttf", 60)), 5, (25, 15), 0


def crisp(img):
    """Pixel fonts must stay hard-edged: snap anti-aliased alpha."""
    a = np.array(img)
    a[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
    return a


class Card:
    def __init__(self, spec):
        self.spec = spec
        self.text = spec["text"]
        if LOC and spec.get("key") in LOC["cards"]:
            self.text = LOC["cards"][spec["key"]]
        self.style = spec["style"]
        self.f0 = beat_f(spec["at"])
        self.f1 = beat_f(spec["until"])
        fnt, px, pad, border = card_font(self.style)
        self.px = px
        bbox = fnt.getbbox(self.text)
        tw, th = bbox[2], bbox[3] - bbox[1]
        bw, bh = tw + 2 * pad[0] + 2 * border, th + 2 * pad[1] + 2 * border
        bw += (-bw) % 4
        bh += (-bh) % 4
        plate = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
        d = ImageDraw.Draw(plate)
        alpha = 0 if self.style in ("end", "tag") else (226 if self.style == "big" else 214)
        d.rectangle([0, 0, bw - 1, bh - 1], fill=INK + (alpha,))
        if border:
            d.rectangle([0, 0, bw - 1, bh - 1], outline=RED + (255,), width=border)
            d.rectangle([border * 2, border * 2, bw - 1 - border * 2, bh - 1 - border * 2], outline=RED + (110,), width=max(2, border // 4))
        txt = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
        td = ImageDraw.Draw(txt)
        ox, oy = pad[0] + border, pad[1] + border - bbox[1]
        td.text((ox + px, oy + px), self.text, font=fnt, fill=(0, 0, 0, 255))   # hard drop shadow
        td.text((ox, oy), self.text, font=fnt, fill=CREAM + (255,))
        txt_a = crisp(txt)
        base = np.array(plate).astype(np.float32)
        ta = txt_a[..., 3:4].astype(np.float32) / 255.0
        base[..., :3] = base[..., :3] * (1 - ta) + txt_a[..., :3] * ta
        base[..., 3:4] = np.maximum(base[..., 3:4], txt_a[..., 3:4])
        self.img = base.astype(np.uint8)
        # Character reveal positions (typewriter).
        self.char_x = [ox + int(fnt.getlength(self.text[:i + 1])) + px for i in range(len(self.text))]
        self.text_x0 = ox
        if self.style == "big":
            self.pos = ((W - bw) // 2 // 4 * 4, (H - bh) // 2 // 4 * 4)
        elif self.style == "end":
            self.pos = ((W - bw) // 2 // 4 * 4, 760)
        elif self.style == "tag":
            self.pos = ((W - bw) // 2 // 4 * 4, 628)     # under the logo, above WISHLIST
        else:
            self.pos = ((W - bw) // 2 // 4 * 4, 936)
        self.typing = self.style in ("caption", "end", "tag")
        self.cps = LOC["cps"] if LOC else 26.0

    def chars_at(self, f):
        return int((f - self.f0) / FPS * self.cps) + 1

    def key_frames(self):
        """Frames where a character appears (for the typewriter sound)."""
        out = []
        for i, ch in enumerate(self.text):
            f = self.f0 + int(np.ceil(i / self.cps * FPS))
            if ch != " " and f < self.f1:
                out.append(f)
        return out

    def draw(self, frame, f):
        if f < self.f0 or f >= self.f1:
            return
        img = self.img
        x, y = self.pos
        age = f - self.f0
        left = self.f1 - f
        if self.style == "big" and age < 4:
            s = min([1.3, 1.16, 1.06, 1.0][age], max(1.0, (W - 24) / img.shape[1]))
            h0, w0 = img.shape[:2]
            hh, ww = int(h0 * s), int(w0 * s)
            ri = (np.arange(hh) / s).astype(int).clip(0, h0 - 1)
            ci = (np.arange(ww) / s).astype(int).clip(0, w0 - 1)
            img = img[ri][:, ci]
            x, y = x - (ww - w0) // 2, y - (hh - h0) // 2
        if self.typing:
            n = self.chars_at(f)
            if n < len(self.text):
                cut = self.char_x[n - 1] if n > 0 else self.text_x0
                img = img.copy()
                plateless = self.style in ("end", "tag")
                img[:, cut:, 3] = np.where(img[:, cut:, 3] > 0, np.minimum(img[:, cut:, 3], 0 if plateless else 214), 0)
                # hide the not-yet-typed glyphs but keep the plate
                if not plateless:
                    plate_rgb = np.array(INK, np.uint8)
                    mask = img[:, cut:, 3] > 0
                    img[:, cut:, :3][mask] = plate_rgb
        a = 1.0
        if left < 4:
            a = left / 4.0
        blend(frame, img, x, y, a)


def wrap_text(text, fnt, max_w):
    """One line, or two: after a sentence if both halves fit, else at the space or CJK punctuation
    nearest the middle."""
    if fnt.getlength(text) <= max_w:
        return [text]
    mid = len(text) / 2

    def split(i):
        return [text[:i].rstrip(), text[i:].lstrip()]

    ends = [i + 1 for i, ch in enumerate(text[:-1]) if ch in "。？！?!" or (ch == "." and text[i + 1] == " ")]
    ends = [i for i in ends if all(fnt.getlength(p) <= max_w for p in split(i))]
    if ends:
        return split(min(ends, key=lambda k: abs(k - mid)))
    breaks = [i + 1 for i, ch in enumerate(text[:-1]) if ch in " 。，、？！?!,."]
    return split(min(breaks or range(1, len(text)), key=lambda k: abs(k - mid)))


class Subtitles:
    """Localized versions keep the game's English speech bubbles in the picture, so they put the
    translated line at the bottom while its bubble is up. Pieces of one line split by a cut merge
    into one subtitle; a line on screen for less than half a second gets none. When a caption card
    is up at the same time, the subtitle sits just above it."""
    MIN_FRAMES = 30
    # Whose bubbles each view shows (default: everyone in the shop). The camera only shows the
    # person on the stool; the computer screen covers the shop. Update if the takes are recaptured.
    SHOWN = {"photo": {"SAMIR"}, "camera_t3": {"KARIM"}, "editor": set(), "os_t3": set(), "news": set()}

    def __init__(self, takes, lines, cards):
        spans = []
        for seg in EDL:
            take = takes[seg["take"]]
            f_in = int(round(seg["t"] * FPS))
            f_out = f_in + seg["beats"] * FPB
            base = beat_f(seg["beat"])
            for f0, f1, text in take.line_spans(self.SHOWN.get(seg["take"])):
                a, b = max(f0, f_in), min(f1, f_out)
                if b > a:
                    spans.append([base + a - f_in, base + b - f_in, text])
        spans.sort()
        merged = []
        for s in spans:
            if merged and merged[-1][2] == s[2] and s[0] - merged[-1][1] <= 2:
                merged[-1][1] = max(merged[-1][1], s[1])
            else:
                merged.append(s)
        fnt, px, pad, _ = card_font("sub")
        captions = [c for c in cards if c.style == "caption"]
        self.items = []
        for f0, f1, text in merged:
            if f1 - f0 < self.MIN_FRAMES:
                continue
            if text not in lines:
                raise KeyError(f"{LOC['name']}: no translation for the line {text!r} (add it to locales.py)")
            img = self.plate(lines[text], fnt, px, pad)
            h, w = img.shape[:2]
            y = (H - 56 - h) // 4 * 4
            for c in captions:
                if c.f0 < f1 and f0 < c.f1:
                    y = min(y, (c.pos[1] - 16 - h) // 4 * 4)
            self.items.append((f0, f1, img, (W - w) // 2 // 4 * 4, y, text))

    @staticmethod
    def plate(text, fnt, px, pad):
        rows = wrap_text(text, fnt, W - 360)
        boxes = [fnt.getbbox(r) for r in rows]
        top, bottom = min(b[1] for b in boxes), max(b[3] for b in boxes)
        pitch = bottom - top + 3 * px
        tw = max(b[2] for b in boxes)
        bw = tw + 2 * pad[0] + px
        bh = (bottom - top) + pitch * (len(rows) - 1) + 2 * pad[1] + px
        bw += (-bw) % 4
        bh += (-bh) % 4
        plate = Image.new("RGBA", (bw, bh), INK + (196,))
        txt = Image.new("RGBA", (bw, bh), (0, 0, 0, 0))
        td = ImageDraw.Draw(txt)
        for i, (r, b) in enumerate(zip(rows, boxes)):
            ox = (bw - px - b[2]) // 2
            oy = pad[1] - top + i * pitch
            td.text((ox + px, oy + px), r, font=fnt, fill=(0, 0, 0, 255))
            td.text((ox, oy), r, font=fnt, fill=CREAM + (255,))
        txt_a = crisp(txt)
        base = np.array(plate).astype(np.float32)
        ta = txt_a[..., 3:4].astype(np.float32) / 255.0
        base[..., :3] = base[..., :3] * (1 - ta) + txt_a[..., :3] * ta
        base[..., 3:4] = np.maximum(base[..., 3:4], txt_a[..., 3:4])
        return base.astype(np.uint8)

    def draw(self, frame, f):
        for f0, f1, img, x, y, _ in self.items:
            if f0 <= f < f1:
                blend(frame, img, x, y, min(1.0, (f - f0 + 1) / 3.0, (f1 - f) / 3.0))


def check_glyphs(cards, lines):
    """Pillow draws a missing character as an empty box, so make sure the fonts cover every string."""
    try:
        from fontTools.ttLib import TTFont
    except ImportError:
        print("  (fontTools not installed: glyph coverage not checked)")
        return
    cmaps = {}

    def covers(fnt, text):
        path = fnt.path
        if path not in cmaps:
            cmaps[path] = set(TTFont(path, fontNumber=0).getBestCmap())
        return [ch for ch in text if not ch.isspace() and ord(ch) not in cmaps[path]]

    missing = []
    for c in cards:
        missing += [(c.text, ch) for ch in covers(card_font(c.style)[0], c.text)]
    sub = card_font("sub")[0]
    for text in lines.values():
        missing += [(text, ch) for ch in covers(sub, text)]
    if missing:
        raise SystemExit("missing glyphs: " + "; ".join(f"{ch!r} in {t!r}" for t, ch in missing))


def blend(frame, img, x, y, alpha=1.0):
    h, w = img.shape[:2]
    x0, y0 = max(0, x), max(0, y)
    x1, y1 = min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0:
        return
    src = img[y0 - y:y1 - y, x0 - x:x1 - x].astype(np.float32)
    a = src[..., 3:4] / 255.0 * alpha
    dst = frame[y0:y1, x0:x1].astype(np.float32)
    frame[y0:y1, x0:x1] = (dst * (1 - a) + src[..., :3] * a).astype(np.uint8)


# ====================================================================== logo
class Logo:
    def __init__(self, path):
        im = Image.open(path).convert("RGBA")
        self.src = np.array(im)
        self.scale = 10
        self.f0 = beat_f(LOGO_AT) - 5        # five frames of fall, impact exactly on the beat
        rng = np.random.default_rng(5)
        h, w = self.src.shape[:2]
        self.cx, self.cy = W // 2, 470
        bw, bh = w * self.scale, h * self.scale
        self.parts = []
        for _ in range(34):
            side = rng.random()
            x = self.cx + (rng.random() - 0.5) * bw * 1.05
            y = self.cy + bh * 0.5 - rng.random() * 30
            vx = (x - self.cx) / (bw / 2) * (5 + 9 * rng.random()) + rng.normal(0, 2)
            vy = -(4 + 10 * rng.random())
            size = 4 * int(rng.integers(1, 4))
            self.parts.append([x, y, vx, vy, size, side])

    def impact_frame(self):
        return self.f0 + 5

    def draw(self, frame, f):
        age = f - self.f0
        if age < 0:
            return (0, 0)
        if age < 5:
            s = self.scale * (1.55 - 0.55 * (age / 5.0) ** 2)
            alpha = 0.35 + 0.65 * age / 5.0
        else:
            s = self.scale
            alpha = 1.0
        h0, w0 = self.src.shape[:2]
        hh, ww = int(round(h0 * s)), int(round(w0 * s))
        ri = (np.arange(hh) / s).astype(int).clip(0, h0 - 1)
        ci = (np.arange(ww) / s).astype(int).clip(0, w0 - 1)
        img = self.src[ri][:, ci]
        x, y = self.cx - ww // 2, self.cy - hh // 2
        x -= x % 2
        y -= y % 2
        blend(frame, img, x, y, alpha)
        # Dust kicked up by the stamp.
        t = age - 5
        if 0 <= t < 40:
            for p in self.parts:
                px = p[0] + p[2] * t
                py = p[1] + p[3] * t + 0.45 * t * t
                a = max(0.0, 1.0 - t / 40.0) * 0.85
                sz = p[4]
                xi, yi = int(px) // 4 * 4, int(py) // 4 * 4
                if 0 <= xi < W - sz and 0 <= yi < H - sz:
                    region = frame[yi:yi + sz, xi:xi + sz].astype(np.float32)
                    frame[yi:yi + sz, xi:xi + sz] = (region * (1 - a) + np.array([150, 134, 110]) * a).astype(np.uint8)
        # Screen shake right after impact (even pixels only).
        if 0 <= t < 14:
            k = (14 - t) / 14.0
            seq = [(6, -4), (-8, 6), (6, 4), (-4, -6), (4, 2), (-2, 4), (2, -2)]
            dx, dy = seq[t % len(seq)]
            return (int(dx * k * 2) // 2 * 2, int(dy * k * 2) // 2 * 2)
        return (0, 0)


# ====================================================================== studio ident
class Studio:
    """McHunter Studio, redrawn as pixel art (studio_logo.py). The ident builds it over the
    darkened night shop in time with the music: the mark dissolves in, the blue dot pops on the
    beat, the small dot on the next off-beat, then the wordmark types itself in."""
    SCALE, END_SCALE = 8, 4
    MARK_FROM, MARK_TO = 15, 27            # frames inside the ident bar
    DOT_AT, SMALL_AT = 36, 54              # beat 1 and beat 1.5
    LETTER_FRAMES = [72 + 4 * i for i in range(8)]
    SUB_FROM = 106
    OUT_FROM, OUT_TO = 120, 138
    CREAM = (255, 246, 226)

    def __init__(self):
        import studio_logo
        self.idx = studio_logo.build()
        self.rgba = studio_logo.to_rgba(self.idx)
        h, w = self.idx.shape
        rng = np.random.default_rng(9)
        appear = np.full((h, w), 10 ** 6, int)
        cols = np.arange(w)[None, :].repeat(h, 0)
        mark = (self.idx == studio_logo.MARK) & (cols < 23)
        small = (self.idx == studio_logo.MARK) & (cols >= 23)
        appear[mark] = rng.integers(self.MARK_FROM, self.MARK_TO + 1, mark.sum())
        appear[self.idx == studio_logo.DOT] = self.DOT_AT
        appear[small] = self.SMALL_AT
        # Wordmark letters: columns split at the empty gaps between glyphs.
        word = self.idx == studio_logo.WORDMARK
        used = word.any(axis=0)
        letters, inside = [], False
        for c in range(w):
            if used[c] and not inside:
                letters.append([c, c])
                inside = True
            elif used[c]:
                letters[-1][1] = c
            elif inside:
                inside = False
        for i, (c0, c1) in enumerate(letters[:8]):
            sel = word.copy()
            sel[:, :c0] = False
            sel[:, c1 + 1:] = False
            appear[sel] = self.LETTER_FRAMES[min(i, 7)]
        sub = self.idx == studio_logo.SUB
        appear[sub] = self.SUB_FROM + rng.integers(0, 10, sub.sum())
        self.appear = appear
        self.letters = letters
        self.dot = self.idx == studio_logo.DOT
        self.small = small
        self.pos = ((W - w * self.SCALE) // 2 // 4 * 4, (H - h * self.SCALE) // 2 // 4 * 4 - 16)
        self.end_pos = ((W - w * self.END_SCALE) // 2 // 4 * 4, 900)
        self.end_f0 = beat_f(STUDIO_END_AT)
        self.end_img = np.repeat(np.repeat(self.rgba, self.END_SCALE, 0), self.END_SCALE, 1)

    def _frame_rgba(self, k):
        from scipy.ndimage import binary_dilation
        img = self.rgba.copy()
        vis = self.appear <= k
        # Dots overshoot by a pixel ring for two frames when they pop in.
        for mask, at, col in ((self.dot, self.DOT_AT, (59, 99, 172)), (self.small, self.SMALL_AT, (238, 116, 23))):
            if at <= k < at + 2:
                grow = binary_dilation(mask) & ~vis
                img[grow] = col + (255,)
                vis = vis | grow
        fresh = vis & (self.appear >= k - 1) & (self.appear <= k)
        img[fresh] = self.CREAM + (255,)
        img[~vis, 3] = 0
        return img

    def draw_ident(self, fr, k):
        dim = 0.86 if k < 116 else 0.86 * max(0.0, (144 - k) / 28.0)
        fr = (fr.astype(np.float32) * (1 - dim)).astype(np.uint8)
        alpha = 1.0 if k < self.OUT_FROM else max(0.0, 1 - (k - self.OUT_FROM) / (self.OUT_TO - self.OUT_FROM))
        if alpha <= 0:
            return fr
        img = self._frame_rgba(k)
        big = np.repeat(np.repeat(img, self.SCALE, 0), self.SCALE, 1)
        blend(fr, big, self.pos[0], self.pos[1], alpha)
        return fr

    def draw_end(self, fr, f):
        if f < self.end_f0:
            return
        a = min(1.0, (f - self.end_f0) / 18.0)
        blend(fr, self.end_img, self.end_pos[0], self.end_pos[1], a)


# ====================================================================== video
def zoom(frame, z):
    if z is None:
        return frame
    s, x0, y0 = z
    rows = (np.arange(H) / s).astype(int) + y0
    cols = (np.arange(W) / s).astype(int) + x0
    rows = rows.clip(0, H - 1)
    cols = cols.clip(0, W - 1)
    return frame[rows][:, cols]


def shift(frame, dx, dy):
    if dx == 0 and dy == 0:
        return frame
    out = np.zeros_like(frame)
    ys, yd = (slice(0, H - dy), slice(dy, H)) if dy >= 0 else (slice(-dy, H), slice(0, H + dy))
    xs, xd = (slice(0, W - dx), slice(dx, W)) if dx >= 0 else (slice(-dx, W), slice(0, W + dx))
    out[yd, xd] = frame[ys, xs]
    return out


def flash_frames(seg, take):
    f_in = int(round(seg["t"] * FPS))
    n = seg["beats"] * FPB
    out = []
    for e in take.events:
        if e["t"] == "flash" and f_in <= e["f"] < f_in + n:
            out.append(e["f"] - f_in)
    return out


def render_video(takes, logo, studio, cards, path, preview=None, subs=None):
    total = beat_f(TOTAL_BEATS) + int(TAIL * FPS)
    enc = None
    if preview is None:
        enc = subprocess.Popen([FFMPEG, "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                                "-framerate", str(FPS), "-i", "-", "-c:v", "libx264rgb", "-preset", "veryfast", "-crf", "0", path],
                               stdin=subprocess.PIPE)
    big_cards = [c for c in cards if c.style == "big"]
    f = 0
    shots = []
    t0 = time.time()
    for seg in EDL:
        take = takes[seg["take"]]
        n = seg["beats"] * FPB
        f_in = int(round(seg["t"] * FPS))
        flashes = flash_frames(seg, take)
        want = range(n) if preview is None else [min(n - 1, 12)]
        gen = take.frames(f_in, n) if preview is None else take.frames(f_in + want[0], 1)
        for k, src in zip(want, gen):
            fr = zoom(src, seg.get("zoom")).copy()
            dim = seg.get("dim", 0.0)
            for c in big_cards:
                if c.f0 <= f < c.f1:
                    dim = max(dim, 0.42)
            if dim > 0:
                fr = (fr.astype(np.float32) * (1 - dim)).astype(np.uint8)
            for fl in flashes:
                age = k - fl
                if 0 <= age < 10:
                    a = 0.92 * np.exp(-age / 2.6)
                    fr = (fr.astype(np.float32) * (1 - a) + 255 * a).astype(np.uint8)
            if seg.get("ident"):
                fr = studio.draw_ident(fr, k)
            dx = dy = 0
            if f >= logo.f0:
                dx, dy = logo.draw(fr, f)
                ia = f - logo.impact_frame()
                if 0 <= ia < 3:
                    a = [0.35, 0.18, 0.07][ia]
                    fr = (fr.astype(np.float32) * (1 - a) + 255 * a).astype(np.uint8)
            studio.draw_end(fr, f)
            for c in cards:
                c.draw(fr, f)
            if subs:
                subs.draw(fr, f)
            fr = shift(fr, dx, dy)
            fi = seg.get("fade_in")
            if fi and k < fi:
                fr = (fr.astype(np.float32) * (k / fi)).astype(np.uint8)
            fo = seg.get("fade_out")
            if fo:
                a0, a1 = beat_f(fo[0]), beat_f(fo[1])
                if f >= a0:
                    fr = (fr.astype(np.float32) * max(0.0, 1 - (f - a0) / (a1 - a0))).astype(np.uint8)
            if preview is None:
                enc.stdin.write(fr.tobytes())
                f += 1
            else:
                shots.append((seg, fr))
        if preview is not None:
            f += n
        else:
            print(f"  {seg['take']:11s} beat {seg['beat']:5.1f}  frames {f:5d}/{total}  {time.time() - t0:5.1f}s", flush=True)
    if preview is not None:
        tw, th = 480, 270
        cols = 6
        rows = (len(shots) + cols - 1) // cols
        sheet = Image.new("RGB", (tw * cols, th * rows))
        d = ImageDraw.Draw(sheet)
        for i, (seg, fr) in enumerate(shots):
            im = Image.fromarray(fr).resize((tw, th), Image.LANCZOS)
            x, y = (i % cols) * tw, (i // cols) * th
            sheet.paste(im, (x, y))
            d.rectangle([x, y, x + 210, y + 16], fill=(0, 0, 0))
            d.text((x + 3, y + 2), f"b{seg['beat']} {seg['take']} @{seg['t']}", fill=(255, 255, 0))
        sheet.save(preview)
        return
    black = np.zeros((H, W, 3), np.uint8)
    while f < total:
        enc.stdin.write(black.tobytes())
        f += 1
    enc.stdin.close()
    enc.wait()


# ====================================================================== audio
def pitched(y, p):
    if abs(p - 1.0) < 1e-3:
        return y
    n = int(len(y) / p)
    return np.interp(np.arange(n) * p, np.arange(len(y)), y)


def x_pan(x):
    return float(np.clip((x / W) * 2 - 1, -1, 1)) * 0.6


class Mixer:
    def __init__(self, seconds):
        self.n = n_of(seconds)
        self.bus = {k: dsp.stereo(self.n) for k in ("sfx", "voice", "amb", "fx", "door")}

    def put(self, bus, y, t, gain=1.0, p=0.0):
        if y.ndim == 1:
            y = dsp.pan(y, p)
        dsp.place(self.bus[bus], y, n_of(t), gain)


def beds(seconds, seed=3):
    n = n_of(seconds)
    rng = np.random.default_rng(seed)
    pc = dsp.lp(rng.standard_normal(n), 380.0, 2) * 0.02 + np.sin(2 * np.pi * 120 * np.arange(n) / SR) * 0.002
    return {
        "rain_in": sfx.rain(seconds, 11, inside=True),
        "wind": sfx.wind(seconds, 12),
        "hum": sfx.room_tone(seconds, 13, hum=0.012),
        "neon": sfx.room_tone(seconds, 14, hum=0.0, neon=0.006),
        "traffic": sfx.traffic(seconds, 15) * 0.8,
        "pc": dsp.pan(pc, 0.0),
    }


def render_audio(takes, cards, seconds):
    mx = Mixer(seconds)
    rng = np.random.default_rng(42)
    seed = [1000]

    def sd():
        seed[0] += 1
        return seed[0]

    # ---------------------------------------------------------- diegetic sound, cut with the picture
    for seg in EDL:
        take = takes[seg["take"]]
        n = seg["beats"] * FPB
        f_in = int(round(seg["t"] * FPS))
        t_seg = beat_t(seg["beat"])
        dur = seg["beats"] * BEAT
        snowy = seg["take"] in SNOWY
        evs = [e for e in take.events if f_in <= e["f"] < f_in + n]
        clicks = {e["f"] for e in evs if e["t"] == "sfx" and e["id"] in ("Click", "Pop", "Shutter", "Stamp")}
        for e in evs:
            t = t_seg + (e["f"] - f_in) / FPS
            kind = e["t"]
            if kind == "sfx":
                sid, v, p = e["id"], e["v"], e["p"]
                if sid == "Click":
                    mx.put("sfx", pitched(sfx.click(sd()), p), t, 0.55 * v)
                elif sid == "Pop":
                    mx.put("sfx", sfx.pop(sd()), t, 0.5 * v)
                elif sid == "DoorBell":
                    mx.put("sfx", sfx.bell(sd()), t, 0.9, -0.35)
                elif sid == "DoorOpen":
                    mx.put("sfx", sfx.door(sd()), t, 0.75, -0.35)
                    outside = sfx.wind(2.8, sd(), 2.2) if snowy else sfx.rain(2.8, sd(), inside=False, intensity=0.9)
                    mx.put("door", dsp.fade(outside, 0.25, 1.4), t + 0.05, 1.0)
                elif sid == "Paper":
                    mx.put("sfx", sfx.paper(sd(), p), t, 0.8 * v, 0.1)
                elif sid == "Voice":
                    mx.put("voice", sfx.voice(v, p, sd()), t, 0.85)
                elif sid == "Key":
                    mx.put("sfx", sfx.key(sd()), t, 0.6 * v)
                elif sid == "Shutter":
                    mx.put("sfx", sfx.shutter(sd()), t, 0.9, -0.1)
                elif sid == "Jam":
                    mx.put("sfx", sfx.jam(sd()), t, 1.0, 0.45)
                elif sid == "PrinterDone":
                    mx.put("sfx", sfx.printer_done(sd()), t, 0.9, 0.45)
                elif sid == "Stamp":
                    if p < 0.9:
                        mx.put("sfx", sfx.whack(sd()), t, 1.0, 0.45)
                    else:
                        mx.put("sfx", sfx.stamp(sd()), t, 0.95, 0.25)
                elif sid == "Cash":
                    mx.put("sfx", sfx.cash(sd()), t, 0.95, 0.4)
                elif sid == "Notify":
                    mx.put("sfx", sfx.notify(), t, 0.7 * v)
                elif sid == "Error":
                    mx.put("sfx", sfx.error(), t, 0.6 * v)
            elif kind == "flash":
                mx.put("sfx", sfx.flash(sd()), t, 0.75)
            elif kind == "step":
                mx.put("sfx", sfx.step(sd(), e.get("s", 1.0)), t, 0.85, x_pan(e["x"]))
            elif kind == "key":
                mx.put("sfx", sfx.key(sd()), t, 0.55, x_pan(e.get("x", W / 2)) * 0.5)
            elif kind == "pose":
                mx.put("sfx", sfx.rustle(sd()), t, 0.9, -0.2)
            elif kind == "zoom":
                mx.put("sfx", sfx.zoom_motor(sd()), t + 0.02, 0.9)
            elif kind == "backdrop":
                mx.put("sfx", sfx.roller(sd(), 1.3), t, 0.85, -0.2)
            elif kind in ("down", "up"):
                if not any(abs(e["f"] - c) <= 2 for c in clicks):
                    mx.put("sfx", sfx.mouse(sd(), up=(kind == "up")), t, 0.8)
            elif kind == "power":
                if e["on"] == 0:
                    mx.put("sfx", sfx.power_down(sd()), t, 1.0)
                else:
                    mx.put("sfx", sfx.power_up(sd()), t, 0.9)
                    mx.put("sfx", sfx.crt_on(sd()), t + 0.55, 0.5, -0.6)
        # The printer motor, where it runs inside this cut.
        on = take.printer_on_at(f_in - 1)
        start = f_in if on else None
        for e in [e for e in take.events if e["t"] == "printer" and f_in <= e["f"] < f_in + n] + [dict(f=f_in + n, t="end")]:
            if e["t"] == "printer" and e["on"] == 1 and start is None:
                start = e["f"]
            elif (e["t"] == "end" or e["on"] == 0) and start is not None:
                length = (e["f"] - start) / FPS
                if length > 0.05:
                    run = sfx.printer_run(length + 0.05, sd(), take.tier)
                    mx.put("sfx", run, t_seg + (start - f_in) / FPS, 0.9)
                start = None
        # Ambience for this cut, gated by the power during the outage.
        prof = AMB.get(seg["take"], {})
        gate = np.ones(n_of(dur))
        if seg["take"] == "power":
            for k in range(n):
                if not take.power_on_at(f_in + k):
                    gate[k * SPF:(k + 1) * SPF] = 0.0
            gate = dsp.zlp(gate, 12.0, 1)
        a, b = n_of(t_seg), n_of(t_seg + dur)
        swell = np.linspace(0.35, 1.0, b - a) if seg.get("ident") else 1.0
        for bed, g in prof.items():
            y = BEDS[bed][:, a:b] * g * swell
            if bed in ("hum", "neon", "pc"):
                y = y * gate[: y.shape[1]]
            y = dsp.fade(y, 0.012, 0.012)
            mx.bus["amb"][:, a:a + y.shape[1]] += y
    # A siren far away in the cold open.
    mx.put("amb", sfx.siren(5.5, 21), beat_t(PRE) + 0.6, 1.0)

    # ---------------------------------------------------------- cards and the logo
    for c in cards:
        t = c.f0 / FPS
        if c.style == "big":
            mx.put("fx", sfx.stamp(sd(), big=True), t, 0.9)
        if c.typing:
            for kf in c.key_frames():
                mx.put("fx", sfx.typewriter(sd()), kf / FPS, 0.8 if c.style in ("caption", "tag") else 0.9)
    t_logo = beat_t(LOGO_AT)
    mx.put("fx", inst.boom(), t_logo, 1.0)
    mx.put("fx", sfx.stamp(sd(), big=True), t_logo, 1.25)
    mx.put("fx", inst.reverse_cymbal(2.2), t_logo - 2.2, 0.55)
    mx.put("fx", inst.riser(2.35), beat_t(112.0 + PRE) + 0.02, 0.32)
    mx.put("fx", dsp.pan(inst.bass_slide(110.0, 73.4, 0.55), 0.0), beat_t(37.0 + PRE), 0.5)
    mx.put("fx", sfx.bell(sd()), beat_t(125.5 + PRE), 0.55, -0.3)
    # The ident: a glassy blip per letter of the wordmark as it types itself, a sparkle as the mark builds.
    for i, fk in enumerate(Studio.LETTER_FRAMES):
        mx.put("fx", sfx.blip(sd(), 1650.0 + 90.0 * i), fk / FPS, 0.5, -0.2 + 0.05 * i)
    mx.put("fx", sfx.shimmer(sd()), Studio.MARK_FROM / FPS, 0.6)

    # ---------------------------------------------------------- the score
    score = Score(MUSIC, seconds)
    stems = score.render()
    music = mix_music(stems, seconds)
    music = radio_intro(music)
    start, slow, until = TAPE_STOP
    stopped = dsp.tape_stop(music, n_of(beat_t(start)), slow)
    k = n_of(beat_t(until))
    music = np.concatenate([stopped[:, :k], music[:, k:]], axis=1)
    env = np.ones(music.shape[1])
    for a, b in MUTES:
        i0, i1 = n_of(beat_t(a)), n_of(beat_t(b))
        r = n_of(0.012)
        env[i0:i1] = 0.0
        env[max(0, i0 - r):i0] = np.minimum(env[max(0, i0 - r):i0], np.linspace(1, 0, i0 - max(0, i0 - r)))
        env[i1:i1 + r] = np.minimum(env[i1:i1 + r], np.linspace(0, 1, len(env[i1:i1 + r])))
    music = music * env
    return mx, music


def mix_music(st, seconds):
    n = st["bass"].shape[1]
    t = np.arange(n) / SR
    plate = dsp.reverb_ir(2.3, predelay=0.03, damp=0.45, width=1.0, seed=31)
    room = dsp.reverb_ir(0.55, predelay=0.008, damp=0.7, width=0.7, seed=32)

    bass = dsp.hp(st["bass"], 38.0, 2)
    bass = dsp.peak_eq(bass, 150.0, 3.5, 0.8)        # the wooden body
    bass = dsp.shelf(bass, 90.0, 1.0, high=False)
    bass = dsp.compressor(bass, -22.0, 3.0, 0.004, 0.12) * dsp.undb(-6.5)
    kit = dsp.hp(st["kit"], 40.0, 2) * dsp.undb(-4.0)
    ride = dsp.shelf(dsp.hp(st["ride"], 320.0, 2), 9000.0, -2.5) * dsp.undb(-6.0)
    snaps = dsp.hp(st["snaps"], 350.0, 2) * dsp.undb(-3.0)
    trem = 0.5 + 0.5 * np.sin(2 * np.pi * 4.6 * t)
    rhodes = st["rhodes"] * dsp.undb(-10.5)
    rhodes = np.vstack([rhodes[0] * (0.62 + 0.38 * trem), rhodes[1] * (0.62 + 0.38 * (1 - trem))])
    rhodes = dsp.shelf(rhodes, 3500.0, -2.0)
    rhodes = dsp.peak_eq(rhodes, 450.0, -2.5, 1.0)
    motor = 1.0 - 0.3 * (0.5 + 0.5 * np.sin(2 * np.pi * 5.1 * t))
    vibes = st["vibes"] * motor * dsp.undb(-8.0)
    pad = dsp.lp(st["pad"], 1800.0, 2) * dsp.undb(3.0)

    dry = bass + kit + ride + snaps + rhodes + vibes + pad
    send_plate = vibes * 0.32 + rhodes * 0.22 + snaps * 0.35 + ride * 0.12 + kit * 0.1 + pad * 0.3
    send_room = kit * 0.25 + bass * 0.08 + snaps * 0.2
    bus = dry + dsp.wet(send_plate, plate, 0.85) + dsp.wet(send_room, room, 0.6)
    bus = dsp.compressor(bus, -18.0, 2.0, 0.02, 0.25, makeup_db=1.5)
    bus = dsp.saturate(bus * 0.85, 1.1)
    bus = dsp.shelf(bus, 10000.0, -1.5)
    bus = bus + sfx.vinyl(seconds, 77, 0.8)[:, :n]
    return bus


def radio_intro(music):
    """Bars 0-3 come from a small radio in the shop; bar 4 opens up into the full mix."""
    a = n_of(beat_t(16.0 + PRE))
    b = n_of(beat_t(20.0 + PRE))
    radio = dsp.bp(np.mean(music, axis=0), 320.0, 3000.0, 2)
    radio = dsp.saturate(radio * 2.2, 1.6) * 0.32
    radio = dsp.pan(radio, 0.15)
    warm = dsp.hp(dsp.lp(music, 5500.0, 2), 110.0, 2)
    w = np.zeros(music.shape[1])
    # The ident plays clean, then slides into the radio over the last half beat before the shop.
    p0, p1 = n_of(beat_t(PRE - 0.5)), n_of(beat_t(PRE))
    w[:p0] = 1.0
    w[p0:p1] = np.linspace(1, 0, p1 - p0)
    w[a:b] = np.linspace(0, 1, b - a)
    w[b:] = 1.0
    w1 = np.clip(w * 2, 0, 1)            # radio → warm in the first half of bar 4
    w2 = np.clip(w * 2 - 1, 0, 1)        # warm → full in the second half
    out = radio * (1 - w1) + warm * (w1 * (1 - w2)) + music * w2
    return out


def master(mx, music, seconds):
    sfx_bus = mx.bus["sfx"]
    voice = dsp.hp(mx.bus["voice"], 120.0, 2)
    voice = dsp.peak_eq(voice, 2200.0, 3.0, 0.9)
    voice = dsp.compressor(voice, -24.0, 3.0, 0.003, 0.08, makeup_db=3.0)
    amb = mx.bus["amb"] + mx.bus["door"]
    fx = mx.bus["fx"]
    room = dsp.reverb_ir(0.5, predelay=0.006, damp=0.75, width=0.6, seed=41)
    sfx_wet = dsp.wet(sfx_bus * 0.5 + voice * 0.35, room, 0.35)
    hall = dsp.reverb_ir(2.6, predelay=0.03, damp=0.5, seed=42)
    fx_wet = dsp.wet(fx, hall, 0.3)

    # Level the score, then let dialogue and key sounds push it down a little.
    m_l = dsp.lufs(music)
    music = music * dsp.undb(-19.0 - m_l)
    key = voice * 2.0 + sfx_bus
    music = dsp.duck(music, key, depth_db=-6.0, thresh_db=-36.0, attack=0.01, release=0.35)

    mix = music + (sfx_bus + sfx_wet) * dsp.undb(0.0) + voice * dsp.undb(6.0) + amb * dsp.undb(-2.0) + (fx + fx_wet) * dsp.undb(-5.0)
    mix = dsp.hp(mix, 25.0, 2)
    mix = dsp.compressor(mix, -16.0, 1.8, 0.015, 0.3, makeup_db=0.0)
    for _ in range(3):
        cur = dsp.lufs(dsp.limiter(mix, -1.5))
        mix = mix * dsp.undb(-14.0 - cur)
    out = dsp.limiter(mix, -1.5)
    return out


def write_wav(path, x):
    from scipy.io import wavfile
    wavfile.write(path, SR, np.clip(x.T, -1, 1).astype(np.float32))


# ====================================================================== main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True, help="the capture folder written by trailer.ps1")
    ap.add_argument("--out", default=os.path.join(ROOT, "Steam", "trailer"))
    ap.add_argument("--work", default=None, help="folder for intermediates (default: <capture>/../work)")
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--audio-only", action="store_true")
    ap.add_argument("--uhd", action="store_true", help="also write a 3840x2160 copy (nearest-neighbour)")
    ap.add_argument("--reuse-video", action="store_true", help="keep work/trailer_video.mkv, redo sound + mux only")
    ap.add_argument("--lang", default="en", choices=["en"] + sorted(locales.LOCALES),
                    help="text language (default: the English original); see locales.py")
    args = ap.parse_args()
    global LOC
    LOC = locales.LOCALES.get(args.lang)
    tag = "" if LOC is None else "_" + args.lang          # file suffix: legit_enough_trailer_zh-CN_1080p60.mp4
    folder = os.path.join(args.capture, "takes")
    work = args.work or os.path.join(os.path.dirname(os.path.abspath(args.capture)), "work" + tag)
    os.makedirs(work, exist_ok=True)
    os.makedirs(args.out, exist_ok=True)
    names = sorted({s["take"] for s in EDL})
    takes = {nm: Take(folder, nm) for nm in names}
    cards = [Card(c) for c in CARDS]
    subs = None
    if LOC:
        print(f"language: {LOC['name']}", flush=True)
        cards.append(Card(dict(key="tagline", text=LOC["tagline"], at=LOGO_AT + 1.5, until=TOTAL_BEATS, style="tag")))
        check_glyphs(cards, LOC["lines"])
        subs = Subtitles(takes, LOC["lines"], cards)
        for f0, f1, _, _, _, text in subs.items:
            print(f"  subtitle {f0 / FPB:6.2f}-{f1 / FPB:6.2f}  {text}", flush=True)
    logo = Logo(os.path.join(args.capture, "art", "logo_dark.png"))
    studio = Studio()
    seconds = TOTAL_BEATS * BEAT + TAIL

    if args.preview:
        path = os.path.join(work, "preview.png")
        render_video(takes, logo, studio, cards, None, preview=path, subs=subs)
        print("preview:", path)
        return

    global BEDS
    t0 = time.time()
    print("audio: ambience beds", flush=True)
    BEDS = beds(seconds + 1.0)
    print("audio: events, cards, score", flush=True)
    mx, music = render_audio(takes, cards, seconds)
    print(f"audio: master ({time.time() - t0:.0f}s)", flush=True)
    out = master(mx, music, seconds)
    wav = os.path.join(work, "trailer_audio.wav")
    write_wav(wav, out)
    np.save(os.path.join(work, "music.npy"), music.astype(np.float32))
    print(f"  integrated {dsp.lufs(out):.1f} LUFS, true peak {dsp.true_peak_db(out):.1f} dBTP", flush=True)
    if args.audio_only:
        return

    video = os.path.join(work, "trailer_video.mkv")
    if args.reuse_video and os.path.exists(video):
        print("video: reusing", video, flush=True)
    else:
        print("video: compositing", flush=True)
        render_video(takes, logo, studio, cards, video, subs=subs)
    final = os.path.join(args.out, f"legit_enough_trailer{tag}_1080p60.mp4")
    enc = [FFMPEG, "-y", "-v", "error", "-i", video, "-i", wav,
           "-vf", "scale=out_color_matrix=bt709:out_range=tv:flags=accurate_rnd+full_chroma_int,format=yuv420p",
           "-c:v", "libx264", "-preset", "slow", "-tune", "animation", "-x264-params", "deblock=-1,-1", "-crf", "10", "-profile:v", "high",
           "-g", "120", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
           "-c:a", "aac", "-b:a", "320k", "-ar", "48000", "-movflags", "+faststart", "-shortest", final]
    print("encode:", final, flush=True)
    subprocess.run(enc, check=True)
    if args.uhd:
        final4k = os.path.join(args.out, f"legit_enough_trailer{tag}_2160p60.mp4")
        enc4k = [FFMPEG, "-y", "-v", "error", "-i", video, "-i", wav,
                 "-vf", "scale=3840:2160:flags=neighbor,scale=out_color_matrix=bt709:out_range=tv,format=yuv420p",
                 "-c:v", "libx264", "-preset", "medium", "-tune", "animation", "-x264-params", "deblock=-1,-1", "-crf", "14", "-profile:v", "high", "-level", "5.2",
                 "-g", "120", "-colorspace", "bt709", "-color_primaries", "bt709", "-color_trc", "bt709", "-color_range", "tv",
                 "-c:a", "aac", "-b:a", "320k", "-ar", "48000", "-movflags", "+faststart", "-shortest", final4k]
        print("encode:", final4k, flush=True)
        subprocess.run(enc4k, check=True)
    print(f"done in {time.time() - t0:.0f}s")


BEDS = {}

if __name__ == "__main__":
    main()
