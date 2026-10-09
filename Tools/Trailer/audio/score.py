"""The trailer score: an original noir caper in D minor, 100 BPM, swung. Upright bass, brushes,
finger snaps, Rhodes and vibraphone over Dm9 | Bb13 | Gm9 | A7b9. The arrangement is driven by
a bar-by-bar plan from the edit, so music and picture come from one timeline."""
import numpy as np

from . import dsp, inst
from .dsp import SR, n_of
from .inst import hz, midi

BPM = 100.0
BEAT = 60.0 / BPM
BAR = 4 * BEAT
SWING = 0.64  # where the off-beat eighth lands inside the beat

CHORDS = ["Dm9", "Bb13", "Gm9", "A7b9"]
VOICINGS = {
    "Dm9": ["F3", "A3", "C4", "E4"],
    "Bb13": ["Ab3", "C4", "D4", "G4"],
    "Gm9": ["Bb3", "D4", "F4", "A4"],
    "A7b9": ["C#4", "F4", "G4", "Bb4"],
    "DmM9": ["D3", "F3", "A3", "C#4", "E4"],
}

WALK = [
    [["D2", "F2", "A2", "B1"], ["Bb1", "D2", "F2", "Ab1"], ["G1", "Bb1", "D2", "Bb1"], ["A1", "C#2", "E2", "Eb2"]],
    [["D2", "E2", "F2", "C2"], ["Bb1", "C2", "D2", "F#1"], ["G1", "D2", "Bb1", "G#1"], ["A1", "C#2", "E2", "C#2"]],
]

# (beat in bar, note, length in beats, velocity); x.5 positions get swung.
SNEAK = [
    [(0.0, "D2", 0.4, 0.95), (0.5, "D2", 0.25, 0.5), (1.5, "F2", 0.35, 0.8), (2.0, "G2", 0.35, 0.75), (2.5, "G#2", 0.35, 0.78), (3.0, "A2", 0.55, 0.92)],
    [(0.0, "D2", 0.4, 0.95), (0.5, "D2", 0.25, 0.5), (1.5, "C2", 0.35, 0.8), (2.0, "A1", 0.45, 0.85), (3.0, "G#1", 0.35, 0.75), (3.5, "A1", 0.45, 0.82)],
]

