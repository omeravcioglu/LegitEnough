"""LEGIT ENOUGH in-game music: four original cues in the trailer's world (D minor, upright bass,
brushes, Rhodes, vibraphone), written as code so every render is the same.

  title_night  72 BPM, 24 bars (80 s) loop   rainy night: rolled Rhodes, vibes fragments, pad
  shop_day     96 BPM, 96 bars (240 s) loop  three AABA choruses: 2-feel -> walking -> head out
  shop_busy    96 BPM, 96 bars (240 s) loop  the same performance with ride, kick, snaps, pushes;
                                             sample-locked to shop_day, so the game crossfades
                                             between the two at the same position
  demo_end     88 BPM -> ritardando, ~50 s   warm bridge melody, lands on D6/9 with the ident motif

Loops are rendered circularly: everything that rings past the loop end (notes, reverb, filter
and compressor state) is wrapped onto the head, and LFOs run a whole number of cycles per loop.

Usage:  python Tools/Music/compose.py [--cue all|title|shop|busy|end] [--no-export]
Writes Assets/LegitEnough/Resources/LegitEnough/Music/*.ogg (what the game plays), MP3 listening
copies in Tools/Music/out/ and float WAV masters in %TEMP%/LegitEnoughMusic (for analyze.py).
Needs numpy, scipy, Pillow and ffmpeg (libvorbis, libmp3lame).
"""
import argparse
import os
import subprocess
import sys
import tempfile
import time
import zlib

import numpy as np
from scipy.io import wavfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Trailer"))
sys.path.insert(0, HERE)

from audio import dsp, inst  # noqa: E402  (the trailer's synth: same instruments, same room)
from audio.dsp import SR, n_of  # noqa: E402
from audio.inst import hz, midi  # noqa: E402

import theory as T  # noqa: E402

GAME_DIR = os.path.join(ROOT, "Assets", "LegitEnough", "Resources", "LegitEnough", "Music")
OUT_DIR = os.path.join(HERE, "out")
# Float masters are big (90 MB for a 4-minute cue): they live in the temp folder, not the project.
WORK_DIR = os.path.join(tempfile.gettempdir(), "LegitEnoughMusic")


# ====================================================================== time

class Clock:
    """Beat -> seconds, with swing on the off-beat eighths and an optional tempo map
    [(beat, bpm), ...] (linear bpm ramps between the points)."""

    def __init__(self, bpm, swing=0.5, tempo=None, beats=512):
        self.bpm = bpm
        self.swing = swing
        self.tempo = tempo
        if tempo:
            grid = np.arange(0, beats + 1, 1 / 96.0)
            bs = [b for b, _ in tempo]
            vs = [v for _, v in tempo]
            bpm_at = np.interp(grid, bs, vs)
            sec = np.concatenate([[0.0], np.cumsum(60.0 / bpm_at[:-1] / 96.0)])
            self._grid, self._sec = grid, sec

    def raw(self, b):
        if self.tempo:
            return float(np.interp(b, self._grid, self._sec))
        return b * 60.0 / self.bpm

    def sec(self, b):
        whole = np.floor(b + 1e-9)
        frac = b - whole
        if abs(frac - 0.5) < 1e-6:
            frac = self.swing
        return self.raw(whole + frac)

    def beats_to_sec(self, at, beats):
        return self.raw(at + beats) - self.raw(at)


# ====================================================================== a take

STEMS = ("bass", "kit", "ride", "snaps", "rhodes", "vibes", "pad")


class Take:
    """Stem buffers for one cue. `loop`: the cue is `length` seconds long and circular."""
    PRE = 0.3

    def __init__(self, clock, length, loop, seed, tail=9.0):
        self.clock = clock
        self.length = length
        self.loop = loop
        self.L = n_of(length)
        self.N = n_of(self.PRE + length + tail)
        self.stems = {k: dsp.stereo(self.N) for k in STEMS}
        self.rng = np.random.default_rng(seed)

    def put(self, stem, beat, y, gain=1.0, pan=0.0, late=0.0, jitter=0.004, rng=None):
        r = rng or self.rng
        t = self.clock.sec(beat) + late + (r.normal(0, jitter) if jitter > 0 else 0.0)
        if y.ndim == 1:
            y = dsp.pan(y, pan)
        dsp.place(self.stems[stem], y, n_of(self.PRE + t), gain)

    def fold(self):
        """Loop: wrap the tail (and anything placed before 0) circularly. One-shot: trim."""
        out = {}
        P, L = n_of(self.PRE), self.L
        for k, b in self.stems.items():
            if self.loop:
                y = b[:, P:P + L].copy()
                tail = b[:, P + L:]
                while tail.shape[1] > 0:
                    m = min(L, tail.shape[1])
                    y[:, :m] += tail[:, :m]
                    tail = tail[:, m:]
                y[:, L - P:] += b[:, :P]
            else:
                y = b[:, P:].copy()
            out[k] = y
        return out


def vel(rng, v, sd=0.035):
    return float(np.clip(v + rng.normal(0, sd), 0.05, 1.0))


def kr(seed, *key):
    """A keyed rng: the same musical decision gets the same random numbers in every version
    of a cue (shop_day and shop_busy share their bass, comping and melody this way)."""
    h = seed
    for k in key:
        v = zlib.crc32(k.encode()) if isinstance(k, str) else int(k) + 7
        h = (h * 1000003 + v) & 0xFFFFFFFF
    return np.random.default_rng(h)


# ====================================================================== instruments in context

