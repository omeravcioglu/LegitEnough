"""LEGIT ENOUGH, Departures: a 20 s short for the demo launch, vertical (1080x1920: Reels, Shorts,
TikTok) and horizontal (1920x1080), from one timeline.

The demo's Day 2, which the trailer never shows: four new clients walk in, one after another, and
each says where they're going and why. An airport departures board keeps the list: every client
flips in a row (destination, remark), and at the end the whole board flips to the call to action.
Their own lines from the game run as subtitles beside the picture. The music is the score's band
(bass, Rhodes, vibes, brushes) playing a bossa nova at 120 BPM instead of the trailer's noir caper,
opened and closed by an airport chime. The picture is the game at 1.5x (vertical) or 1x
(horizontal), nearest-neighbour, with no effects.

Usage:
  powershell -ExecutionPolicy Bypass -File Tools/Trailer/trailer.ps1 -Out <folder> -Script day2
  python Tools/Trailer/make_departures.py --capture <folder>/capture [--preview] [--only vertical|horizontal]
Writes Steam/trailer/legit_enough_departures_vertical_1080x1920.mp4 and legit_enough_departures_1080p60.mp4
(--preview: one contact sheet per format instead of the movies).
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

import make_day1 as D  # noqa: E402  (blend, darken, crisp text, nearest)
import make_trailer as T  # noqa: E402  (takes, ambience beds, the music bus, colours)
import studio_logo  # noqa: E402
from audio import dsp, inst, sfx  # noqa: E402
from audio import score as SC  # noqa: E402
from audio.dsp import SR, n_of  # noqa: E402

FPS = 60
BPM = 120.0
BEAT = 60.0 / BPM               # 0.5 s
FPB = 30                        # frames per beat
TOTAL_BEATS = 42                # 21 s; the last beat fades to black
SECONDS = TOTAL_BEATS * BEAT
SRC_W, SRC_H = 1920, 1080
FORMATS = {"vertical": (1080, 1920), "horizontal": (1920, 1080)}
CREAM, INK, AMBER, EDGE = D.CREAM, D.INK, D.AMBER, D.EDGE
RED = (226, 92, 72)
GREEN = (124, 206, 112)
TILE = (31, 28, 25)
SPLIT = (12, 11, 10)
PANEL = (11, 10, 9)
STAMP_TTF = os.path.join(T.FONTS, "LegitStamp.ttf")
PIXEL_TTF = os.path.join(T.FONTS, "LegitPixel.ttf")


def bf(b):
    return int(round(b * FPB))


def bt(b):
    return b * BEAT


def S(b, n, take, f, v, h, **kw):
    """A shot on beat b, n beats long, from frame f of `take`. v: top-left (x, y) of the vertical
    window in take pixels (720 x 540, shown at 1.5x); h: left edge of the horizontal window
    (1280 x 1080, shown at 1x)."""
    d = dict(b=b, n=n, take=take, f=f, v=v, h=h)
    d.update(kw)
    return d


# ====================================================================== the edit
COUNTER_V, COUNTER_H = (760, 0), 300     # the client at the counter and their speech bubble
FACE_V, FACE_H = (120, 230), 0           # the camera: the face in the viewfinder, then the photo
EDL = [
    S(0, 8, "elif", 608, COUNTER_V, COUNTER_H),          # "My grandmother left me her flat in Izmir. And her cat..."
    S(8, 8, "kenji", 557, COUNTER_V, COUNTER_H),         # "My brother opened a noodle bar in Osaka..."
    S(16, 8, "viktor", 554, COUNTER_V, COUNTER_H),       # "There's a flight to Paris this evening. You have four hours..."
    S(24, 8, "anja", 634, COUNTER_V, COUNTER_H),         # "There's a warehouse job in Hamburg. Night shifts..."
    S(32, 2, "elif_photo", 330, FACE_V, FACE_H),         # "Like a statue." Flash.
    S(34, 2, "viktor_photo", 262, FACE_V, FACE_H),       # "Not moving." Flash.
    S(36, 6, "busy", 60, (0, 120), 0, end=True),          # Elif and Viktor on the bench, two orders on the counter
]
# The board, one row per client as they start talking: (beat, destination, remark, remark colour, clock).
ROWS = [
    (0, "IZMIR", "+1 CAT", AMBER, "09:05"),
    (8, "OSAKA", "FULL GAME", RED, "09:45"),
    (16, "PARIS", "4 HOURS", RED, "10:20"),
    (24, "HAMBURG", "NIGHT SHIFT", AMBER, "11:30"),
]
END_AT = 32.0
END_ROWS = [("LEGIT ENOUGH", CREAM), ("FREE DEMO", AMBER), ("NOW BOARDING", GREEN), ("ON STEAM", CREAM)]
END_ROWS_H = [(("LEGIT", CREAM), ("ENOUGH", CREAM)), (("FREE", AMBER), ("DEMO", AMBER)),
              (("NOW", GREEN), ("BOARDING", GREEN)), (("ON", CREAM), ("STEAM", CREAM))]
FADE_OUT = (TOTAL_BEATS - 1.0, float(TOTAL_BEATS))


# ====================================================================== the board
def glyph(ch, size, color):
    """One tile letter in the stamp font. Its zero has a slash that reads as 8 on a tile: O instead."""
    ch = "O" if ch == "0" else ch
    f = ImageFont.truetype(STAMP_TTF, size)
    b = f.getbbox(ch)
    img = Image.new("RGBA", (max(1, b[2] - b[0]), max(1, b[3] - b[1])), (0, 0, 0, 0))
    ImageDraw.Draw(img).text((-b[0], -b[1]), ch, font=f, fill=color + (255,))
    return D.crisp_text(img)


FLAP_CHARS = "ABCDEFGHIJKLMNOPRSTUVWXYZ0123456789"
FLIP_FRAMES = 14                 # how long one tile spins before it settles
STAGGER = 2                      # frames between neighbouring tiles


class Board:
    """A split-flap departures board. Each tile spins through letters for FLIP_FRAMES and settles,
    left to right, whenever its row is given new text."""

    H_BOX = (24, 24, 616, 668)          # the horizontal panel

    def __init__(self, fmt):
        self.fmt = fmt
        if fmt == "vertical":
            self.box = (24, 150, 1056, 550)
            self.tw, self.th, self.gap, self.size = 48, 60, 4, 48
            self.cols = 19
            x0 = (1080 - (self.cols * (self.tw + self.gap) - self.gap)) // 2
            self.lines = [(x0, 254 + 72 * r, self.cols) for r in range(4)]
            self.head = (60, 172)
            self.clock = (1080 - 60 - 5 * 40 + 4, 168, 36, 48, 36)          # x, y, tile w, tile h, font size
        else:
            self.box = Board.H_BOX
            self.tw, self.th, self.gap, self.size = 40, 52, 4, 36
            self.cols = 11
            x0 = 64
            self.lines = []
            for r in range(4):
                y = 148 + 128 * r
                self.lines += [(x0, y, 7), (x0, y + 58, 11)]
            self.head = (64, 48)
            self.clock = (616 - 40 - 5 * 36 + 4, 46, 32, 44, 30)
        self.cells = {}            # (line, col) -> [(frame, char, colour)]
        self.clock_cells = {}
        self.cache = {}
        self.last_key = None
        self.last_img = None
        self.bg = self._panel()

    def _panel(self):
        x0, y0, x1, y1 = self.box
        img = np.zeros((y1 - y0, x1 - x0, 4), np.uint8)
        img[..., :3] = PANEL
        img[..., 3] = 255
        img[:4, :, :3] = EDGE
        img[-4:, :, :3] = EDGE
        img[:, :4, :3] = EDGE
        img[:, -4:, :3] = EDGE
        # Header: a little plane, DEPARTURES, an amber rule under it.
        plane = ["....X.....", "....XX....", "X...XXX...", "XXXXXXXXXX", "X...XXX...", "....XX....", "....X....."]
        hx, hy = self.head[0] - x0, self.head[1] - y0
        s = 4 if self.fmt == "vertical" else 3
        for r, row in enumerate(plane):
            for c, ch in enumerate(row):
                if ch == "X":
                    img[hy + 6 + r * s:hy + 6 + (r + 1) * s, hx + c * s:hx + (c + 1) * s, :3] = AMBER
        title = D.text_image("DEPARTURES", 48 if self.fmt == "vertical" else 36, CREAM, shadow=False)
        D.blend_rgba(img, title, hx + 10 * s + 18, hy + 4)
        rule_y = (self.lines[0][1] - y0) - (24 if self.fmt == "vertical" else 22)
        img[rule_y:rule_y + 4, 28:-28, :3] = AMBER
        return img

    def set_text(self, line, text, color, f0, align="left"):
        x, y, n = self.lines[line]
        text = text[:n]
        start = 0 if align == "left" else (n - len(text)) // 2
        for c in range(n):
            ch = text[c - start] if 0 <= c - start < len(text) else " "
            self.cells.setdefault((line, c), []).append((f0 + STAGGER * c, ch, color))

    def set_clock(self, text, f0):
        for c, ch in enumerate(text):
            self.clock_cells.setdefault(c, []).append((f0 + STAGGER * c, ch, AMBER))

    @staticmethod
    def _state(changes, f, salt):
        """What a tile shows at frame f: (char, colour, spinning)."""
        cur, col = " ", CREAM
        for k, (t0, ch, color) in enumerate(changes):
            if f < t0:
                break
            if f < t0 + FLIP_FRAMES and (ch != " " or cur != " "):
                i = (f - t0) // 2 + salt * 7 + k * 3
                return FLAP_CHARS[i % len(FLAP_CHARS)], color, True
            cur, col = ch, color
        return cur, col, False

    def _glyph(self, ch, size, color, dim=False):
        key = (ch, size, color, dim)
        if key not in self.cache:
            c = tuple(int(v * 0.72) for v in color) if dim else color
            self.cache[key] = glyph(ch, size, c)
        return self.cache[key]

    def _tile(self, img, x, y, w, h, ch, color, size, spinning):
        img[y:y + h, x:x + w, :3] = TILE
        img[y:y + h, x:x + w, 3] = 255
        if ch != " ":
            g = self._glyph(ch, size, color, spinning)
            gx, gy = x + (w - g.shape[1]) // 2, y + (h - g.shape[0]) // 2
            D.blend_rgba(img, g, gx, gy)
        m = y + h // 2 - 1
        img[m:m + 2, x:x + w, :3] = SPLIT

    def draw(self, fr, f):
        states = []
        for (line, c), changes in sorted(self.cells.items()):
            states.append(((line, c),) + self._state(changes, f, line * 31 + c))
        clock = [(c,) + self._state(ch, f, 97 + c) for c, ch in sorted(self.clock_cells.items())]
        key = (tuple(states), tuple(clock))
        if key != self.last_key:
            img = self.bg.copy()
            x0, y0 = self.box[0], self.box[1]
            for (line, c), ch, color, spin in states:
                lx, ly, _ = self.lines[line]
                self._tile(img, lx - x0 + c * (self.tw + self.gap), ly - y0, self.tw, self.th, ch, color, self.size, spin)
            cx, cy, cw, chh, cs = self.clock
            for c, ch, color, spin in clock:
                self._tile(img, cx - x0 + c * (cw + 4), cy - y0, cw, chh, ch, color, cs, spin)
            self.last_key, self.last_img = key, img
        D.blend(fr, self.last_img, self.box[0], self.box[1])

    def spins(self):
        """(frame, tiles) for every flip, for the clatter."""
        out = {}
        for changes in list(self.cells.values()) + list(self.clock_cells.values()):
            prev = " "
            for t0, ch, _ in changes:
                if ch != " " or prev != " ":
                    out[t0] = out.get(t0, 0) + 1
                prev = ch
        return sorted(out.items())


# ====================================================================== subtitles
def wrap(text, font, max_w):
    lines, cur = [], ""
    for word in text.split():
        t = (cur + " " + word).strip()
        if font.getlength(t) <= max_w or not cur:
            cur = t
        else:
            lines.append(cur)
            cur = word
    lines.append(cur)
    return lines


def line_dur(text):
    return max(1.7, min(5.2, 1.1 + 0.05 * len(text)))


class Subtitles:
    """Each client's lines, as the game shows them in the speech bubble, set big beside the picture."""

    def __init__(self, fmt, takes):
        self.fmt = fmt
        self.items = []
        for shot in EDL:
            if not shot.get("subs", True):
                continue
            take = takes[shot["take"]]
            f_in, n, f_out = shot["f"], bf(shot["n"]), bf(shot["b"])
            said = [e for e in take.events if e["t"] == "line"]
            for i, e in enumerate(said):
                end = e["f"] + int(line_dur(e["text"]) * FPS)
                if i + 1 < len(said):
                    end = min(end, said[i + 1]["f"])
                a, b = max(e["f"], f_in), min(end, f_in + n)
                if b - a < 20:
                    continue
                self.items.append((f_out + a - f_in, f_out + b - f_in, e["who"], e["text"]))
        self.cache = {}

    def image(self, who, text):
        key = (who, text)
        if key in self.cache:
            return self.cache[key]
        if self.fmt == "vertical":
            name_size, size, max_w, pitch = 36, 48, 980, 52
        else:
            name_size, size, max_w, pitch = 30, 36, 520, 40
        name = D.text_image(who, name_size, AMBER)
        f = ImageFont.truetype(PIXEL_TTF, size)
        lines = wrap(text, f, max_w)
        w = max([name.shape[1]] + [int(f.getlength(t)) + 4 for t in lines])
        h = name.shape[0] + 14 + pitch * len(lines)
        img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
        d = ImageDraw.Draw(img)
        d.fontmode = "1"
        y = name.shape[0] + 14
        px = size // 12
        for t in lines:
            tb = f.getbbox(t)
            d.text((-tb[0] + px, y - tb[1] + px), t, font=f, fill=(0, 0, 0, 255))
            d.text((-tb[0], y - tb[1]), t, font=f, fill=CREAM + (255,))
            y += pitch
        out = D.crisp_text(img)
        D.blend_rgba(out, name, 0, 0)
        self.cache[key] = out
        return out

    def draw(self, fr, f, at):
        for f0, f1, who, text in self.items:
            if f0 <= f < f1:
                img = self.image(who, text)
                x, y = at
                if x is None:
                    x = (fr.shape[1] - img.shape[1]) // 2
                D.blend(fr, img, x, y)


