"""LEGIT ENOUGH, Day 1: a 20 s gameplay short for the demo launch, vertical (1080x1920: Reels,
Shorts, TikTok) and horizontal (1920x1080), from one timeline.

The game's first customer, start to finish, in the order it happens: the sign flips to OPEN, Samir
walks in and asks for a French passport, you take his photo, make his papers and get paid. Every
shot is a straight slice of a take (trailer.ps1 records them) at 1:1 pixels: no zooms, shakes or
retouching. The cuts land on the beat of the score (audio/score.py, played at 120 BPM, so a beat is
30 frames). The only text is one caption per step and the end card.

Vertical: the picture is a 1080 x 1080 window of the take, unscaled, with the caption in a band
above it and the call to action under it, clear of the platforms' own overlays. Horizontal: the
whole frame, with the caption under it in the style of the game's hint notes.

Usage:
  powershell -ExecutionPolicy Bypass -File Tools/Trailer/trailer.ps1 -Out <folder> -Only night,samir,photo,editor,print,handover
  python Tools/Trailer/make_day1.py --capture <folder>/capture [--preview] [--only vertical|horizontal]
Writes Steam/trailer/legit_enough_day1_vertical_1080x1920.mp4 and legit_enough_day1_1080p60.mp4
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
SECONDS = TOTAL_BEATS * BEAT
SRC_W, SRC_H = 1920, 1080
FORMATS = {"vertical": (1080, 1920), "horizontal": (1920, 1080)}
CREAM, INK = T.CREAM, T.INK
AMBER = (232, 168, 64)
EDGE = (52, 42, 34)
STAMP_TTF = os.path.join(T.FONTS, "LegitStamp.ttf")

# The score's timing lives in module globals, read at render time.
SC.BPM, SC.BEAT, SC.BAR, SC.SWING = BPM, BEAT, 4 * BEAT, 0.62


def bf(b):
    return int(round(b * FPB))


def bt(b):
    return b * BEAT


def S(b, n, take, f, vx, **kw):
    """A shot on beat b, n beats long, from frame f of `take`. vx: left edge of the vertical
    window (1080 x 1080, unscaled) in take pixels."""
    d = dict(b=b, n=n, take=take, f=f, vx=vx)
    d.update(kw)
    return d


# ====================================================================== the edit
SHOP = 520          # the vertical window on the shop: the door, the counter, the speech bubble
CAMERA = 80         # the camera's live view and the photo review
EDL = [
    S(0, 4, "samir", 47, SHOP, fade_in=4),      # the sign flips to OPEN on beat 1, the bell, Samir in the door
    S(4, 4, "samir", 566, SHOP),                # "I need a French passport."
    S(8, 4, "photo", 266, CAMERA),              # turn his head
    S(12, 4, "photo", 885, CAMERA),             # "Like a statue." The shutter on beat 13: he blinked.
    S(16, 4, "editor", 790, 560),               # his photo goes in, PRINT
    S(20, 4, "print", 1250, 660),               # the passport: the whole data page, a strip of the red cover
    S(24, 8, "handover", 571, SHOP),            # the cash on beat 25, "If anyone asks, I was never here."
    S(32, 8, "night", 120, 40, end=True, fade_in=10),   # closed for the night: the end card
]
FADE_OUT = (39.0, 40.0)

# One caption per step: (first beat, last beat, lines when stacked, single line).
CAPTIONS = [
    (0, 8, ["DAY 1.", "YOUR FIRST CUSTOMER."], "DAY 1. YOUR FIRST CUSTOMER."),
    (8, 16, ["TAKE HIS PHOTO."], "TAKE HIS PHOTO."),
    (16, 24, ["MAKE HIS PAPERS."], "MAKE HIS PAPERS."),
    (24, 32, ["GET PAID."], "GET PAID."),
]
CTA = "FREE DEMO OUT NOW ON STEAM"
END_AT = 32.0

# Vertical layout (y in output pixels). Instagram, TikTok and Shorts draw their own controls over
# roughly the top 140 px, the bottom 360 px and a column on the right, so the words stay in between.
V_CAPTION = (140, 400)          # the caption band, text centred in it
V_PICTURE = 410                 # top of the 1080 x 1080 window
V_CTA = 1530                    # centre of the call to action under the picture

# The arrangement, one entry per bar (audio/score.py kinds).
MUSIC = [
    dict(kind="sneak"),                                              # 0 the sign, the bell
    dict(kind="sneak", brushes=True, stabs=True, fill=True),         # 1 "I need a French passport."
    dict(kind="groove_b", m=0, hit=True, drive=True),                # 2 the photo
    dict(kind="groove_b", m=1, drive=True),                          # 3 the shutter
    dict(kind="groove_b", m=2, hit=True, drive=True),                # 4 the papers
    dict(kind="groove_b", m=3, drive=True, fill=True),               # 5 the passport
    dict(kind="groove", phrase=0, m=0, snaps=True, hit=True),        # 6 the cash
    dict(kind="groove", phrase=0, m=1, snaps=True, fill=True),       # 7 "If anyone asks..."
    dict(kind="end", tag=True),                                      # 8 the logo
    dict(kind="rest"),                                               # 9 the chord rings out
]
CASH_DING = 25.0                # the vibes' two-note ding from the trailer, on the cash


# ====================================================================== pictures
def blend(fr, img, x, y, alpha=1.0):
    H, W = fr.shape[:2]
    h, w = img.shape[:2]
    x0, y0, x1, y1 = max(0, x), max(0, y), min(W, x + w), min(H, y + h)
    if x1 <= x0 or y1 <= y0 or alpha <= 0:
        return
    src = img[y0 - y:y1 - y, x0 - x:x1 - x].astype(np.float32)
    a = src[..., 3:4] / 255.0 * alpha
    dst = fr[y0:y1, x0:x1].astype(np.float32)
    fr[y0:y1, x0:x1] = (dst * (1 - a) + src[..., :3] * a).astype(np.uint8)


def darken(fr, a):
    return (fr.astype(np.float32) * (1 - a)).astype(np.uint8)


def crisp_text(img):
    a = np.array(img)
    a[..., 3] = np.where(a[..., 3] >= 128, 255, 0)
    return a


def text_image(text, size, color, shadow=True):
    """A line in the game's stamp font, on its 12 px grid (size a multiple of 12), with a hard
    shadow one font pixel down and right."""
    f = ImageFont.truetype(STAMP_TTF, size)
    px = size // 12 if shadow else 0
    b = f.getbbox(text)
    w, h = b[2] - b[0] + px, b[3] - b[1] + px
    img = Image.new("RGBA", (w, h), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    if px:
        d.text((-b[0] + px, -b[1] + px), text, font=f, fill=(0, 0, 0, 255))
    d.text((-b[0], -b[1]), text, font=f, fill=color + (255,))
    return crisp_text(img)


def fit_size(lines, max_w, sizes=(108, 96, 84, 72, 60, 48)):
    """The biggest size (on the font's 12 px grid) at which every line fits max_w."""
    for size in sizes:
        f = ImageFont.truetype(STAMP_TTF, size)
        if max(f.getbbox(t)[2] - f.getbbox(t)[0] for t in lines) <= max_w:
            return size
    return sizes[-1]


def stack(images, gap):
    w = max(i.shape[1] for i in images)
    h = sum(i.shape[0] for i in images) + gap * (len(images) - 1)
    out = np.zeros((h, w, 4), np.uint8)
    y = 0
    for i in images:
        x = (w - i.shape[1]) // 2
        out[y:y + i.shape[0], x:x + i.shape[1]] = i
        y += i.shape[0] + gap
    return out


def note_image(text, size=48):
    """A caption in the style of the game's hint notes: a dark plate with an amber edge on the left."""
    t = text_image(text, size, CREAM)
    pad_x, pad_y, bar = 28, 14, 8
    w, h = t.shape[1] + 2 * pad_x + bar, t.shape[0] + 2 * pad_y
    w += (-w) % 4
    h += (-h) % 4
    out = np.zeros((h, w, 4), np.uint8)
    out[..., :3] = INK
    out[..., 3] = 230
    out[:, :bar, :3] = AMBER
    out[:, :bar, 3] = 255
    blend_rgba(out, t, bar + pad_x, pad_y)
    return out


def blend_rgba(dst, src, x, y):
    h, w = src.shape[:2]
    a = src[..., 3:4].astype(np.float32) / 255.0
    reg = dst[y:y + h, x:x + w].astype(np.float32)
    reg[..., :3] = reg[..., :3] * (1 - a) + src[..., :3] * a
    reg[..., 3:4] = np.maximum(reg[..., 3:4], src[..., 3:4])
    dst[y:y + h, x:x + w] = reg.astype(np.uint8)


def nearest(img, s):
    return np.repeat(np.repeat(img, s, 0), s, 1)


class Layout:
    """Everything that isn't the take: captions, the call to action, the end card."""

    def __init__(self, fmt, capture):
        self.fmt = fmt
        W, H = FORMATS[fmt]
        self.W, self.H = W, H
        logo = np.array(Image.open(os.path.join(capture, "art", "logo_dark.png")).convert("RGBA"))
        studio = studio_logo.to_rgba(studio_logo.build())
        if fmt == "vertical":
            size = fit_size([t for c in CAPTIONS for t in c[2]], 960)      # one size for every caption
            self.captions = [(bf(b0), bf(b1), stack([text_image(t, size, CREAM) for t in lines], size // 4))
                             for b0, b1, lines, _ in CAPTIONS]
            self.cta = text_image(CTA, fit_size([CTA], 860, (60, 48)), AMBER)
            self.logo = nearest(logo, 6)
            self.studio = nearest(studio, 3)
        else:
            self.captions = [(bf(b0), bf(b1), note_image(line)) for b0, b1, _, line in CAPTIONS]
            self.cta = text_image(CTA, 60, AMBER)
            self.logo = nearest(logo, 10)
            self.studio = nearest(studio, 4)
        self.end_f = bf(END_AT)

    def base(self):
        """The frame the picture goes into."""
        if self.fmt == "horizontal":
            return None
        fr = np.zeros((self.H, self.W, 3), np.uint8)
        fr[:] = INK
        fr[V_PICTURE - 4:V_PICTURE, :] = EDGE
        fr[V_PICTURE + 1080:V_PICTURE + 1084, :] = EDGE
        return fr

    def place(self, src, shot):
        if self.fmt == "horizontal":
            return src.copy()
        fr = self.base()
        x0 = shot["vx"]
        fr[V_PICTURE:V_PICTURE + 1080] = src[:, x0:x0 + 1080]
        return fr

    def picture_box(self):
        if self.fmt == "horizontal":
            return (0, 0, self.W, self.H)
        return (0, V_PICTURE, self.W, V_PICTURE + 1080)

    def draw(self, fr, f):
        end_age = f - self.end_f
        if self.fmt == "vertical":
            for f0, f1, img in self.captions:
                if f0 <= f < f1:
                    blend(fr, img, (self.W - img.shape[1]) // 2, (V_CAPTION[0] + V_CAPTION[1] - img.shape[0]) // 2)
            # A little left of centre: the platforms' buttons run down the right edge.
            blend(fr, self.cta, 530 - self.cta.shape[1] // 2, V_CTA - self.cta.shape[0] // 2)
            if end_age >= 0:
                a = min(1.0, end_age / 12.0)
                blend(fr, self.logo, (self.W - self.logo.shape[1]) // 2, (V_CAPTION[0] + V_CAPTION[1] - self.logo.shape[0]) // 2, a)
                # The studio on the shop floor, clear of the counter's screens.
                sx = (self.W - self.studio.shape[1]) // 2 // 4 * 4
                blend(fr, self.studio, sx, V_PICTURE + 640 - self.studio.shape[0] // 2, min(1.0, max(0.0, (end_age - 30) / 18.0)))
            return fr
        for f0, f1, img in self.captions:
            if f0 <= f < f1:
                blend(fr, img, (self.W - img.shape[1]) // 2, self.H - 16 - img.shape[0])
        if end_age >= 0:
            a = min(1.0, end_age / 12.0)
            # Under the door's CLOSED sign, not over it.
            blend(fr, self.logo, (self.W - self.logo.shape[1]) // 2, 290, a)
            blend(fr, self.cta, (self.W - self.cta.shape[1]) // 2, 690, min(1.0, max(0.0, (end_age - 15) / 12.0)))
            sx = (self.W - self.studio.shape[1]) // 2 // 4 * 4
            blend(fr, self.studio, sx, 840, min(1.0, max(0.0, (end_age - 30) / 18.0)))
        return fr


def frame_for(layout, shot, src, k, ff):
    fr = layout.place(src, shot)
    if shot.get("end"):
        # Closed for the night: the shop sits back so the end card reads.
        x0, y0, x1, y1 = layout.picture_box()
        fr[y0:y1, x0:x1] = darken(fr[y0:y1, x0:x1], (0.62 if layout.fmt == "horizontal" else 0.45) * min(1.0, k / 12.0))
    if shot.get("fade_in") and k < shot["fade_in"]:
        x0, y0, x1, y1 = layout.picture_box()
        fr[y0:y1, x0:x1] = darken(fr[y0:y1, x0:x1], 1 - k / shot["fade_in"])
    fr = layout.draw(fr, ff)
    if ff >= bf(FADE_OUT[0]):
        fr = darken(fr, min(1.0, (ff - bf(FADE_OUT[0])) / (bf(FADE_OUT[1]) - bf(FADE_OUT[0]))))
    return fr


def render_video(fmt, takes, capture, path=None, preview=None):
    W, H = FORMATS[fmt]
    total = bf(TOTAL_BEATS)
    layout = Layout(fmt, capture)
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
                enc.stdin.write(frame_for(layout, shot, src, k, f).tobytes())
                f += 1
            print(f"  {fmt:10s} {shot['take']:9s} beat {shot['b']:4.1f}  frame {f:4d}/{total}  {time.time() - t0:5.1f}s", flush=True)
        else:
            for k in sorted({min(n - 1, 20), n // 2, n - 2}):
                src = next(take.frames(shot["f"] + k, 1))
                sheet.append((shot, f + k, frame_for(layout, shot, src, k, f + k)))
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


# ====================================================================== sound
def render_music():
    score = SC.Score(MUSIC, SECONDS)
    stems = score.render()

    def vib(b, note, beats, v):
        score.add("vibes", int(b // 4), b % 4, inst.vibes(inst.hz(inst.midi(note)), beats * BEAT, v, score.rng), 1.0, exact=True)

    vib(CASH_DING, "A5", 0.5, 0.7)
    vib(CASH_DING + 0.5, "D6", 2.0, 0.75)
    return T.mix_music(stems, SECONDS)


def render_sound(takes):
    """Every sound the game made inside each shot, rebuilt at its frame, plus the room around it."""
    mx = T.Mixer(SECONDS)
    beds = T.beds(SECONDS + 1.0)
    seed = [7000]

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
                    mx.put("sfx", T.pitched(sfx.click(sd()), p), t, 0.55 * v)
                elif sid == "Pop":
                    mx.put("sfx", sfx.pop(sd()), t, 0.5 * v)
                elif sid == "DoorBell":
                    mx.put("sfx", sfx.bell(sd()), t, 0.9, -0.35)
                elif sid == "DoorOpen":
                    mx.put("sfx", sfx.door(sd()), t, 0.75, -0.35)
                    mx.put("door", dsp.fade(sfx.rain(2.8, sd(), inside=False, intensity=0.9), 0.25, 1.4), t + 0.05, 1.0)
                elif sid == "Paper":
                    mx.put("sfx", sfx.paper(sd(), p), t, 0.8 * v, 0.1)
                elif sid == "Voice":
                    mx.put("voice", sfx.voice(v, p, sd()), t, 0.85)
                elif sid == "Shutter":
                    mx.put("sfx", sfx.shutter(sd()), t, 0.9, -0.1)
                elif sid == "Stamp":
                    mx.put("sfx", sfx.whack(sd()) if p < 0.9 else sfx.stamp(sd()), t, 1.0, 0.3)
                elif sid == "Cash":
                    mx.put("sfx", sfx.cash(sd()), t, 0.95, 0.3)
                elif sid == "PrinterDone":
                    mx.put("sfx", sfx.printer_done(sd()), t, 0.9, 0.45)
            elif kind == "flash":
                mx.put("sfx", sfx.flash(sd()), t, 0.75)
            elif kind == "step":
                mx.put("sfx", sfx.step(sd(), e.get("s", 1.0)), t, 0.85, T.x_pan(e["x"]))
            elif kind == "key":
                mx.put("sfx", sfx.key(sd()), t, 0.55)
            elif kind == "pose":
                mx.put("sfx", sfx.rustle(sd()), t, 0.9, -0.2)
            elif kind == "zoom":
                mx.put("sfx", sfx.zoom_motor(sd()), t + 0.02, 0.9)
            elif kind == "backdrop":
                mx.put("sfx", sfx.roller(sd(), 1.3), t, 0.85, -0.2)
            elif kind in ("down", "up"):
                if not any(abs(e["f"] - c) <= 2 for c in clicks):
                    mx.put("sfx", sfx.mouse(sd(), up=(kind == "up")), t, 0.8)
        # The printer motor, where it runs inside this shot.
        start = f_in if take.printer_on_at(f_in - 1) else None
        for e in [e for e in evs if e["t"] == "printer"] + [dict(f=f_in + n, t="end")]:
            if e["t"] == "printer" and e["on"] == 1 and start is None:
                start = e["f"]
            elif (e["t"] == "end" or e["on"] == 0) and start is not None:
                if e["f"] - start > 3:
                    mx.put("sfx", sfx.printer_run((e["f"] - start) / FPS + 0.05, sd(), take.tier), t_shot + (start - f_in) / FPS, 0.8)
                start = None
        # The room: rain on the window, the tube hum, the street.
        a, b = n_of(t_shot), n_of(t_shot + shot["n"] * BEAT)
        for bed, g in T.AMB.get(shot["take"], {}).items():
            y = dsp.fade(beds[bed][:, a:b] * g, 0.012, 0.012)
            mx.bus["amb"][:, a:a + y.shape[1]] += y
    return mx


def master(mx, music):
    sfx_bus = mx.bus["sfx"]
    voice = dsp.compressor(dsp.peak_eq(dsp.hp(mx.bus["voice"], 120.0, 2), 2200.0, 3.0, 0.9), -24.0, 3.0, 0.003, 0.08, makeup_db=3.0)
    amb = mx.bus["amb"] + mx.bus["door"]
    sfx_wet = dsp.wet(sfx_bus * 0.5 + voice * 0.35, dsp.reverb_ir(0.5, predelay=0.006, damp=0.75, width=0.6, seed=41), 0.3)
    music = music * dsp.undb(-16.0 - dsp.lufs(music))
    music = dsp.duck(music, voice * 2.0 + sfx_bus, depth_db=-3.0, thresh_db=-34.0, attack=0.01, release=0.25)
    mix = music + (sfx_bus + sfx_wet) * dsp.undb(-1.0) + voice * dsp.undb(4.0) + amb * dsp.undb(-6.0)
    mix = dsp.compressor(dsp.hp(mix, 25.0, 2), -16.0, 2.0, 0.01, 0.2)
    for _ in range(3):
        mix = mix * dsp.undb(-14.0 - dsp.lufs(dsp.limiter(mix, -1.5)))
    return dsp.limiter(mix, -1.5)


# ====================================================================== main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True, help="the capture folder written by trailer.ps1")
    ap.add_argument("--out", default=os.path.join(ROOT, "Steam", "trailer"))
    ap.add_argument("--work", default=None, help="folder for intermediates (default: <capture>/../work_day1)")
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--only", choices=sorted(FORMATS), help="render one format")
    args = ap.parse_args()
    work = args.work or os.path.join(os.path.dirname(os.path.abspath(args.capture)), "work_day1")
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
    out = master(render_sound(takes), render_music())
    # The sound fades out with the picture over the last beat.
    i0 = n_of(bt(FADE_OUT[0]))
    out[:, i0:] *= np.linspace(1.0, 0.0, out.shape[1] - i0) ** 1.5
    wav = os.path.join(work, "day1_audio.wav")
    T.write_wav(wav, out)
    print(f"  {dsp.lufs(out):.1f} LUFS, true peak {dsp.true_peak_db(out):.1f} dBTP ({time.time() - t0:.0f}s)", flush=True)
    names = {"vertical": "legit_enough_day1_vertical_1080x1920.mp4", "horizontal": "legit_enough_day1_1080p60.mp4"}
    for fmt in fmts:
        video = os.path.join(work, f"day1_{fmt}.mkv")
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