def soft_pad(freqs, dur, v=0.4, attack=1.4, release=1.6, seed=0):
    """A dark, slow ensemble pad (a few detuned partials, cheap and smooth)."""
    rng = np.random.default_rng(seed)
    n = n_of(dur + release)
    t = np.arange(n) / SR
    out = np.zeros((2, n))
    for f in freqs:
        for j, d in enumerate((-0.06, 0.0, 0.055)):
            ff = f * 2 ** (d / 12)
            y = np.zeros(n)
            for k in range(1, 7):
                if ff * k > 5000:
                    break
                y += np.sin(2 * np.pi * ff * k * t + rng.random() * 6.28) / k ** 1.6
            out += dsp.pan(y, (j - 1) * 0.6)
    e = np.clip(t / attack, 0, 1) ** 2
    e *= np.where(t > dur, np.exp(-(t - dur) / (release / 3)), 1.0)
    return out * e / (len(freqs) * 3) * v


def bass_note(take, beat, m, beats, v, rng, gain=1.0, dead=False):
    d = take.clock.beats_to_sec(beat, beats)
    y = inst.bass(hz(m), d, v, rng, dead=dead)
    take.put("bass", beat, y, gain, 0.0, late=0.006, jitter=0.004, rng=rng)


def rhodes_chord(take, beat, voicing, beats, v, rng, gain=1.0, roll=0.0, top_only=0):
    d = take.clock.beats_to_sec(beat, beats)
    notes = voicing[-top_only:] if top_only else voicing
    for i, m in enumerate(notes):
        y = inst.rhodes(hz(m), d, vel(rng, v, 0.03))
        p = -0.3 + 0.6 * i / max(1, len(notes) - 1)
        take.put("rhodes", beat, y, gain * 0.5, p, late=0.012 + roll * i, jitter=0.003, rng=rng)


def vibes_note(take, beat, m, beats, v, rng, gain=1.0, pan=0.18):
    d = take.clock.beats_to_sec(beat, beats)
    take.put("vibes", beat, inst.vibes(hz(m), d, vel(rng, v, 0.03), rng), gain, pan, late=0.008, jitter=0.005, rng=rng)


def melody(take, bar0, phrase, rng, gain=1.0, octave=0, double=0.0, v_mul=1.0):
    """phrase: list of bars of (beat, note, beats, vel). `double` adds the Rhodes an octave down."""
    for i, b in enumerate(phrase):
        for beat, name, length, v in b:
            m = midi(name) + 12 * octave
            at = (bar0 + i) * 4 + beat
            vibes_note(take, at, m, length, v * v_mul, rng, gain)
            if double > 0:
                d = take.clock.beats_to_sec(at, length)
                y = inst.rhodes(hz(m - 12), d, vel(rng, v * v_mul * 0.9, 0.03))
                take.put("rhodes", at, y, gain * double, -0.12, late=0.014, jitter=0.004, rng=rng)


# ---------------------------------------------------------------- drums

def swish(take, beat, beats, v, rng, gain=1.0):
    d = take.clock.beats_to_sec(beat, beats)
    take.put("kit", beat, inst.brush_swish(d, vel(rng, v), rng), gain, -0.15, late=0.0, jitter=0.006, rng=rng)


def tap(take, beat, v, rng, gain=1.0):
    take.put("kit", beat, inst.brush_tap(vel(rng, v), rng), gain, -0.1, late=0.004, jitter=0.004, rng=rng)


def foot(take, beat, v, rng, gain=1.0):
    take.put("kit", beat, inst.hat_foot(vel(rng, v), rng), gain, 0.2, late=0.0, jitter=0.003, rng=rng)


def kick(take, beat, v, rng, gain=1.0):
    take.put("kit", beat, inst.kick(vel(rng, v, 0.02), rng), gain, 0.0, late=0.003, jitter=0.003, rng=rng)


def ride(take, beat, v, rng, gain=1.0):
    take.put("ride", beat, inst.ride(vel(rng, v), int(rng.integers(0, 3))), gain, 0.35, late=-0.002, jitter=0.003, rng=rng)


def snap(take, beat, v, rng, gain=1.0, pan=0.25):
    take.put("snaps", beat, inst.snap(vel(rng, v), rng), gain, pan, late=0.0, jitter=0.004, rng=rng)


def brush_fill(take, bar, rng, v0=0.22, v1=0.46, rim=False, from_beat=3.0):
    k, b = 0, from_beat
    steps = int(round((4.0 - from_beat) * 3))
    for j in range(steps):
        pos = bar * 4 + from_beat + j / 3.0
        v = v0 + (v1 - v0) * j / max(1, steps - 1)
        take.put("kit", pos, inst.brush_tap(vel(rng, v), rng), 1.0, -0.1, late=0.0, jitter=0.002, rng=rng)
        k += 1
    if rim:
        take.put("kit", bar * 4 + 3 + 2 / 3.0, inst.rim(0.45, rng), 0.5, 0.1, jitter=0.002, rng=rng)


# ====================================================================== charts and tunes

def chorus_chart():
    A1 = [T.bar("D madd9"), T.bar("D mM9"), T.bar("D m9"), T.bar("D m69"),
          T.bar("G m9"), T.bar("C 13"), T.bar("F maj9"), T.bar("E m7b5", "A 7b9")]
    A2 = A1[:6] + [T.bar("Bb maj7#11", "A 7b9"), T.bar("D m9", "D 7b9")]
    B = [T.bar("G m9"), T.bar("C 13"), T.bar("F maj9"), T.bar("Bb maj7#11"),
         T.bar("E m7b5"), T.bar("A 7b9"), T.bar("D m9", "Bb 13"), T.bar("E m7b5", "A 7b9")]
    return [("A1", A1), ("A2", A2), ("B", B), ("A3", A1)]


