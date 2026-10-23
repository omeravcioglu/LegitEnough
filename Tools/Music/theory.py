"""Harmony for the in-game score: chord symbols, voice-led Rhodes voicings, guide-tone lines and
a rule-based walking bass. Pure Python + numpy; every choice is seeded, so a cue renders the same
every time."""
import itertools

import numpy as np

PC = {"C": 0, "C#": 1, "Db": 1, "D": 2, "D#": 3, "Eb": 3, "E": 4, "F": 5, "F#": 6, "Gb": 6,
      "G": 7, "G#": 8, "Ab": 8, "A": 9, "A#": 10, "Bb": 10, "B": 11}

# quality -> (upper structure for the Rhodes as intervals over the root,
#             chord tones the bass leans on, the chord scale for passing tones)
QUAL = {
    "madd9":   ((3, 7, 12, 14), (0, 3, 7), (0, 2, 3, 5, 7, 9, 10)),
    "mM9":     ((3, 7, 11, 14), (0, 3, 7, 11), (0, 2, 3, 5, 7, 9, 11)),
    "m9":      ((3, 7, 10, 14), (0, 3, 7, 10), (0, 2, 3, 5, 7, 9, 10)),
    "m69":     ((3, 7, 9, 14), (0, 3, 7, 9), (0, 2, 3, 5, 7, 9, 10)),
    "m11":     ((3, 7, 10, 17), (0, 3, 7, 10), (0, 2, 3, 5, 7, 9, 10)),
    "m7b5":    ((3, 6, 10, 17), (0, 3, 6, 10), (0, 1, 3, 5, 6, 8, 10)),
    "7b9":     ((4, 10, 13, 20), (0, 4, 7, 10), (0, 1, 4, 5, 7, 8, 10)),
    "13":      ((4, 10, 14, 21), (0, 4, 7, 10), (0, 2, 4, 5, 7, 9, 10)),
    "7sus":    ((5, 10, 14, 21), (0, 5, 7, 10), (0, 2, 5, 7, 9, 10)),
    "maj9":    ((4, 7, 11, 14), (0, 4, 7, 11), (0, 2, 4, 5, 7, 9, 11)),
    "maj7#11": ((4, 11, 14, 18), (0, 4, 7, 11), (0, 2, 4, 6, 7, 9, 11)),
    "69":      ((4, 9, 14, 19), (0, 4, 7, 9), (0, 2, 4, 7, 9, 11)),
}
# Guide tones (3rd and 7th, or what stands in for them).
GUIDE = {"madd9": (3, 14), "mM9": (3, 11), "m9": (3, 10), "m69": (3, 9), "m11": (3, 10), "m7b5": (3, 10),
         "7b9": (4, 10), "13": (4, 10), "7sus": (5, 10), "maj9": (4, 11), "maj7#11": (4, 11), "69": (4, 9)}


class Chord:
    __slots__ = ("root", "qual", "beats")

    def __init__(self, root, qual, beats=4.0):
        self.root = PC[root]
        self.qual = qual
        self.beats = beats

    def pcs(self, which=0):
        return [(self.root + i) % 12 for i in QUAL[self.qual][which]]

    def tones(self):
        return set(self.pcs(1))

    def scale(self):
        return set(self.pcs(2))

    def upper(self):
        return set(self.pcs(0))

    def guide(self):
        return [(self.root + i) % 12 for i in GUIDE[self.qual]]

    def __repr__(self):
        return f"{self.root}:{self.qual}"


def bar(*chords):
    """bar('D madd9') or bar('E m7b5', 'A 7b9'): one bar, chords split evenly."""
    k = len(chords)
    out = []
    for c in chords:
        r, q = c.split()
        out.append(Chord(r, q, 4.0 / k))
    return out


def notes_in(pc, lo, hi):
    return [m for m in range(lo, hi + 1) if m % 12 == pc]


# ------------------------------------------------------------------ Rhodes voicings