THEME_A = [
    [(0.5, "A4", 0.4, 0.7), (1.0, "D5", 0.45, 0.8), (1.5, "F5", 0.4, 0.75), (2.0, "E5", 1.3, 0.9), (3.5, "D5", 0.45, 0.72)],
    [(0.0, "C5", 1.4, 0.85), (2.0, "Ab4", 0.45, 0.7), (2.5, "G4", 1.2, 0.8)],
    [(0.5, "Bb4", 0.4, 0.7), (1.0, "D5", 0.45, 0.8), (1.5, "G5", 0.4, 0.8), (2.0, "F5", 1.3, 0.9), (3.5, "E5", 0.45, 0.72)],
    [(0.0, "F5", 0.9, 0.85), (1.0, "E5", 0.45, 0.75), (1.5, "Bb4", 0.45, 0.75), (2.0, "C#5", 1.6, 0.9)],
]
THEME_A2 = [
    [(0.0, "D5", 0.45, 0.8), (0.5, "F5", 0.45, 0.75), (1.0, "A5", 1.2, 0.92), (2.5, "G#5", 0.45, 0.7), (3.0, "A5", 0.9, 0.85)],
    [(0.0, "Ab5", 1.4, 0.88), (2.0, "G5", 0.45, 0.75), (2.5, "F5", 0.45, 0.7), (3.0, "D5", 0.9, 0.8)],
    [(0.5, "A4", 0.4, 0.7), (1.0, "Bb4", 0.45, 0.75), (1.5, "D5", 0.4, 0.75), (2.0, "F5", 1.4, 0.88)],
    [(0.0, "E5", 0.45, 0.8), (0.5, "C#5", 0.45, 0.75), (1.0, "Bb4", 0.45, 0.75), (1.5, "A4", 0.45, 0.7), (2.0, "C#5", 0.4, 0.8), (2.5, "E5", 0.4, 0.8), (3.5, "D5", 1.6, 0.9)],
]
THEME_B = [
    [(0.0, "D5", 0.22, 0.8), (0.5, "D5", 0.22, 0.6), (1.5, "F5", 0.22, 0.75), (2.0, "A5", 0.4, 0.85), (3.0, "G#5", 0.22, 0.7), (3.5, "A5", 0.5, 0.82)],
    [(1.0, "Ab5", 0.22, 0.75), (1.5, "G5", 0.22, 0.7), (2.0, "F5", 0.22, 0.75), (2.5, "D5", 0.7, 0.85)],
    [(0.0, "G5", 0.22, 0.8), (0.5, "G5", 0.22, 0.6), (1.5, "Bb5", 0.22, 0.75), (2.0, "A5", 0.4, 0.85), (3.0, "F5", 0.22, 0.7), (3.5, "G5", 0.5, 0.82)],
    [(1.0, "F5", 0.22, 0.75), (1.5, "E5", 0.22, 0.7), (2.0, "C#5", 0.22, 0.75), (2.5, "A4", 0.8, 0.85)],
]
INTRO = [
    [(0.0, "A4", 1.0, 0.55), (1.0, "D5", 1.0, 0.6), (2.0, "F5", 1.0, 0.62), (3.0, "E5", 3.0, 0.7)],
    [(2.5, "D5", 1.0, 0.55), (3.5, "C#5", 2.0, 0.6)],
]

COMP = [  # Rhodes rhythm per bar: (beat, length)
    [(0.0, 0.85), (1.5, 0.3)],
    [(0.5, 0.3), (2.0, 1.3)],
    [(0.0, 0.85), (2.5, 0.3), (3.5, 0.4)],
    [(1.5, 0.35), (2.0, 1.4)],
]


def swing(beat):
    whole = np.floor(beat)
    frac = beat - whole
    if abs(frac - 0.5) < 1e-6:
        return whole + SWING
    return beat