# ====================================================================== layout
V_PICTURE = 780          # top of the vertical picture: 1080 x 810 (720 x 540 of the take at 1.5x)
V_SUBS = (None, 572)     # subtitles: centred, under the board
H_PICTURE = 640          # left edge of the horizontal picture: 1280 x 1080 at 1x
H_SUBS = (64, 700)


class Layout:
    def __init__(self, fmt, capture, takes):
        self.fmt = fmt
        self.W, self.H = FORMATS[fmt]
        self.board = Board(fmt)
        for k, (b, dest, remark, color, clock) in enumerate(ROWS):
            if fmt == "vertical":
                # One line: the destination in cream, the remark in its own colour.
                self.board.set_text(k, dest.ljust(7) + " " + remark, CREAM, bf(b))
                for c in range(8, self.board.lines[k][2]):
                    t0, ch, _ = self.board.cells[(k, c)][-1]
                    self.board.cells[(k, c)][-1] = (t0, ch, color)
            else:
                self.board.set_text(2 * k, dest, CREAM, bf(b))
                self.board.set_text(2 * k + 1, remark, color, bf(b) + 4)
            self.board.set_clock(clock, bf(b))
        end = bf(END_AT)
        if fmt == "vertical":
            for k, (text, color) in enumerate(END_ROWS):
                self.board.set_text(k, text, color, end + 6 * k, align="center")
        else:
            for k, ((a, ca), (b2, cb)) in enumerate(END_ROWS_H):
                self.board.set_text(2 * k, a, ca, end + 6 * k)
                self.board.set_text(2 * k + 1, b2, cb, end + 6 * k + 4)
        self.subs = Subtitles(fmt, takes)
        logo = np.array(Image.open(os.path.join(capture, "art", "logo_dark.png")).convert("RGBA"))
        self.logo = D.nearest(logo, 4 if fmt == "vertical" else 6)
        self.studio = D.nearest(studio_logo.to_rgba(studio_logo.build()), 3)

    def picture(self, src, shot):
        if self.fmt == "vertical":
            x0, y0 = shot["v"]
            region = src[y0:y0 + 540, x0:x0 + 720]
            return np.repeat(np.repeat(region, 3, 0), 3, 1)[::2, ::2]          # 1.5x, nearest
        x0 = shot["h"]
        return src[:, x0:x0 + 1280]

    def compose(self, src, shot, f, k):
        fr = np.zeros((self.H, self.W, 3), np.uint8)
        fr[:] = INK
        pic = self.picture(src, shot)
        if shot.get("fade_in") and k < shot["fade_in"]:
            pic = D.darken(pic, 1 - k / shot["fade_in"])
        if shot.get("end"):
            pic = D.darken(pic, 0.25 * min(1.0, k / 12.0))
        if self.fmt == "vertical":
            fr[V_PICTURE - 4:V_PICTURE] = EDGE
            fr[V_PICTURE:V_PICTURE + 810] = pic
            fr[V_PICTURE + 810:V_PICTURE + 814] = EDGE
            box = (0, V_PICTURE, self.W, V_PICTURE + 810)
        else:
            fr[:, H_PICTURE:] = pic
            fr[:, H_PICTURE - 4:H_PICTURE] = EDGE
            box = (H_PICTURE, 0, self.W, self.H)
        self.board.draw(fr, f)
        self.subs.draw(fr, f, V_SUBS if self.fmt == "vertical" else H_SUBS)
        if shot.get("end"):
            # The logo where the subtitles were; the studio under it (horizontal) or on the empty floor (vertical).
            k_end = max(0, f - bf(shot["b"]))
            a = min(1.0, k_end / 14.0)
            s_a = min(1.0, max(0.0, (k_end - 24) / 18.0))
            lw, lh = self.logo.shape[1], self.logo.shape[0]
            sw, sh = self.studio.shape[1], self.studio.shape[0]
            if self.fmt == "vertical":
                D.blend(fr, self.logo, (self.W - lw) // 2, V_SUBS[1] + (V_PICTURE - 12 - V_SUBS[1] - lh) // 2, a)
                D.blend(fr, self.studio, (self.W - sw - 64) // 4 * 4, box[3] - sh - 30, s_a)
            else:
                cx = (Board.H_BOX[0] + Board.H_BOX[2]) // 2
                D.blend(fr, self.logo, cx - lw // 2, 700, a)
                D.blend(fr, self.studio, (cx - sw // 2) // 4 * 4, 720 + lh, s_a)
        if f >= bf(FADE_OUT[0]):
            fr = D.darken(fr, min(1.0, (f - bf(FADE_OUT[0])) / (bf(FADE_OUT[1]) - bf(FADE_OUT[0]))))
        return fr


def render_video(fmt, takes, capture, path=None, preview=None):
    W, H = FORMATS[fmt]
    total = bf(TOTAL_BEATS)
    layout = Layout(fmt, capture, takes)
    enc = None
    if preview is None:
        enc = subprocess.Popen(["ffmpeg", "-y", "-v", "error", "-f", "rawvideo", "-pix_fmt", "rgb24", "-s", f"{W}x{H}",
                                "-framerate", str(FPS), "-i", "-", "-c:v", "libx264rgb", "-preset", "veryfast", "-crf", "0", path],
                               stdin=subprocess.PIPE)
    sheet, f, t0 = [], 0, time.time()
    for shot in EDL:
        take = takes[shot["take"]]
        n = bf(shot["n"])
        if preview is None:
            for k, src in enumerate(take.frames(shot["f"], n)):
                enc.stdin.write(layout.compose(src, shot, f, k).tobytes())
                f += 1
            print(f"  {fmt:10s} {shot['take']:12s} beat {shot['b']:4.1f}  frame {f:4d}/{total}  {time.time() - t0:5.1f}s", flush=True)
        else:
            for k in sorted({min(n - 1, 24), n // 2, n - 2}):
                src = next(take.frames(shot["f"] + k, 1))
                sheet.append((shot, f + k, layout.compose(src, shot, f + k, k)))
            f += n
    if preview is not None:
        tw, th = (270, 480) if fmt == "vertical" else (480, 270)
        cols = 6 if fmt == "vertical" else 4
        rows = (len(sheet) + cols - 1) // cols
        img = Image.new("RGB", (tw * cols, (th + 16) * rows))
        d = ImageDraw.Draw(img)
        for i, (shot, ff, fr) in enumerate(sheet):
            x, y = (i % cols) * tw, (i // cols) * (th + 16)
            img.paste(Image.fromarray(fr).resize((tw, th), Image.LANCZOS), (x, y + 16))
            d.text((x + 3, y + 2), f"b{ff / FPB:.1f} {shot['take']} f{shot['f'] + ff - bf(shot['b'])}", fill=(255, 255, 0))
        img.save(preview)
        return
    enc.stdin.close()
    enc.wait()


# ====================================================================== music: a bossa nova
PROG = ["Dm9", "Bb13", "Gm9", "A7b9"]
ROOTS = {"Dm9": ("D2", "A2"), "Bb13": ("Bb1", "F2"), "Gm9": ("G1", "D2"), "A7b9": ("A1", "E2")}
CLAVE = [(0.0, 1.5, 3.0), (1.0, 2.5)]                      # bossa clave over two bars, on the rim
COMP = [[(0.0, 0.45), (1.5, 0.45), (3.0, 0.9)], [(0.5, 0.45), (2.0, 0.9), (3.5, 0.45)]]
CHIME_UP = [(0.0, "D5"), (0.5, "F5"), (1.0, "A5")]        # "attention, please"
CHIME_DOWN = [(0.0, "A5"), (0.5, "F5"), (1.0, "D5")]


def bossa(seconds, bars, end_bar):
    """Stems for mix_music: the band as a bossa nova, straight eighths, the clave on the rim."""
    rng = np.random.default_rng(12)
    n = n_of(seconds)
    st = {k: dsp.stereo(n) for k in ("bass", "kit", "ride", "snaps", "rhodes", "vibes", "pad")}

    def put(stem, beat, y, gain=1.0, p=0.0):
        t = beat * BEAT + rng.normal(0, 0.003)
        dsp.place(st[stem], dsp.pan(y, p) if y.ndim == 1 else y, n_of(max(0.0, t)), gain)

    def chord(beat, name, beats, v, stem="rhodes", gain=0.5):
        for i, note in enumerate(SC.VOICINGS[name]):
            f = inst.hz(inst.midi(note))
            y = inst.rhodes(f, beats * BEAT, v) if stem == "rhodes" else inst.vibes(f, beats * BEAT, v, rng)
            put(stem, beat, y, gain, -0.25 + 0.17 * i)

    for bar in range(bars):
        b0 = 4 * bar
        name = PROG[bar % 4]
        if bar == 0:
            # The chime, then the band's chord under it.
            for off, note in CHIME_UP:
                put("vibes", b0 + off, inst.vibes(inst.hz(inst.midi(note)), 2.5 * BEAT, 0.7, rng), 1.0)
            chord(b0 + 2.0, "Dm9", 2.0, 0.45)
            for k in range(4):
                put("ride", b0 + 2.0 + 0.5 * k, inst.brush_tap(0.35 + 0.2 * (k % 2), rng), 0.8, 0.3)
            continue
        if bar >= end_bar:
            if bar == end_bar:
                for off, note in CHIME_DOWN:
                    put("vibes", b0 + off, inst.vibes(inst.hz(inst.midi(note)), 3.0 * BEAT, 0.7, rng), 1.0)
                put("bass", b0, inst.bass(inst.hz(inst.midi("D2")), 6 * BEAT, 0.9, rng), 1.0)
                chord(b0, "DmM9", 7.0, 0.55)
                put("kit", b0, inst.kick(0.6, rng), 0.8)
                put("kit", b0, inst.rim(0.8, rng), 0.7)
                put("vibes", b0 + 3.0, inst.vibes(inst.hz(inst.midi("D6")), 3.0 * BEAT, 0.5, rng), 0.8)
            continue
        root, fifth = ROOTS[name]
        nxt = ROOTS[PROG[(bar + 1) % 4]][0]
        # Bass: root, fifth, and a push into the next bar.
        put("bass", b0, inst.bass(inst.hz(inst.midi(root)), 1.4 * BEAT, 0.85, rng), 1.0)
        put("bass", b0 + 2.0, inst.bass(inst.hz(inst.midi(fifth)), 1.2 * BEAT, 0.75, rng), 1.0)
        put("bass", b0 + 3.5, inst.bass(inst.hz(inst.midi(nxt)), 0.45 * BEAT, 0.6, rng), 1.0)
        # Kit: soft kick on 1 and 3, the clave on the rim, a shaker of brush taps.
        put("kit", b0, inst.kick(0.45, rng), 0.7)
        put("kit", b0 + 2.0, inst.kick(0.35, rng), 0.6)
        for off in CLAVE[bar % 2]:
            put("kit", b0 + off, inst.rim(0.7, rng), 0.55, 0.15)
        for k in range(8):
            put("ride", b0 + 0.5 * k, inst.brush_tap(0.28 + 0.22 * (k % 2), rng), 0.75, 0.3)
        # Rhodes on the off-beats, bossa style.
        for off, length in COMP[bar % 2]:
            chord(b0 + off, name, length, 0.55, gain=0.42)
        # A little vibes answer every other bar, out of the way of the voices.
        if bar % 4 == 3:
            for off, note in ((2.0, "E5"), (2.5, "C#5"), (3.0, "A4")):
                put("vibes", b0 + off, inst.vibes(inst.hz(inst.midi(note)), 0.9 * BEAT, 0.45, rng), 0.7)
    return st


def render_music():
    end_bar = int(END_AT // 4)
    stems = bossa(SECONDS, TOTAL_BEATS // 4 + 1, end_bar)
    return T.mix_music(stems, SECONDS)


# ====================================================================== sound
def clatter(tiles, seed):
    """A split-flap board turning: a dry rattle of small flaps, longer for more tiles."""
    rng = np.random.default_rng(seed)
    dur = FLIP_FRAMES / FPS + STAGGER * min(tiles, 20) / FPS + 0.05
    y = np.zeros(n_of(dur + 0.05))
    hits = int(10 * max(1, tiles) ** 0.7)
    for _ in range(hits):
        t = rng.random() * dur
        k = n_of(0.006)
        click = rng.standard_normal(k) * np.exp(-np.arange(k) / (k / 5))
        i = n_of(t)
        y[i:i + k] += click[: len(y) - i] * (0.3 + 0.7 * rng.random())
    y = dsp.bp(y, 1800.0, 7500.0, 2)
    return dsp.pan(y * 0.25, 0.0)


def render_sound(takes, board_spins):
    mx = T.Mixer(SECONDS)
    beds = T.beds(SECONDS + 1.0)
    seed = [9000]

    def sd():
        seed[0] += 1
        return seed[0]

    for shot in EDL:
        take = takes[shot["take"]]
        n = bf(shot["n"])
        f_in = shot["f"]
        t_shot = bt(shot["b"])
        evs = [e for e in take.events if f_in <= e["f"] < f_in + n]
        clicks = {e["f"] for e in evs if e["t"] == "sfx" and e["id"] in ("Click", "Pop", "Shutter", "Stamp")}
        for e in evs:
            t = t_shot + (e["f"] - f_in) / FPS
            kind = e["t"]
            if kind == "sfx":
                sid, v, p = e["id"], e["v"], e["p"]
                if sid == "Click":
                    mx.put("sfx", T.pitched(sfx.click(sd()), p), t, 0.5 * v)
                elif sid == "Pop":
                    mx.put("sfx", sfx.pop(sd()), t, 0.5 * v)
                elif sid == "DoorBell":
                    mx.put("sfx", sfx.bell(sd()), t, 0.8, -0.35)
                elif sid == "DoorOpen":
                    mx.put("sfx", sfx.door(sd()), t, 0.7, -0.35)
                    mx.put("door", dsp.fade(sfx.rain(2.8, sd(), inside=False, intensity=0.9), 0.25, 1.4), t + 0.05, 1.0)
                elif sid == "Paper":
                    mx.put("sfx", sfx.paper(sd(), p), t, 0.8 * v, 0.1)
                elif sid == "Voice":
                    mx.put("voice", sfx.voice(v, p, sd()), t, 0.85)
                elif sid == "Shutter":
                    mx.put("sfx", sfx.shutter(sd()), t, 0.9, -0.1)
                elif sid == "Cash":
                    mx.put("sfx", sfx.cash(sd()), t, 0.95, 0.3)
            elif kind == "flash":
                mx.put("sfx", sfx.flash(sd()), t, 0.75)
            elif kind == "step":
                mx.put("sfx", sfx.step(sd(), e.get("s", 1.0)), t, 0.8, T.x_pan(e["x"]))
            elif kind == "pose":
                mx.put("sfx", sfx.rustle(sd()), t, 0.9, -0.2)
            elif kind == "zoom":
                mx.put("sfx", sfx.zoom_motor(sd()), t + 0.02, 0.9)
            elif kind == "backdrop":
                mx.put("sfx", sfx.roller(sd(), 1.3), t, 0.85, -0.2)
            elif kind in ("down", "up"):
                if not any(abs(e["f"] - c) <= 2 for c in clicks):
                    mx.put("sfx", sfx.mouse(sd(), up=(kind == "up")), t, 0.7)
        a, b = n_of(t_shot), n_of(t_shot + shot["n"] * BEAT)
        prof = dict(rain_in=0.45, hum=0.65, traffic=0.35) if "photo" in shot["take"] else dict(rain_in=0.75, hum=1.0, traffic=0.55)
        for bed, g in prof.items():
            y = dsp.fade(beds[bed][:, a:b] * g, 0.012, 0.012)
            mx.bus["amb"][:, a:a + y.shape[1]] += y
    for f, tiles in board_spins:
        mx.put("fx", clatter(tiles, sd()), f / FPS, 1.0)
    return mx


def master(mx, music):
    sfx_bus = mx.bus["sfx"]
    voice = dsp.compressor(dsp.peak_eq(dsp.hp(mx.bus["voice"], 120.0, 2), 2200.0, 3.0, 0.9), -24.0, 3.0, 0.003, 0.08, makeup_db=3.0)
    amb = mx.bus["amb"] + mx.bus["door"]
    fx = mx.bus["fx"]
    sfx_wet = dsp.wet(sfx_bus * 0.5 + voice * 0.35, dsp.reverb_ir(0.5, predelay=0.006, damp=0.75, width=0.6, seed=41), 0.3)
    music = music * dsp.undb(-16.0 - dsp.lufs(music))
    music = dsp.duck(music, voice * 2.0 + sfx_bus, depth_db=-4.0, thresh_db=-34.0, attack=0.01, release=0.3)
    mix = music + (sfx_bus + sfx_wet) * dsp.undb(-1.0) + voice * dsp.undb(4.0) + amb * dsp.undb(-7.0) + fx * dsp.undb(-6.0)
    mix = dsp.compressor(dsp.hp(mix, 25.0, 2), -16.0, 2.0, 0.01, 0.2)
    # The board's clicks are sharp: a lower ceiling keeps the true peak under -1 dBTP.
    for _ in range(3):
        mix = mix * dsp.undb(-14.0 - dsp.lufs(dsp.limiter(mix, -2.2)))
    return dsp.limiter(mix, -2.2)


# ====================================================================== main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True, help="the capture folder written by trailer.ps1 -Script day2")
    ap.add_argument("--out", default=os.path.join(ROOT, "Steam", "trailer"))
    ap.add_argument("--work", default=None, help="folder for intermediates (default: <capture>/../work_departures)")
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--only", choices=sorted(FORMATS), help="render one format")
    args = ap.parse_args()
    work = args.work or os.path.join(os.path.dirname(os.path.abspath(args.capture)), "work_departures")
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
    spins = Layout("vertical", args.capture, takes).board.spins()
    out = master(render_sound(takes, spins), render_music())
    i0 = n_of(bt(FADE_OUT[0]))
    out[:, i0:] *= np.linspace(1.0, 0.0, out.shape[1] - i0) ** 1.5
    wav = os.path.join(work, "departures_audio.wav")
    T.write_wav(wav, out)
    print(f"  {dsp.lufs(out):.1f} LUFS, true peak {dsp.true_peak_db(out):.1f} dBTP ({time.time() - t0:.0f}s)", flush=True)
    names = {"vertical": "legit_enough_departures_vertical_1080x1920.mp4", "horizontal": "legit_enough_departures_1080p60.mp4"}
    for fmt in fmts:
        video = os.path.join(work, f"departures_{fmt}.mkv")
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