def voice(chord, prev=None, lo=51, hi=74, center=62.0):
    """Four-note rootless voicing nearest the previous one (smallest total motion), inside the
    keyboard's sweet spot, with no muddy close intervals at the bottom."""
    pcs = chord.pcs(0)
    best, best_cost = None, 1e9
    for combo in itertools.product(*[notes_in(p, lo, hi) for p in pcs]):
        s = sorted(combo)
        if len(set(s)) < 4:
            continue
        span = s[-1] - s[0]
        if span < 7 or span > 16:
            continue
        gaps = np.diff(s)
        if s[0] < 57 and gaps[0] < 3:
            continue          # low-interval limit: no seconds down in the mud
        if s[0] < 54 and gaps[0] < 5:
            continue
        cost = 0.0 if prev is None else float(np.sum(np.abs(np.array(s) - np.array(prev))))
        cost += 0.22 * abs(np.mean(s) - center)
        if gaps[-1] == 1:
            cost += 2.0       # a minor second on top rubs against the melody
        if best is None or cost < best_cost:
            best, best_cost = s, cost
    return best


def voice_chart(chart, start=None, **kw):
    """Voice every chord of a chart (list of bars of Chords) in order."""
    prev = start
    out = []
    for b in chart:
        vb = []
        for c in b:
            v = voice(c, prev, **kw)
            vb.append(v)
            prev = v
        out.append(vb)
    return out


# ------------------------------------------------------------------ walking bass

LO, HI = 28, 50   # E1 .. D3


def _nearest(pc, ref, lo=LO, hi=HI):
    c = notes_in(pc, lo, hi)
    return min(c, key=lambda m: (abs(m - ref), m))


def _step_cost(a, b):
    d = abs(a - b)
    if d == 0:
        return 5.0
    if d <= 2:
        return 0.0
    if d <= 4:
        return 0.45
    if d == 5:
        return 1.0
    if d == 7:
        return 1.3
    return 2.2 + 0.4 * (d - 7)


def _approach(target, prev, chord, rng):
    """The note on the beat before a chord change: chromatic from either side, the target's
    fifth, or a scale step."""
    opts = [(target + 1, 0.3), (target - 1, 0.3), (target + 7, 0.1), (target - 5, 0.12)]
    for d in (-2, 2):
        if (target + d) % 12 in chord.scale():
            opts.append((target + d, 0.14))
    opts = [(m, w) for m, w in opts if LO <= m <= HI and m != prev]
    w = np.array([o[1] for o in opts])
    return opts[int(rng.choice(len(opts), p=w / w.sum()))][0]