class Score:
    """Renders the plan into stems. A plan entry per bar: dict(kind=..., plus options)."""

    def __init__(self, plan, length, start=0.0, seed=7):
        self.plan = plan
        self.start = start
        self.n = n_of(length)
        self.rng = np.random.default_rng(seed)
        self.stems = {k: dsp.stereo(self.n) for k in
                      ("bass", "kit", "ride", "snaps", "rhodes", "vibes", "pad", "fx")}

    # ---------------------------------------------------------- placement
    def at(self, bar, beat):
        t = self.start + bar * BAR + swing(beat) * BEAT
        return n_of(t + self.rng.normal(0, 0.0035)) if beat > 0 or bar > 0 else n_of(t)

    def add(self, stem, bar, beat, y, gain=1.0, p=0.0, exact=False):
        s = n_of(self.start + bar * BAR + swing(beat) * BEAT) if exact else self.at(bar, beat)
        y = dsp.pan(y, p) if y.ndim == 1 else y
        dsp.place(self.stems[stem], y, s, gain)

    def vel(self, v):
        return float(np.clip(v + self.rng.normal(0, 0.04), 0.05, 1.0))

    # ---------------------------------------------------------- parts
    def bass_walk(self, bar, var=0, gain=1.0, ci=None):
        line = WALK[var % 2][(bar if ci is None else ci) % 4]
        for i, name in enumerate(line):
            accent = 0.92 if i == 0 else 0.8
            y = inst.bass(hz(midi(name)), BEAT * 0.88, self.vel(accent), self.rng)
            self.add("bass", bar, float(i), y, gain)
            if self.rng.random() < 0.28 and i < 3:  # a dead-note rake into the next beat
                nxt = line[i + 1]
                dn = inst.bass(hz(midi(nxt)), 0.08, 0.5, self.rng, dead=True)
                self.add("bass", bar, i + 0.5, dn, gain * 0.6)

    def bass_sneak(self, bar, gain=1.0):
        for beat, name, length, v in SNEAK[bar % 2]:
            y = inst.bass(hz(midi(name)), length * BEAT, self.vel(v), self.rng)
            self.add("bass", bar, beat, y, gain)

    def bass_note(self, bar, beat, name, beats, v=0.9, gain=1.0):
        self.add("bass", bar, beat, inst.bass(hz(midi(name)), beats * BEAT, v, self.rng), gain, exact=True)

    def ride(self, bar, gain=1.0, half=False):
        hits = [(0.0, 0.62), (1.0, 0.85), (1.5, 0.5), (2.0, 0.62), (3.0, 0.85), (3.5, 0.5)]
        for i, (beat, v) in enumerate(hits):
            if half and beat >= 2.0:
                break
            self.add("ride", bar, beat, inst.ride(self.vel(v), int(self.rng.integers(0, 3))), gain, p=0.35)

    def brushes(self, bar, gain=1.0, ghosts=True, half=False):
        for beat in (1.0, 3.0):
            if half and beat >= 2.0:
                break
            self.add("kit", bar, beat, inst.brush_tap(self.vel(0.62), self.rng), gain, p=-0.1)
            self.add("kit", bar, beat, inst.hat_foot(self.vel(0.55), self.rng), gain * 0.8, p=0.15)
        if ghosts:
            for beat in (0.5, 2.5):
                if self.rng.random() < 0.6 and not (half and beat >= 2):
                    self.add("kit", bar, beat, inst.brush_tap(self.vel(0.2), self.rng), gain, p=-0.1)
        for h in (0.0, 2.0):
            if half and h >= 2:
                break
            sw = inst.brush_swish(BEAT * 2.0, self.vel(0.5), self.rng)
            self.add("kit", bar, h, sw, gain, p=-0.15)

    def kick(self, bar, beats=(0.0, 1.0, 2.0, 3.0), v=0.4, gain=1.0):
        for b in beats:
            vv = v * (1.35 if b == 0.0 else 1.0)
            self.add("kit", bar, b, inst.kick(self.vel(vv), self.rng), gain)

    def snaps(self, bar, gain=1.0):
        for beat in (1.0, 3.0):
            self.add("snaps", bar, beat, inst.snap(self.vel(0.8), self.rng), gain, p=0.25 if beat == 1.0 else -0.2)

    def fill(self, bar, from_beat=3.0, gain=1.0):
        """Brush triplets into the next bar, with a rim accent."""
        k = 0
        b = from_beat
        while b < 4.0 - 1e-6:
            for j in range(3):
                pos = b + j / 3.0
                v = 0.35 + 0.18 * k
                self.add("kit", bar, pos, inst.brush_tap(self.vel(min(v, 0.95)), self.rng), gain, p=-0.1, exact=True)
                k += 1
            b += 1.0
        self.add("kit", bar, 3.0 + 2 / 3.0, inst.rim(0.7, self.rng), gain * 0.7, exact=True)

    def comp(self, bar, chord=None, gain=1.0, pattern=None, v=0.62):
        chord = chord or CHORDS[bar % 4]
        rhythm = COMP[(pattern if pattern is not None else bar) % 4]
        for beat, length in rhythm:
            for i, name in enumerate(VOICINGS[chord]):
                y = inst.rhodes(hz(midi(name)), length * BEAT, self.vel(v))
                self.add("rhodes", bar, beat, y, gain * 0.55, p=-0.25 + 0.17 * i)

    def chord_hold(self, bar, chord, beats, v=0.55, gain=1.0, beat=0.0, stem="rhodes"):
        for i, name in enumerate(VOICINGS[chord]):
            if stem == "rhodes":
                y = inst.rhodes(hz(midi(name)), beats * BEAT, v)
            else:
                y = inst.vibes(hz(midi(name)), beats * BEAT, v, self.rng)
            self.add(stem, bar, beat, y, gain * 0.5, p=-0.3 + 0.2 * i, exact=True)

    def melody(self, bar, phrase, idx, gain=1.0, octave=0, double=False):
        for beat, name, length, v in phrase[idx % len(phrase)]:
            m = midi(name) + 12 * octave
            y = inst.vibes(hz(m), length * BEAT, self.vel(v), self.rng)
            self.add("vibes", bar, beat, y, gain, p=0.2)
            if double:
                r = inst.rhodes(hz(m - 12), length * BEAT, min(1.0, v * 0.95))
                self.add("rhodes", bar, beat, r, gain * 0.42, p=-0.15)

    def pad(self, bar, chord, beats, gain=1.0):
        names = VOICINGS[chord]
        freqs = [hz(midi(n) - 12) for n in names[:3]]
        y = inst.pad(freqs, beats * BEAT, 0.5, attack=1.6, release=1.8)
        self.add("pad", bar, 0.0, y, gain, exact=True)

    # ---------------------------------------------------------- render
    def render(self):
        theme_bar = 0
        for bar, spec in enumerate(self.plan):
            kind = spec.get("kind", "rest")
            gain = spec.get("gain", 1.0)
            ci = spec.get("m", theme_bar) % 4 if kind in ("groove", "groove_b") else 0
            chord = CHORDS[ci]
            if kind == "ident":
                # The studio sting: three vibraphone notes rising with the logo, a warm chord under the name.
                for beat, name, v in ((0.5, "D5", 0.6), (1.0, "A5", 0.66), (1.5, "D6", 0.72)):
                    self.add("vibes", bar, beat, inst.vibes(hz(midi(name)), 2.5 * BEAT, v, self.rng), 0.95, p=0.0, exact=True)
                self.chord_hold(bar, "Dm9", 3.6, v=0.4, gain=0.75, beat=2.0)
                self.pad(bar, "Dm9", 4.0, gain=0.5)
            elif kind == "intro":
                if spec.get("pad", True):
                    self.pad(bar, "Dm9", 4.5)
                self.melody(bar, INTRO, spec.get("i", 0), 0.75)
            elif kind == "sneak":
                self.bass_sneak(bar, gain)
                if spec.get("snaps", True):
                    self.snaps(bar, 0.9)
                if spec.get("ride", False):
                    self.ride(bar, 0.45)
                if spec.get("brushes", False):
                    self.brushes(bar, 0.7, ghosts=False)
                if spec.get("stabs", False):
                    for beat, length in ((1.5, 0.3), (3.5, 0.3)):
                        for i, name in enumerate(VOICINGS["Dm9"]):
                            self.add("rhodes", bar, beat, inst.rhodes(hz(midi(name)), length * BEAT, 0.5), 0.45, p=-0.2 + 0.13 * i)
                if spec.get("fill", False):
                    self.fill(bar, 3.0)
            elif kind in ("groove", "groove_b"):
                half = spec.get("half", False)
                self.bass_walk(bar, spec.get("walk", bar // 4), gain, ci)
                self.ride(bar, 0.9 * gain, half=half)
                self.brushes(bar, 0.95 * gain, half=half)
                self.kick(bar, (0.0, 2.0) if not spec.get("drive") else (0.0, 1.0, 2.0, 3.0), 0.42 if not spec.get("drive") else 0.5, gain)
                if spec.get("snaps", False):
                    self.snaps(bar, 0.7)
                if not half:
                    self.comp(bar, chord=chord, gain=gain, pattern=ci)
                phrase = THEME_B if kind == "groove_b" else (THEME_A if (spec.get("phrase", theme_bar // 4) % 2 == 0) else THEME_A2)
                if spec.get("melody", True):
                    self.melody(bar, phrase, spec.get("m", theme_bar), gain * 0.95, octave=spec.get("octave", 0), double=spec.get("double", False))
                theme_bar += 1
                if spec.get("hit", False):
                    self.add("kit", bar, 0.0, inst.kick(0.85, self.rng), 1.0, exact=True)
                    self.add("kit", bar, 0.0, inst.brush_tap(0.95, self.rng), 1.0, exact=True)
                    self.add("ride", bar, 0.0, inst.crash(0.55), 0.6, p=0.3, exact=True)
                    for i, name in enumerate(VOICINGS[chord]):
                        self.add("rhodes", bar, 0.0, inst.rhodes(hz(midi(name)), 0.5 * BEAT, 0.85), 0.5, p=-0.25 + 0.17 * i, exact=True)
                if spec.get("fill", False):
                    self.fill(bar, spec.get("fill_from", 3.0))
            elif kind == "break":
                self.bass_sneak(bar, 0.8)
                self.ride(bar, 0.5)
                if spec.get("spy", True) and spec.get("spy_here", bar % 2 == 0):
                    self.chord_hold(bar, "DmM9", 7.5, v=0.42, gain=0.9, stem="vibes")
                if "ding" in spec:
                    b = spec["ding"]
                    self.add("vibes", bar, b, inst.vibes(hz(midi("A5")), 0.5 * BEAT, 0.75, self.rng), 1.0, exact=True)
                    self.add("vibes", bar, b + 0.5, inst.vibes(hz(midi("D6")), 2.5 * BEAT, 0.8, self.rng), 1.0, exact=True)
                if spec.get("fill", False):
                    self.fill(bar, 3.0)
            elif kind == "hits":
                # Stop-time: band hits on 1 and the and-of-2, ring in between.
                for beat in (0.0, 1.5):
                    self.bass_note(bar, beat, "D2" if beat == 0 else "A1", 0.35, 0.95)
                    self.add("kit", bar, beat, inst.kick(0.75, self.rng), 1.0, exact=True)
                    self.add("kit", bar, beat, inst.brush_tap(0.9, self.rng), 0.9, exact=True)
                    for i, name in enumerate(VOICINGS["Dm9"]):
                        self.add("rhodes", bar, beat, inst.rhodes(hz(midi(name)), 0.35 * BEAT, 0.8), 0.5, p=-0.25 + 0.17 * i, exact=True)
                self.add("ride", bar, 0.0, inst.ride(0.9, 1), 0.8, p=0.35, exact=True)
            elif kind == "end":
                # The logo lands: the whole band on D minor-major 9, bass on the floor, then a tag.
                self.bass_note(bar, 0.0, "D1", 6.0, 1.0, 1.1)
                self.bass_note(bar, 0.0, "D2", 6.0, 0.8, 0.7)
                self.add("kit", bar, 0.0, inst.kick(1.0, self.rng), 1.3, exact=True)
                self.add("ride", bar, 0.0, inst.crash(1.0), 1.0, p=0.25, exact=True)
                self.chord_hold(bar, "DmM9", 8.0, v=0.75, gain=1.1)
                self.chord_hold(bar, "DmM9", 8.0, v=0.65, gain=0.8, stem="vibes")
                tag = spec.get("tag", True)
                if tag:
                    # A two-note wink from the vibes after the dust settles.
                    self.add("vibes", bar + 1, 2.0, inst.vibes(hz(midi("A5")), 0.4 * BEAT, 0.6, self.rng), 0.9, exact=True)
                    self.add("vibes", bar + 1, 2.5, inst.vibes(hz(midi("D6")), 3.0 * BEAT, 0.7, self.rng), 0.9, exact=True)
                    self.bass_note(bar + 1, 2.5, "D2", 0.4, 0.7, 0.8)
            elif kind == "rest":
                if "fill_from" in spec:
                    self.fill(bar, spec["fill_from"])
            # anything else: silence from the band.
        return self.stems