# The head. (beat, note, beats, velocity); x.5 beats are swung.
MEL_A = [
    [(0.5, "A4", .5, .62), (1, "D5", .5, .70), (1.5, "E5", .5, .66), (2, "F5", 2.0, .78)],
    [(0, "E5", 1.5, .72), (1.5, "D5", .5, .62), (2, "C#5", 2.0, .74)],
    [(0.5, "C5", .5, .60), (1, "D5", .5, .66), (1.5, "E5", .5, .66), (2, "A5", 1.5, .80), (3.5, "G5", .5, .64)],
    [(0, "F5", 1, .72), (1, "E5", .5, .64), (1.5, "D5", .5, .62), (2, "B4", 2.0, .70)],
    [(0.5, "Bb4", .5, .60), (1, "D5", .5, .66), (1.5, "F5", .5, .68), (2, "A5", 2.0, .78)],
    [(0, "G5", 1, .72), (1, "F5", .5, .62), (1.5, "E5", .5, .64), (2, "D5", 1.5, .70), (3.5, "E5", .5, .60)],
    [(0, "C5", 2.0, .70), (2.5, "A4", .5, .58), (3, "C5", .5, .62), (3.5, "E5", .5, .66)],
    [(0, "D5", 1.5, .72), (2, "Bb4", 1.0, .66), (3, "C#5", 1.0, .70)],
]
MEL_A2_END = [  # bars 5-8 of the second A: up to Bb, then into the bridge on F#
    [(0.5, "D5", .5, .64), (1, "F5", .5, .68), (1.5, "A5", .5, .72), (2, "Bb5", 1.5, .80), (3.5, "A5", .5, .64)],
    [(0, "G5", 1.5, .74), (1.5, "E5", .5, .62), (2, "D5", .5, .64), (2.5, "E5", 1.5, .70)],
    [(0, "F5", 1, .70), (1, "E5", 1, .68), (2, "G5", .5, .66), (2.5, "F5", .5, .62), (3, "E5", .5, .62), (3.5, "C#5", .5, .64)],
    [(0, "D5", 2.0, .72), (2.5, "C5", .5, .60), (3, "Eb5", .5, .64), (3.5, "F#5", .5, .68)],
]
MEL_B = [
    [(0, "G5", 1.5, .76), (1.5, "A5", .5, .66), (2, "Bb5", 2.0, .80)],
    [(0, "A5", 1, .72), (1, "G5", .5, .64), (1.5, "E5", 2.5, .72)],
    [(0.5, "F5", .5, .62), (1, "G5", .5, .66), (1.5, "A5", .5, .70), (2, "C6", 1.5, .80), (3.5, "A5", .5, .64)],
    [(0, "A5", 1, .72), (1, "E5", 3.0, .74)],
    [(0.5, "D5", .5, .60), (1, "G5", .5, .66), (1.5, "Bb5", .5, .72), (2, "A5", 2.0, .76)],
    [(0, "G5", 1, .70), (1, "F5", .5, .64), (1.5, "E5", .5, .64), (2, "C#5", 1.5, .70), (3.5, "E5", .5, .60)],
    [(0, "F5", 1.5, .72), (1.5, "E5", .5, .62), (2, "D5", 1, .68), (3, "C5", 1, .64)],
    [(0, "Bb4", 1, .64), (1, "D5", .5, .62), (1.5, "G5", .5, .66), (2, "F5", 1, .70), (3, "E5", .5, .64), (3.5, "C#5", .5, .64)],
]
MEL_A3_END = [  # the last A settles down instead of turning around
    [(0, "C5", 1.0, .66), (1, "A4", 1.0, .60), (2.5, "G4", .5, .56), (3, "A4", .5, .58), (3.5, "C5", .5, .60)],
    [(0, "D5", 2.0, .64), (2.5, "Bb4", .5, .56), (3, "A4", 1.0, .58)],
]

# Rhodes comping rhythms: (beat, beats, vel x, chord index in the bar)
COMP_ONE = {
    "hold": [(0, 3.4, 1.0, 0)],
    "charleston": [(0, 1.3, 1.0, 0), (1.5, .45, .85, 0)],
    "two": [(0, .9, 1.0, 0), (2.5, .45, .82, 0)],
    "off": [(.5, .45, .9, 0), (2.0, 1.4, .95, 0)],
    "late": [(1.5, .45, .9, 0), (3.0, .9, .85, 0)],
    "push": [(-.5, 1.9, 1.0, 0)],
    "space": [],
}
COMP_TWO = {
    "both": [(0, 1.6, 1.0, 0), (2, 1.6, .95, 1)],
    "ant": [(0, .9, 1.0, 0), (1.5, 2.0, .92, 1)],
    "push": [(-.5, 2.2, 1.0, 0), (2, 1.4, .9, 1)],
}


# ====================================================================== shop day / busy

SHOP_BPM = 96.0
SHOP_BARS = 96


def shop_plan():
    """Per bar: chorus, section, bar in section, bass feel, dynamics, lead."""
    plan = []
    secs = chorus_chart()
    lead = {  # (chorus, section) -> lead part
        (0, "A1"): None, (0, "A2"): "A2", (0, "B"): "B", (0, "A3"): "A3",
        (1, "A1"): "guide", (1, "A2"): "guide", (1, "B"): "rhodesB", (1, "A3"): "guide",
        (2, "A1"): "A1dbl", (2, "A2"): "guide_lo", (2, "B"): "B", (2, "A3"): None,
    }
    dyn = {(0, "A1"): .84, (0, "A2"): .86, (0, "B"): .9, (0, "A3"): .9,
           (1, "A1"): .92, (1, "A2"): .94, (1, "B"): .97, (1, "A3"): .93,
           (2, "A1"): .93, (2, "A2"): .91, (2, "B"): .93, (2, "A3"): .85}
    for c in range(3):
        for name, sec in secs:
            two = (c == 0 and name in ("A1", "A2")) or (c == 2 and name == "A3")
            for k, b in enumerate(sec):
                plan.append(dict(c=c, sec=name, k=k, chords=b, two=two, dyn=dyn[(c, name)], lead=lead[(c, name)]))
    return plan