def walk(chart, rng, start=38, feel=None, next_chord=None, end_target=None):
    """Walking bass over a chart. Returns [(abs_beat, midi, beats, accent)] plus ghost notes as
    (abs_beat, midi, beats, -1). `feel[bar]` = 'walk' or 'two' (half notes). `next_chord` and
    `end_target` say where the line goes after the chart (otherwise it wraps to its own top)."""
    flat = []
    for bi, b in enumerate(chart):
        beat = 0.0
        for c in b:
            flat.append((bi * 4 + beat, c))
            beat += c.beats
    out = []
    prev = start
    for i, (at, c) in enumerate(flat):
        last = i + 1 == len(flat)
        nxt = (next_chord if (last and next_chord is not None) else flat[(i + 1) % len(flat)][1])
        bi = int(at // 4)
        mode = feel[bi] if feel else "walk"
        k = int(round(c.beats))
        # Beat one: the root, near where the line is; on a repeated chord sometimes the 3rd or 5th.
        same = i > 0 and flat[i - 1][1].root == c.root and flat[i - 1][1].qual[0] == c.qual[0]
        recolour = i > 0 and flat[i - 1][1].root == c.root and flat[i - 1][1].qual != c.qual and c.beats < 4
        if recolour:
            pc = (c.root + QUAL[c.qual][1][1]) % 12      # Dm9 -> D7b9: show the new 3rd (F#)
        elif same and rng.random() < 0.35:
            pc = (c.root + (QUAL[c.qual][1][1] if rng.random() < 0.5 else 7)) % 12
        else:
            pc = c.root
        n1 = _nearest(pc, prev)
        if abs(n1 - prev) > 7 and rng.random() < 0.7:
            alt = n1 - 12 if n1 > prev else n1 + 12
            if LO <= alt <= HI:
                n1 = alt
        tgt = end_target if (last and end_target is not None) else _nearest(nxt.root, n1)
        if mode == "two":
            if k >= 4:
                out.append((at, n1, 1.85, 1.0))
                if nxt.root != c.root and rng.random() < 0.45:
                    n3 = _approach(tgt, n1, c, rng)
                else:
                    n3 = _nearest((c.root + 7) % 12, n1)
                    if n3 == n1:
                        n3 = _nearest((c.root + 3) % 12, n1)
                out.append((at + 2, n3, 1.8, 0.85))
                if rng.random() < 0.3:
                    out.append((at + 3.5, _approach(tgt, n3, c, rng), 0.4, 0.6))
                prev = n3
            else:
                out.append((at, n1, 1.85, 1.0))
                prev = n1
            continue
        if k >= 4:
            n4 = _approach(tgt, n1, c, rng)
            cand = [m for m in range(LO, HI + 1) if m % 12 in c.scale()]
            best, bc = None, 1e9
            for n2 in cand:
                for n3 in cand:
                    s = _step_cost(n1, n2) + _step_cost(n2, n3) + _step_cost(n3, n4)
                    s += 0.0 if n3 % 12 in c.tones() else 0.8
                    s += 0.0 if n2 % 12 in c.tones() else 0.25
                    d1, d2, d3 = np.sign(n2 - n1), np.sign(n3 - n2), np.sign(n4 - n3)
                    if d1 != d2 and d2 != d3:
                        s += 0.7
                    s += 0.03 * (abs(n2 - 40) + abs(n3 - 40))
                    if n3 == n1:
                        s += 0.7      # D-E-D-C#: walking, not pacing
                    s += rng.random() * 1.1
                    if s < bc:
                        best, bc = (n2, n3), s
            n2, n3 = best
            line = [n1, n2, n3, n4]
            for j, m in enumerate(line):
                out.append((at + j, m, 0.9, 1.0 if j == 0 else (0.86 if j == 2 else 0.8)))
            if rng.random() < 0.2:          # a dead-note "skip" into the next bar
                out.append((at + 3 + 2 / 3.0, n4, 0.15, -1))
            prev = n4
        elif k == 2:
            n2 = _approach(tgt, n1, c, rng)
            out.append((at, n1, 0.9, 1.0))
            out.append((at + 1, n2, 0.9, 0.8))
            prev = n2
        else:
            out.append((at, n1, 0.9, 0.9))
            prev = n1
    return out


# ------------------------------------------------------------------ guide-tone line

def guide_line(chart, rng, start=70, lo=65, hi=79):
    """A slow counter-line on the 3rds and 7ths (and colour tones), voice-led bar to bar, with
    the odd scalar pickup into a phrase. Returns [(abs_beat, midi, beats, vel)]."""
    out = []
    prev = start
    for bi, b in enumerate(chart):
        c = b[0]
        g = [m for pc in c.guide() for m in notes_in(pc, lo, hi)]
        target = min(g, key=lambda m: (abs(m - prev), rng.random()))
        r = rng.random()
        last_in_phrase = bi % 4 == 3
        if r < 0.22 and not last_in_phrase:
            prev = target
            continue                                   # breathe
        if len(b) == 2:
            c2 = b[1]
            g2 = [m for pc in c2.guide() for m in notes_in(pc, lo, hi)]
            t2 = min(g2, key=lambda m: abs(m - target))
            out.append((bi * 4, target, 1.8, 0.5))
            out.append((bi * 4 + 2, t2, 1.8, 0.46))
            prev = t2
        elif r < 0.62:
            out.append((bi * 4, target, 3.6, 0.5))
            prev = target
        else:
            col = [m for pc in c.upper() for m in notes_in(pc, lo, hi) if m != target]
            t2 = min(col, key=lambda m: (abs(m - target), rng.random()))
            out.append((bi * 4, target, 1.8, 0.52))
            out.append((bi * 4 + 2, t2, 1.8, 0.45))
            prev = t2
        if last_in_phrase and rng.random() < 0.55:
            nb = chart[(bi + 1) % len(chart)][0]
            ng = [m for pc in nb.guide() for m in notes_in(pc, lo, hi)]
            land = min(ng, key=lambda m: abs(m - prev))
            sc = sorted(m for m in range(lo - 2, hi + 3) if m % 12 in c.scale())
            # three scale notes walking into the next bar's guide tone
            if land >= prev:
                run = [m for m in sc if m < land][-3:]
            else:
                run = [m for m in sc if m > land][:3][::-1]
            if len(run) == 3:
                out = [e for e in out if not (e[0] >= bi * 4 + 2.5)]
                if out and out[-1][0] >= bi * 4 and out[-1][0] + out[-1][2] > bi * 4 + 2.5:
                    s, m, d, v = out[-1]
                    out[-1] = (s, m, bi * 4 + 2.4 - s, v)
                for j, m in enumerate(run):
                    out.append((bi * 4 + 2.5 + 0.5 * j, m, 0.45, 0.42 + 0.04 * j))
                prev = land
    return out
