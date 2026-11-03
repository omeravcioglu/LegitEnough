"""LEGIT ENOUGH, the demo teaser: 20 s at 150 BPM, vertical (1080x1920: Reels, Shorts, TikTok) and
horizontal (1920x1080), from one timeline.

A teaser in five beats, all from the demo's Day 2 (trailer.ps1 -Script day2jobs), none of it in the trailer:
  the hook     Viktor: "French passport. Today."
  the premise  YOU RUN A BACK-ROOM PASSPORT SHOP. (the sign flips to OPEN)
  the loop     REAL FACES. / FAKE PAPERS. / REAL MONEY. on the drop: Elif's photo, her Turkish passport, her cash,
               then a run of one-beat shots (the printer jams and gets hit, the booklet stamp, the other clients)
  the stakes   the band stops: Viktor reads his passport and finds last year's background
  the title    the logo lands; FREE DEMO OUT NOW ON STEAM
Every caption is three words or fewer and holds 1.6 s or more. The picture is the game unscaled
(horizontal) or at 1.5x (vertical: a 720 x 1080 window of the take), cut on the beat, with no shakes,
zooms or retouching. The score is the trailer's band (audio/score.py) played at 150 BPM.

Usage:
  powershell -ExecutionPolicy Bypass -File Tools/Trailer/trailer.ps1 -Out <folder> -Script day2jobs
  python Tools/Trailer/make_teaser.py --capture <folder>/capture --night <a capture with a night take>
      [--preview] [--only vertical|horizontal]
Writes Steam/trailer/legit_enough_teaser_vertical_1080x1920.mp4 and legit_enough_teaser_1080p60.mp4.
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
from audio.dsp import n_of  # noqa: E402

FPS = 60
BPM = 150.0
BEAT = 60.0 / BPM               # 0.4 s
FPB = 24                        # frames per beat
TOTAL_BEATS = 50                # 20 s
SECONDS = TOTAL_BEATS * BEAT
FORMATS = {"vertical": (1080, 1920), "horizontal": (1920, 1080)}
CREAM, INK, AMBER, EDGE = D.CREAM, D.INK, D.AMBER, D.EDGE
RED = (226, 92, 72)
STAMP_TTF = os.path.join(T.FONTS, "LegitStamp.ttf")
V_TOP = 150                     # the vertical picture: 1080 x 1620 from y 150 (a 720 x 1080 window at 1.5x)

SC.BPM, SC.BEAT, SC.BAR, SC.SWING = BPM, BEAT, 4 * BEAT, 0.62


def bf(b):
    return int(round(b * FPB))


def bt(b):
    return b * BEAT


def S(b, n, take, f, vx, **kw):
    """A shot on beat b, n beats long, from frame f of `take`; vx: left edge of the vertical window."""
    d = dict(b=b, n=n, take=take, f=f, vx=vx)
    d.update(kw)
    return d


# Vertical windows (left edge of 720 take pixels).
COUNTER, DOOR, CAMERA, PREVIEW, BOOKLET, PRINTER, NIGHT = 760, 520, 120, 880, 340, 1100, 40

EDL = [
    S(0, 6, "viktor", 415, COUNTER),                 # "French passport. Today."
    S(6, 6, "open2", 53, DOOR),                      # the sign flips to OPEN on beat 7
    S(12, 4, "elif_photo", 332, CAMERA),             # the drop: the shutter on beat 13, her photo
    S(16, 2, "elif_doc", 736, PREVIEW),              # her photo drops into the Turkish passport
    S(18, 2, "elif_print", 1260, BOOKLET),           # the booklet: navy cover, her page
    S(20, 4, "elif_hand", 553, COUNTER),             # "Here's the money." Cash on beat 22.
    S(24, 1, "kenji", 600, COUNTER),                 # the run: the next client,
    S(25, 1, "elif_print", 458, PRINTER),            # the jammed printer gets hit,
    S(26, 1, "viktor_photo", 420, CAMERA),           # Viktor's photo, on last year's grey,
    S(27, 1, "viktor_print", 1200, BOOKLET),         # his French passport
    S(28, 4, "viktor_hand", 395, COUNTER),           # he reads it: "Wrong background. France wants white now."
    S(32, 4, "viktor_hand", 780, COUNTER),           # "I'm not paying for this."
    S(36, 14, "night", 120, NIGHT, dim=0.55),        # closed for the night: the logo
]
# Captions: (first beat, last beat, lines of (word, colour)). Three words or fewer, 1.6 s or more.
CARDS = [
    (0.5, 6, [[("FRENCH", CREAM), ("PASSPORT.", CREAM)], [("TODAY.", RED)]]),
    (6.5, 12, [[("YOU RUN A", CREAM)], [("BACK-ROOM", CREAM)], [("PASSPORT", AMBER), ("SHOP.", CREAM)]]),
    (12, 16, [[("REAL", CREAM), ("FACES.", AMBER)]]),
    (16, 20, [[("FAKE", RED), ("PAPERS.", CREAM)]]),
    (20, 24, [[("REAL", CREAM), ("MONEY.", AMBER)]]),
    (28, 32, [[("ONE", CREAM), ("MISTAKE...", RED)]]),
    (32.5, 36, [[("I'M NOT", CREAM)], [("PAYING FOR THIS.", CREAM)]]),
]
TITLE_AT = 36.0   # the logo lands
CTA_AT = 39.0     # FREE DEMO OUT NOW ON STEAM
STUDIO_AT = 43.0
FADE = (49.0, 50.0)

# The arrangement, one entry per bar.
MUSIC = [
    dict(kind="sneak"),                                              # 0 the hook
    dict(kind="sneak", brushes=True, stabs=True),                    # 1 the premise
    dict(kind="sneak", brushes=True, stabs=True, fill=True),         # 2 into the drop
    dict(kind="groove_b", m=0, hit=True, drive=True),                # 3 REAL FACES.
    dict(kind="groove_b", m=1, hit=True, drive=True),                # 4 FAKE PAPERS.
    dict(kind="groove_b", m=2, hit=True, drive=True),                # 5 REAL MONEY.
    dict(kind="groove_b", m=3, drive=True, fill=True),               # 6 the run
    dict(kind="hits"),                                               # 7 he reads it
    dict(kind="rest"),                                               # 8 ...wrong background
    dict(kind="end", tag=True),                                      # 9 the logo; 10 FREE DEMO under the ring
    dict(kind="rest"),
    dict(kind="rest"),
    dict(kind="rest"),
]
HITS = []                       # extra band hits (beats)
RISER_TO = [12.0, TITLE_AT]


# ====================================================================== text
def outlined(text, size, color, outline=None):
    """A line in the stamp font with a hard black outline and drop shadow, crisp on the font's grid."""
    f = ImageFont.truetype(STAMP_TTF, size)
    px = max(2, size // 12)
    o = outline if outline is not None else px
    b = f.getbbox(text)
    w, h = b[2] - b[0] + 2 * o + px, b[3] - b[1] + 2 * o + px
    mask = Image.new("L", (w, h), 0)
    ImageDraw.Draw(mask).text((o - b[0], o - b[1]), text, font=f, fill=255)
    m = np.array(mask) >= 128
    # Square dilation for the outline, then the shadow one font pixel down-right.
    dil = m.copy()
    for dy in range(-o, o + 1):
        for dx in range(-o, o + 1):
            dil |= np.roll(np.roll(m, dy, 0), dx, 1)
    shadow = np.roll(np.roll(dil, px, 0), px, 1)
    out = np.zeros((h, w, 4), np.uint8)
    out[shadow] = (0, 0, 0, 200)
    out[dil] = (0, 0, 0, 255)
    out[m] = color + (255,)
    return out


def card_image(lines, max_w):
    """Stacked lines; each line is [(word, colour)...]. The biggest size (on the 12 px grid) that fits."""
    for size in (156, 144, 132, 120, 108, 96, 84, 72):
        f = ImageFont.truetype(STAMP_TTF, size)
        if max(f.getlength(" ".join(w for w, _ in line)) for line in lines) + 2 * size // 12 <= max_w:
            break
    rows = []
    for line in lines:
        parts = [outlined(w if i == len(line) - 1 else w + " ", size, c) for i, (w, c) in enumerate(line)]
        hh = max(p.shape[0] for p in parts)
        row = np.zeros((hh, sum(p.shape[1] for p in parts), 4), np.uint8)
        x = 0
        for p in parts:
            row[:p.shape[0], x:x + p.shape[1]] = np.maximum(row[:p.shape[0], x:x + p.shape[1]], p)
            x += p.shape[1] - 2 * max(2, size // 12)
        rows.append(row)
    gap = size // 6
    W = max(r.shape[1] for r in rows)
    H = sum(r.shape[0] for r in rows) + gap * (len(rows) - 1)
    img = np.zeros((H, W, 4), np.uint8)
    y = 0
    for r in rows:
        x = (W - r.shape[1]) // 2
        img[y:y + r.shape[0], x:x + r.shape[1]] = r
        y += r.shape[0] + gap
    return img


def pop(img, age, frames=4, start=1.12):
    """Lands on the beat: a few frames from a little bigger to its size (nearest-neighbour)."""
    if age >= frames:
        return img
    s = start - (start - 1.0) * age / frames
    h, w = img.shape[:2]
    ri = (np.arange(int(h * s)) / s).astype(int).clip(0, h - 1)
    ci = (np.arange(int(w * s)) / s).astype(int).clip(0, w - 1)
    return img[ri][:, ci]


# ====================================================================== pictures
class Layout:
    def __init__(self, fmt, capture):
        self.fmt = fmt
        self.W, self.H = FORMATS[fmt]
        max_w = 980 if fmt == "vertical" else 1600
        self.cards = [(bf(b0), bf(b1), card_image(lines, max_w)) for b0, b1, lines in CARDS]
        logo = np.array(Image.open(os.path.join(capture, "art", "logo_dark.png")).convert("RGBA"))
        self.logo = D.nearest(logo, 12 if fmt == "vertical" else 11)
        self.cta = card_image([[("FREE", CREAM), ("DEMO", AMBER)], [("OUT NOW ON STEAM", CREAM)]], 900 if fmt == "vertical" else 1300)
        self.studio = D.nearest(studio_logo.to_rgba(studio_logo.build()), 4)
        self.card_y = 1330 if fmt == "vertical" else 860          # centre of the captions: over the counter

    def picture(self, src, shot):
        if self.fmt == "horizontal":
            return src.copy()
        fr = np.zeros((self.H, self.W, 3), np.uint8)
        fr[:] = INK
        x0 = shot["vx"]
        region = src[:, x0:x0 + 720]
        fr[V_TOP:V_TOP + 1620] = np.repeat(np.repeat(region, 3, 0), 3, 1)[::2, ::2]
        return fr

    def compose(self, src, shot, f, k):
        fr = self.picture(src, shot) if src is not None else np.full((self.H, self.W, 3), INK, np.uint8)
        dim = shot.get("dim", 0.0)
        if dim:
            ramp = min(1.0, k / 10.0)
            fr = D.darken(fr, dim * ramp)
        if shot.get("flash_in") and k < 6:
            fr = (fr.astype(np.float32) * (1 - 0.9 * (1 - k / 6)) + 255 * 0.9 * (1 - k / 6)).astype(np.uint8)
        for f0, f1, img in self.cards:
            if f0 <= f < f1:
                im = pop(img, f - f0)
                D.blend(fr, im, (self.W - im.shape[1]) // 2, self.card_y - im.shape[0] // 2)
        if f >= bf(TITLE_AT):
            age = f - bf(TITLE_AT)
            im = pop(self.logo, age, 5, 1.25)
            ly = (760 if self.fmt == "vertical" else 400) - im.shape[0] // 2
            D.blend(fr, im, (self.W - im.shape[1]) // 2, ly)
            if age < 3:
                fr = (fr.astype(np.float32) * 0.6 + 255 * 0.4 * (1 - age / 3)).astype(np.uint8)
        if f >= bf(CTA_AT):
            im = pop(self.cta, f - bf(CTA_AT))
            cy = 1180 if self.fmt == "vertical" else 760
            D.blend(fr, im, (self.W - im.shape[1]) // 2, cy - im.shape[0] // 2)
        if f >= bf(STUDIO_AT):
            a = min(1.0, (f - bf(STUDIO_AT)) / 15.0)
            sy = 1460 if self.fmt == "vertical" else 930
            D.blend(fr, self.studio, (self.W - self.studio.shape[1]) // 2 // 4 * 4, sy, a)
        if f >= bf(FADE[0]):
            fr = D.darken(fr, min(1.0, (f - bf(FADE[0])) / (bf(FADE[1]) - bf(FADE[0]))))
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
                enc.stdin.write(layout.compose(src, shot, f, k).tobytes())
                f += 1
            print(f"  {fmt:10s} {shot['take']:12s} beat {shot['b']:4.1f}  frame {f:4d}/{total}  {time.time() - t0:5.1f}s", flush=True)
        else:
            for k in sorted({min(n - 1, 8), n // 2, n - 2}):
                src = next(take.frames(shot["f"] + k, 1))
                sheet.append((shot, f + k, layout.compose(src, shot, f + k, k)))
            f += n
    if preview is not None:
        tw, th = (270, 480) if fmt == "vertical" else (480, 270)
        cols = 8 if fmt == "vertical" else 5
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

    for b, note, beats, v in VIBES:
        vib(b, note, beats, v)
    return T.mix_music(stems, SECONDS)


VIBES = [(22.0, "A5", 0.5, 0.75), (22.5, "D6", 2.0, 0.8)]       # (beat, note, beats, velocity): the cash ding


def render_sound(takes):
    mx = T.Mixer(SECONDS)
    beds = T.beds(SECONDS + 1.0)
    seed = [11000]

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
                    mx.put("sfx", sfx.bell(sd()), t, 0.85, -0.35)
                elif sid == "DoorOpen":
                    mx.put("sfx", sfx.door(sd()), t, 0.7, -0.35)
                    mx.put("door", dsp.fade(sfx.rain(2.8, sd(), inside=False, intensity=0.9), 0.25, 1.4), t + 0.05, 1.0)
                elif sid == "Paper":
                    mx.put("sfx", sfx.paper(sd(), p), t, 0.8 * v, 0.1)
                elif sid == "Voice":
                    mx.put("voice", sfx.voice(v, p, sd()), t, 0.85)
                elif sid == "Shutter":
                    mx.put("sfx", sfx.shutter(sd()), t, 1.0, -0.1)
                elif sid == "Stamp":
                    mx.put("sfx", sfx.whack(sd()) if p < 0.9 else sfx.stamp(sd()), t, 1.1, 0.3)
                elif sid == "Cash":
                    mx.put("sfx", sfx.cash(sd()), t, 1.1, 0.3)
                elif sid == "Jam":
                    mx.put("sfx", sfx.jam(sd()), t, 1.1, 0.4)
                elif sid == "PrinterDone":
                    mx.put("sfx", sfx.printer_done(sd()), t, 0.9, 0.45)
            elif kind == "flash":
                mx.put("sfx", sfx.flash(sd()), t, 0.85)
            elif kind == "step":
                mx.put("sfx", sfx.step(sd(), e.get("s", 1.0)), t, 0.8, T.x_pan(e["x"]))
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
                    mx.put("sfx", sfx.mouse(sd(), up=(kind == "up")), t, 0.7)
        # The printer motor, where it runs inside this shot.
        start = f_in if take.printer_on_at(f_in - 1) else None
        for e in [e for e in evs if e["t"] == "printer"] + [dict(f=f_in + n, t="end")]:
            if e["t"] == "printer" and e["on"] == 1 and start is None:
                start = e["f"]
            elif (e["t"] == "end" or e["on"] == 0) and start is not None:
                if e["f"] - start > 3:
                    mx.put("sfx", sfx.printer_run((e["f"] - start) / FPS + 0.05, sd(), take.tier), t_shot + (start - f_in) / FPS, 0.8)
                start = None
        a, b = n_of(t_shot), n_of(t_shot + shot["n"] * BEAT)
        night = shot["take"] == "night"
        prof = (dict(rain_in=1.0, neon=1.0, hum=0.5, traffic=1.0) if night else
                dict(rain_in=0.45, hum=0.65, traffic=0.35) if shot["take"].endswith(("photo", "doc")) else
                dict(rain_in=0.75, hum=1.0, traffic=0.55))
        for bed, g in prof.items():
            y = dsp.fade(beds[bed][:, a:b] * g, 0.012, 0.012)
            mx.bus["amb"][:, a:a + y.shape[1]] += y
    # Each caption lands with a soft rubber stamp; the logo with the big one.
    for b0, b1, lines in CARDS:
        mx.put("fx", sfx.stamp(sd()), bt(b0), 0.7)
    mx.put("fx", sfx.stamp(sd()), bt(CTA_AT), 0.6)
    mx.put("fx", inst.boom(), bt(TITLE_AT), 1.0)
    mx.put("fx", sfx.stamp(sd(), big=True), bt(TITLE_AT), 1.2)
    for b in RISER_TO:
        mx.put("fx", inst.riser(1.6), bt(b) - 1.6, 0.35)
    mx.put("fx", inst.reverse_cymbal(1.2), bt(TITLE_AT) - 1.2, 0.5)
    return mx


def master(mx, music):
    sfx_bus = mx.bus["sfx"]
    voice = dsp.compressor(dsp.peak_eq(dsp.hp(mx.bus["voice"], 120.0, 2), 2200.0, 3.0, 0.9), -24.0, 3.0, 0.003, 0.08, makeup_db=3.0)
    amb = mx.bus["amb"] + mx.bus["door"]
    fx = mx.bus["fx"]
    sfx_wet = dsp.wet(sfx_bus * 0.5 + voice * 0.35, dsp.reverb_ir(0.5, predelay=0.006, damp=0.75, width=0.6, seed=41), 0.3)
    fx_wet = dsp.wet(fx, dsp.reverb_ir(2.4, predelay=0.03, damp=0.5, seed=42), 0.3)
    music = music * dsp.undb(-15.0 - dsp.lufs(music))
    music = dsp.duck(music, voice * 2.0 + sfx_bus, depth_db=-3.0, thresh_db=-34.0, attack=0.01, release=0.25)
    mix = music + (sfx_bus + sfx_wet) * dsp.undb(-1.0) + voice * dsp.undb(4.0) + amb * dsp.undb(-8.0) + (fx + fx_wet) * dsp.undb(-4.0)
    mix = dsp.compressor(dsp.hp(mix, 25.0, 2), -16.0, 2.0, 0.01, 0.2)
    for _ in range(3):
        mix = mix * dsp.undb(-14.0 - dsp.lufs(dsp.limiter(mix, -2.0)))
    return dsp.limiter(mix, -2.0)


# ====================================================================== main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--capture", required=True, help="the capture folder written by trailer.ps1 -Script day2jobs")
    ap.add_argument("--night", required=True, help="a capture folder with a night take (trailer.ps1 -Only night)")
    ap.add_argument("--out", default=os.path.join(ROOT, "Steam", "trailer"))
    ap.add_argument("--work", default=None, help="folder for intermediates (default: <capture>/../work_teaser)")
    ap.add_argument("--preview", action="store_true")
    ap.add_argument("--only", choices=sorted(FORMATS), help="render one format")
    args = ap.parse_args()
    work = args.work or os.path.join(os.path.dirname(os.path.abspath(args.capture)), "work_teaser")
    os.makedirs(work, exist_ok=True)
    os.makedirs(args.out, exist_ok=True)
    takes = {}
    for nm in sorted({s["take"] for s in EDL}):
        folder = os.path.join(args.night if nm == "night" else args.capture, "takes")
        takes[nm] = T.Take(folder, nm)
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
    i0 = n_of(bt(FADE[0]))
    out[:, i0:] *= np.linspace(1.0, 0.0, out.shape[1] - i0) ** 1.5
    wav = os.path.join(work, "teaser_audio.wav")
    T.write_wav(wav, out)
    print(f"  {dsp.lufs(out):.1f} LUFS, true peak {dsp.true_peak_db(out):.1f} dBTP ({time.time() - t0:.0f}s)", flush=True)
    names = {"vertical": "legit_enough_teaser_vertical_1080x1920.mp4", "horizontal": "legit_enough_teaser_1080p60.mp4"}
    for fmt in fmts:
        video = os.path.join(work, f"teaser_{fmt}.mkv")
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