def render_shop(busy, seed=1966):
    clock = Clock(SHOP_BPM, swing=0.62)
    length = SHOP_BARS * 4 * 60.0 / SHOP_BPM
    take = Take(clock, length, True, seed + (1 if busy else 0))
    plan = shop_plan()
    chart = [p["chords"] for p in plan]
    voicings = T.voice_chart(chart)

    # ------------------------------------------------ bass: section by section, keyed
    for si in range(len(plan) // 8):
        sub = chart[si * 8:(si + 1) * 8]
        feel = ["walk" if busy else ("two" if plan[si * 8 + j]["two"] else "walk") for j in range(8)]
        nxt = chart[((si + 1) * 8) % len(chart)][0]
        end_t = T._nearest(nxt.root, 38)
        rng = kr(seed, "bass", si, feel[0])
        rake = kr(seed, "rake", si)
        line = T.walk(sub, rng, start=38, feel=feel, next_chord=nxt, end_target=end_t)
        for at, m, beats, acc in line:
            p = plan[si * 8 + int(at // 4)]
            b = si * 32 + at
            if acc < 0:
                bass_note(take, b, m, beats, 0.45, rng, 0.55, dead=True)
            else:
                bass_note(take, b, m, beats, vel(rng, (0.7 + 0.12 * acc) * p["dyn"]), rng)
            if acc > 0 and acc < 0.9 and beats < 1 and rake.random() < (0.24 if busy else 0.12):
                # a raked dead note into the next beat
                bass_note(take, b + 0.5, m, 0.08, 0.42, rake, 0.5, dead=True)

    # ------------------------------------------------ Rhodes comping (shared), busy adds stabs
    events = []
    for bi, p in enumerate(plan):
        rng = kr(seed, "comp", bi)
        lead_on = p["lead"] is not None
        if len(p["chords"]) == 2:
            names = ["both", "ant", "push"]
            w = [0.45, 0.35, 0.2]
            pat = COMP_TWO[names[int(rng.choice(3, p=w))]]
        else:
            if p["two"]:
                names, w = ["hold", "two", "space", "push"], [0.4, 0.3, 0.15, 0.15]
            elif lead_on:
                names, w = ["hold", "two", "charleston", "space", "push"], [0.28, 0.24, 0.18, 0.16, 0.14]
            else:
                names, w = ["charleston", "two", "off", "late", "push", "hold"], [0.22, 0.18, 0.18, 0.16, 0.14, 0.12]
            pat = COMP_ONE[names[int(rng.choice(len(names), p=np.array(w) / sum(w)))]]
        base = (0.44 if lead_on else 0.5) * p["dyn"]
        for beat, beats, vm, ci in pat:
            events.append([bi * 4 + beat, beats, base * vm, voicings[bi][ci], 1.0])
        if busy and len(pat) <= 1 and len(p["chords"]) == 1:
            free = [x for x in (1.5, 2.5, 3.5) if all(abs(x - e[0]) > 0.4 for e in pat)]
            if free:
                x = free[int(rng.integers(0, len(free)))]
                events.append([bi * 4 + x, 0.35, base * 0.85, voicings[bi][0], 0.9])
    # The next hit (or a push) cuts the one before. Busy's stabs are cut by the shared hits but
    # never cut them, so the shared comping stays identical in both versions.
    shared = sorted([e for e in events if e[4] == 1.0], key=lambda e: e[0])
    total = len(plan) * 4
    starts = [e[0] for e in shared]
    for i, e in enumerate(shared):
        nxt = shared[(i + 1) % len(shared)][0] + (total if i + 1 == len(shared) else 0)
        e[1] = min(e[1], nxt - e[0] - 0.08)
    for e in events:
        if e[4] != 1.0:
            later = [x for x in starts if x > e[0]] or [starts[0] + total]
            e[1] = min(e[1], later[0] - e[0] - 0.08)
    events.sort(key=lambda e: e[0])
    for at, beats, v, vc, g in events:
        rng = kr(seed, "rh", int(round(at * 6)))
        rhodes_chord(take, at, vc, max(0.25, beats), v, rng, g)

    # ------------------------------------------------ lead parts (shared)
    for si in range(len(plan) // 8):
        p0 = plan[si * 8]
        bar0 = si * 8
        rng = kr(seed, "lead", si)
        lead = p0["lead"]
        dm = p0["dyn"]
        if lead == "A2":
            melody(take, bar0, MEL_A[:4] + MEL_A2_END, rng, 0.95, v_mul=dm)
        elif lead == "B":
            melody(take, bar0, MEL_B, rng, 0.95, v_mul=dm)
        elif lead == "A3":
            melody(take, bar0, MEL_A[:6] + MEL_A3_END, rng, 0.92, v_mul=dm)
        elif lead == "A1dbl":
            melody(take, bar0, MEL_A, rng, 0.92, double=0.32, v_mul=dm)
        elif lead == "rhodesB":
            for i, b in enumerate(MEL_B):
                for beat, name, length, v in b:
                    at = (bar0 + i) * 4 + beat
                    d = clock.beats_to_sec(at, length)
                    y = inst.rhodes(hz(midi(name) - 12), d, vel(rng, v * 0.9 * dm, 0.03))
                    take.put("rhodes", at, y, 0.62, 0.1, late=0.01, jitter=0.004, rng=rng)
        elif lead in ("guide", "guide_lo"):
            lo, hi = (62, 74) if lead == "guide_lo" else (65, 79)
            sub = chart[bar0:bar0 + 8]
            for at, m, beats, v in T.guide_line(sub, rng, start=69 if lead == "guide" else 65, lo=lo, hi=hi):
                vibes_note(take, bar0 * 4 + at, m, beats, v * dm * 1.05, rng, 0.85, pan=0.25)

    # ------------------------------------------------ drums
    for bi, p in enumerate(plan):
        rng = kr(seed, "kit", bi)
        d = p["dyn"]
        b0 = bi * 4
        two = p["two"]
        for h in (0.0, 2.0):
            swish(take, b0 + h, 2.0, (0.34 if two else 0.42) * d, rng, 1.0)
        for h in (1.0, 3.0):
            tap(take, b0 + h, (0.27 if two else 0.36) * d, rng)
            foot(take, b0 + h, 0.33 * d, rng)
        for h in (0.5, 2.5):
            if rng.random() < 0.3:
                tap(take, b0 + h, 0.13, rng)
        if not two:
            for h in (0.0, 2.0):
                kick(take, b0 + h, 0.16 * d, rng)
        if p["k"] == 7:
            brush_fill(take, bi, rng, 0.2, 0.42 * d, rim=False)
        if p["k"] == 0 and bi > 0 and rng.random() < 0.5:
            kick(take, b0, 0.24, rng)
        if not busy and p["c"] == 1:
            for h, v in ((0, .5), (1, .62), (1.5, .34), (2, .5), (3, .62), (3.5, .34)):
                if h % 1 and rng.random() < 0.25:
                    continue
                ride(take, b0 + h, v * d, rng, 0.75)
        if busy:
            r2 = kr(seed, "busy", bi)
            for h, v in ((0, .6), (1, .8), (1.5, .44), (2, .6), (3, .8), (3.5, .44)):
                if h == 3.5 and r2.random() < 0.15:
                    continue
                ride(take, b0 + h, v * d, r2, 1.0)
            for h in (1.0, 3.0):
                kick(take, b0 + h, 0.14, r2)
            for h in (0.5, 1.5, 2.5, 3.5):       # the drummer talking on the brush
                if r2.random() < 0.28:
                    tap(take, b0 + h + (2 / 3.0 - 0.5 if r2.random() < 0.3 else 0), 0.16 + 0.12 * r2.random(), r2)
            if p["sec"] != "B" and p["c"] != 1:
                snap(take, b0 + 1.0, 0.62 * d, r2, 1.0, 0.3)
                snap(take, b0 + 3.0, 0.62 * d, r2, 1.0, -0.25)
            if p["k"] == 7:
                brush_fill(take, bi, r2, 0.28, 0.55, rim=True, from_beat=2.0 if r2.random() < 0.4 else 3.0)
            if p["k"] == 7 and r2.random() < 0.5:
                kick(take, b0 + 3.5, 0.32, r2)   # a soft bomb into the next section
    return take


# ====================================================================== title (rainy night)

TITLE_BPM = 72.0
TITLE_CHART = (
    [T.bar("D madd9"), T.bar("D mM9"), T.bar("D m9"), T.bar("D m69"),
     T.bar("G m9"), T.bar("C 13"), T.bar("F maj9"), T.bar("E m7b5", "A 7b9")] +
    [T.bar("G m9"), T.bar("C 13"), T.bar("F maj9"), T.bar("Bb maj7#11"),
     T.bar("E m7b5"), T.bar("A 7b9"), T.bar("D m9"), T.bar("Bb 13", "A 7b9")] +
    [T.bar("D madd9"), T.bar("D mM9"), T.bar("D m9"), T.bar("D m69"),
     T.bar("G m9"), T.bar("C 13"), T.bar("Bb maj7#11"), T.bar("A 7b9")])
TITLE_MEL = {
    0: [(1, "A4", 1, .5), (2, "D5", 1, .55), (3, "E5", 1, .56)],
    1: [(0, "F5", 3, .6), (3, "E5", 1, .5)],
    2: [(0, "D5", 1.5, .55), (1.5, "C5", .5, .48), (2, "A4", 2, .5)],
    4: [(1, "Bb4", 1, .5), (2, "D5", 1, .54), (3, "F5", 1, .56)],
    5: [(0, "A5", 2.5, .6), (3, "G5", 1, .5)],
    6: [(0, "E5", 3, .56), (3, "C5", 1, .46)],
    7: [(0, "D5", 2, .52), (2, "C#5", 2, .54)],
    8: [(0, "D5", 1, .5), (1, "F5", 1, .54), (2, "A5", 2, .6)],
    9: [(0, "G5", 2, .56), (2, "E5", 2, .52)],
    10: [(1, "C6", 1, .54), (2, "A5", 2, .58)],
    11: [(0, "E5", 4, .58)],
    12: [(1, "Bb4", 1, .48), (2, "D5", 1, .52), (3, "G5", 1, .55)],
    13: [(0, "F5", 2, .56), (2, "E5", 1, .5), (3, "C#5", 1, .5)],
    14: [(0, "D5", 4, .54)],
    20: [(1, "Bb4", 1, .46), (2, "D5", 1, .5), (3, "F5", 1, .52)],
    21: [(0, "G5", 2.5, .54), (3, "E5", 1, .46)],
    22: [(0, "D5", 2, .5), (2, "E5", 2, .5)],
    23: [(0, "C#5", 3, .5)],
}


def render_title(seed=72):
    clock = Clock(TITLE_BPM, swing=0.5)
    bars = len(TITLE_CHART)
    length = bars * 4 * 60.0 / TITLE_BPM
    take = Take(clock, length, True, seed, tail=10.0)
    voicings = T.voice_chart(TITLE_CHART)
    for bi, b in enumerate(TITLE_CHART):
        rng = kr(seed, "bar", bi)
        sec_b = 8 <= bi < 16
        # Rolled Rhodes, held; a soft re-strike of the top two now and then.
        for ci, c in enumerate(b):
            at = bi * 4 + ci * c.beats
            rhodes_chord(take, at, voicings[bi][ci], c.beats - 0.15, 0.4, rng, 1.0, roll=0.04)
            if len(b) == 1 and (rng.random() < 0.35 or bi == len(TITLE_CHART) - 1):
                rhodes_chord(take, at + 2.5, voicings[bi][ci], 1.3, 0.3, rng, 0.8, roll=0.03, top_only=2)
        # Bass: whole notes (half notes through the middle).
        for ci, c in enumerate(b):
            at = bi * 4 + ci * c.beats
            m = T._nearest(c.root, 38)
            if sec_b and len(b) == 1:
                bass_note(take, at, m, 1.85, vel(rng, 0.62), rng)
                fifth = T._nearest((c.root + 7) % 12, m)
                bass_note(take, at + 2, fifth, 1.8, vel(rng, 0.52), rng)
            elif bi == len(TITLE_CHART) - 1:
                # Walk up into the top of the loop: A ... C# -> D.
                bass_note(take, at, m, 2.9, vel(rng, 0.6), rng)
                bass_note(take, at + 3, T._nearest(1, m), 0.95, vel(rng, 0.5), rng)
            else:
                bass_note(take, at, m, c.beats - 0.1, vel(rng, 0.6), rng)
        # Pad: the room at night.
        for ci, c in enumerate(b):
            at = bi * 4 + ci * c.beats
            fr = [hz(m) for m in voicings[bi][ci][:3]]
            y = soft_pad(fr, clock.beats_to_sec(at, c.beats), 0.5, attack=1.2, release=1.4, seed=bi * 3 + ci)
            take.put("pad", at, y, 1.0, jitter=0.0, rng=rng)
        # Brushes breathe through the middle only.
        if sec_b:
            for h in (0.0, 2.0):
                swish(take, bi * 4 + h, 2.0, 0.24, rng)
            if bi in (11, 15):
                tap(take, bi * 4 + 3.0, 0.16, rng)
        if bi in TITLE_MEL:
            for beat, name, length_b, v in TITLE_MEL[bi]:
                vibes_note(take, bi * 4 + beat, midi(name), length_b, v, rng, 1.0, pan=0.15)
    return take


# ====================================================================== demo end

END_BPM = 88.0
END_CHART = [T.bar("G m9"), T.bar("C 13"), T.bar("F maj9"), T.bar("Bb maj7#11"),
             T.bar("E m7b5"), T.bar("A 7b9"), T.bar("D m9"), T.bar("G 13"),
             T.bar("G m9"), T.bar("C 13"), T.bar("F maj9"), T.bar("Bb maj9"),
             T.bar("G m9"), T.bar("A 7sus", "A 7b9"), T.bar("D 69"), T.bar("D 69")]
END_MEL = MEL_B[:6] + [
    [(0, "F5", 2, .7), (2, "E5", 1, .64), (3, "D5", 1, .62)],
    [(0, "E5", 2, .66), (2, "B4", 1, .6), (3, "D5", 1, .64)],
    [(0, "D5", 1, .62), (1, "F5", 1, .66), (2, "A5", 2, .72)],
    [(0, "G5", 2, .68), (2, "E5", 1, .62), (3, "D5", 1, .6)],
    [(0, "E5", 2, .66), (2, "G5", 1, .64), (3, "A5", 1, .68)],
    [(0, "F5", 3, .7), (3, "D5", 1, .6)],
    [(0, "D5", 2, .62), (2, "C5", 1, .58), (3, "Bb4", 1, .56)],
    [(0, "D5", 2, .6), (2, "C#5", 2, .62)],
]


def render_end(seed=88):
    tempo = [(0, END_BPM), (48, END_BPM), (56, 64.0), (64, 60.0)]
    clock = Clock(END_BPM, swing=0.6, tempo=tempo, beats=80)
    bars = len(END_CHART)
    length = clock.raw(bars * 4)
    take = Take(clock, length, False, seed, tail=7.0)
    voicings = T.voice_chart(END_CHART)
    feel = ["two"] * 8 + ["walk"] * 4 + ["two"] * 2 + ["two"] * 2
    line = T.walk(END_CHART[:14], kr(seed, "bass"), start=43, feel=feel[:14],
                  next_chord=END_CHART[14][0], end_target=38)
    for at, m, beats, acc in line:
        rng = kr(seed, "b", int(at * 4))
        if acc < 0:
            bass_note(take, at, m, beats, 0.42, rng, 0.5, dead=True)
        else:
            bass_note(take, at, m, beats, vel(rng, 0.72 + 0.12 * acc), rng)
    rng = kr(seed, "final")
    bass_note(take, 56, 38, 7.0, 0.8, rng, 1.0)
    bass_note(take, 56, 26, 7.0, 0.55, rng, 0.6)
    for bi, b in enumerate(END_CHART[:14]):
        rng = kr(seed, "comp", bi)
        if len(b) == 2:
            rhodes_chord(take, bi * 4, voicings[bi][0], 1.8, 0.48, rng)
            rhodes_chord(take, bi * 4 + 2, voicings[bi][1], 1.8, 0.46, rng)
        elif bi >= 12:
            rhodes_chord(take, bi * 4, voicings[bi][0], 3.6, 0.46, rng, roll=0.03)
        else:
            pat = [COMP_ONE["hold"], COMP_ONE["two"], COMP_ONE["charleston"]][int(rng.integers(0, 3))]
            for beat, beats, vm, ci in pat:
                rhodes_chord(take, bi * 4 + max(0, beat), voicings[bi][0], beats, 0.46 * vm, rng)
    rng = kr(seed, "land")
    rhodes_chord(take, 56, voicings[14][0], 7.5, 0.52, rng, 1.1, roll=0.05)
    melody(take, 0, END_MEL, kr(seed, "mel"), 0.95)
    # The studio ident's three notes, landing home.
    for beat, name, beats, v in ((56.5, "D5", 6.5, .56), (57.0, "A5", 6.0, .62), (57.5, "D6", 6.0, .68)):
        vibes_note(take, beat, midi(name), beats, v, rng, 0.95, pan=0.0)
    for bi in range(14):
        rng = kr(seed, "kit", bi)
        for h in (0.0, 2.0):
            swish(take, bi * 4 + h, 2.0, 0.38 if bi < 12 else 0.3, rng)
        if bi < 12:
            for h in (1.0, 3.0):
                tap(take, bi * 4 + h, 0.32, rng)
                foot(take, bi * 4 + h, 0.3, rng)
        if bi in (7, 11):
            brush_fill(take, bi, rng, 0.2, 0.4)
        if 8 <= bi < 12:
            for h, v in ((0, .44), (1, .54), (1.5, .3), (2, .44), (3, .54), (3.5, .3)):
                ride(take, bi * 4 + h, v, rng, 0.6)
    rng = kr(seed, "kit_end")
    ride(take, 56, 0.42, rng, 0.7)
    swish(take, 56, 4.0, 0.26, rng)
    return take


# ====================================================================== mixing

class Bus:
    """Mixing helpers that run circularly on loops (state wraps across the seam)."""

    def __init__(self, loop, L):
        self.loop = loop
        self.L = L
        self.P = min(n_of(4.0), L)

    def circ(self, fn, x):
        if not self.loop:
            return fn(x)
        P = self.P
        xp = np.concatenate([x[:, -P:], x, x[:, :P]], axis=1)
        return fn(xp)[:, P:P + self.L]

    def rev(self, x, ir):
        y = dsp.convolve(x, ir)
        if not self.loop:
            return y[:, :x.shape[1]]
        out = y[:, :self.L].copy()
        tail = y[:, self.L:]
        out[:, :tail.shape[1]] += tail
        return out

    def lfo(self, n, rate):
        sec = n / SR
        if self.loop:
            rate = max(1, round(rate * sec)) / sec   # whole cycles per loop: the seam can't hear it
        t = np.arange(n) / SR
        return 0.5 + 0.5 * np.sin(2 * np.pi * rate * t)


LEVELS = {  # stem loudness relative to the mix (LU), before the master
    "title": dict(bass=-10.0, kit=-18.0, ride=-30.0, snaps=-30.0, rhodes=-4.5, vibes=-4.5, pad=-11.5),
    "shop": dict(bass=-6.5, kit=-10.0, ride=-14.5, snaps=-30.0, rhodes=-6.0, vibes=-5.0, pad=-30.0),
    "busy": dict(bass=-6.5, kit=-8.5, ride=-10.0, snaps=-13.5, rhodes=-6.0, vibes=-5.5, pad=-30.0),
    "end": dict(bass=-7.5, kit=-11.5, ride=-15.5, snaps=-30.0, rhodes=-5.0, vibes=-4.0, pad=-30.0),
}
TARGET_LUFS = {"title": -21.0, "shop": -20.0, "busy": -19.5, "end": -20.0}
TP_MAX = -2.0


def mix(take, kind, ref_lufs=None):
    st = take.fold()
    n = st["bass"].shape[1]
    bus = Bus(take.loop, n)

    bass = bus.circ(lambda x: dsp.hp(x, 40.0, 2), st["bass"])
    bass = bus.circ(lambda x: dsp.peak_eq(x, 150.0, 2.5, 0.8), bass)
    bass = bus.circ(lambda x: dsp.shelf(x, 90.0, -1.5, high=False), bass)
    bass = bus.circ(lambda x: dsp.lp(x, 4200.0, 2), bass)
    bass = bus.circ(lambda x: dsp.compressor(x, -24.0, 2.5, 0.006, 0.14), bass)
    kit = bus.circ(lambda x: dsp.shelf(dsp.hp(x, 70.0, 2), 9000.0, -2.0), st["kit"])
    ride_ = bus.circ(lambda x: dsp.lp(dsp.shelf(dsp.hp(x, 320.0, 2), 7000.0, -5.0), 11500.0, 2), st["ride"])
    snaps = bus.circ(lambda x: dsp.hp(x, 400.0, 2), st["snaps"])
    trem = bus.lfo(n, 4.2)
    rh = st["rhodes"]
    rh = np.vstack([rh[0] * (0.78 + 0.22 * trem), rh[1] * (0.78 + 0.22 * (1 - trem))])
    rh = bus.circ(lambda x: dsp.peak_eq(dsp.shelf(x, 3200.0, -1.0), 450.0, -1.5, 1.0), rh)
    rh = bus.circ(lambda x: dsp.hp(x, 90.0, 2), rh)
    motor = 1.0 - 0.22 * bus.lfo(n, 5.0)
    vib = bus.circ(lambda x: dsp.peak_eq(x, 2400.0, 1.5, 0.9), st["vibes"] * motor)
    pad = bus.circ(lambda x: dsp.hp(dsp.lp(x, 1500.0, 2), 120.0, 2), st["pad"])
    parts = dict(bass=bass, kit=kit, ride=ride_, snaps=snaps, rhodes=rh, vibes=vib, pad=pad)

    # Balance by numbers: each stem sits at its loudness relative to a fixed reference.
    lv = LEVELS[kind]
    ref = -14.0
    gains = {}
    for k, x in parts.items():
        l = dsp.lufs(x)
        if l <= -69.0:
            gains[k] = 0.0
            continue
        g = dsp.undb(ref + lv[k] - l)
        gains[k] = g
        parts[k] = x * g

    plate = dsp.reverb_ir(2.4, predelay=0.03, damp=0.5, width=0.9, seed=31)
    room = dsp.reverb_ir(0.6, predelay=0.008, damp=0.7, width=0.6, seed=32)
    p = parts
    dry = sum(p.values())
    send_plate = p["vibes"] * 0.34 + p["rhodes"] * 0.24 + p["snaps"] * 0.3 + p["ride"] * 0.1 + p["kit"] * 0.12 + p["pad"] * 0.3
    send_room = p["kit"] * 0.28 + p["bass"] * 0.06 + p["snaps"] * 0.2
    wet_mix = 0.72 if kind != "title" else 0.95
    mixd = dry + bus.rev(send_plate, plate) * wet_mix + bus.rev(send_room, room) * 0.5
    mixd = bus.circ(lambda x: dsp.hp(x, 28.0, 2), mixd)
    mixd = bus.circ(lambda x: dsp.compressor(x, -22.0, 1.8, 0.03, 0.35, makeup_db=0.0), mixd)
    mixd = bus.circ(lambda x: dsp.shelf(x, 10000.0, -1.5), mixd)

    target = TARGET_LUFS[kind]
    out = mixd
    ceiling = -3.5
    for _ in range(6):
        out = mixd * dsp.undb(target - dsp.lufs(mixd))
        out = bus.circ(lambda x: dsp.limiter(x, ceiling), out)
        out = out * dsp.undb(target - dsp.lufs(out))
        if dsp.true_peak_db(out) <= TP_MAX - 0.6:   # headroom for the Vorbis encode
            break
        ceiling -= 0.7
    if not take.loop:
        # Let the last chord ring out, then a gentle fade to true silence.
        env = np.max(np.abs(out), axis=0)
        last = np.nonzero(env > dsp.undb(-62.0))[0]
        end = min(out.shape[1], (last[-1] if last.size else out.shape[1]) + n_of(0.3))
        out = out[:, :end]
        out = dsp.fade(out, 0.0, 2.5)
    return out, gains


# ====================================================================== export

def write_wav(path, x):
    wavfile.write(path, SR, np.ascontiguousarray(x.T.astype(np.float32)))


def ffmpeg(*args):
    subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-y", *args], check=True)


def export(name, x, game=True):
    os.makedirs(OUT_DIR, exist_ok=True)
    os.makedirs(WORK_DIR, exist_ok=True)
    wav = os.path.join(WORK_DIR, name + ".wav")
    write_wav(wav, x)
    if game:
        os.makedirs(GAME_DIR, exist_ok=True)
        ogg = os.path.join(GAME_DIR, name + ".ogg")
        ffmpeg("-i", wav, "-c:a", "libvorbis", "-q:a", "5", "-ar", str(SR), "-ac", "2", ogg)
    mp3 = os.path.join(OUT_DIR, name + ".mp3")
    ffmpeg("-i", wav, "-c:a", "libmp3lame", "-b:a", "256k", mp3)
    return wav


CUES = {
    "title": ("title_night", lambda: render_title()),
    "shop": ("shop_day", lambda: render_shop(False)),
    "busy": ("shop_busy", lambda: render_shop(True)),
    "end": ("demo_end", lambda: render_end()),
}


def demo_mix(audio):
    """Title (past its loop point) -> shop -> busy (sample-locked switch) -> end, crossfading
    the way the game does (1.5 s equal-power)."""
    xf = n_of(1.5)

    def seg(x, start, dur, loop=True):
        n = n_of(dur)
        idx = (np.arange(n) + n_of(start)) % x.shape[1] if loop else np.arange(n_of(start), min(x.shape[1], n_of(start) + n))
        return x[:, idx]

    def cross(a, b):
        t = np.linspace(0, np.pi / 2, xf)
        mid = a[:, -xf:] * np.cos(t) + b[:, :xf] * np.sin(t)
        return np.concatenate([a[:, :-xf], mid, b[:, xf:]], axis=1)

    title, shop, busy, end = audio["title"], audio["shop"], audio["busy"], audio["end"]
    a = seg(title, 0, 95.0)                    # the title loops once at 80 s
    shop_part = seg(shop, 0, 120.0)
    out = cross(a, shop_part)
    busy_part = seg(busy, 120.0 - 1.5, 60.0)    # same position: the band just digs in
    # busy starts where the shop crossfade window starts, so both are at the same loop time
    out = cross(out, busy_part)
    out = cross(out, np.concatenate([end, np.zeros((2, n_of(1.0)))], axis=1))
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--cue", default="all")
    ap.add_argument("--no-export", action="store_true")
    args = ap.parse_args()
    which = list(CUES) if args.cue == "all" else args.cue.split(",")
    audio = {}
    for k in which:
        name, fn = CUES[k]
        t0 = time.time()
        take = fn()
        out, gains = mix(take, k)
        audio[k] = out
        print(f"{name}: {out.shape[1] / SR:.2f} s, {dsp.lufs(out):.2f} LUFS, TP {dsp.true_peak_db(out):.2f} dBTP"
              f"  [{time.time() - t0:.0f} s]", flush=True)
        if not args.no_export:
            export(name, out)
    if not args.no_export and all(k in audio for k in CUES):
        dm = demo_mix(audio)
        write_wav(os.path.join(WORK_DIR, "demo_mix.wav"), dm)
        ffmpeg("-i", os.path.join(WORK_DIR, "demo_mix.wav"), "-c:a", "libmp3lame", "-b:a", "256k",
               os.path.join(OUT_DIR, "demo_mix.mp3"))
        print(f"demo_mix: {dm.shape[1] / SR:.1f} s", flush=True)


if __name__ == "__main__":
    main()
