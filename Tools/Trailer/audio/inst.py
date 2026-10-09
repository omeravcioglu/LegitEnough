"""Synthesized instruments for the trailer score: upright bass, brushed jazz kit, Rhodes,
vibraphone, a dark pad and trailer effects. Every note function returns a mono float array
at dsp.SR that includes its natural tail."""
import numpy as np

from . import dsp
from .dsp import SR, n_of, t_axis

A4 = 440.0
NOTE_NAMES = {"C": 0, "C#": 1, "DB": 1, "D": 2, "D#": 3, "EB": 3, "E": 4, "F": 5, "F#": 6, "GB": 6,
              "G": 7, "G#": 8, "AB": 8, "A": 9, "A#": 10, "BB": 10, "B": 11}


def midi(name):
    """'D2', 'Bb3', 'C#5' → MIDI number."""
    name = name.strip().upper()
    i = 2 if len(name) > 2 and name[1] in "#B" and not name[1:].lstrip("-").isdigit() else 1
    return 12 * (int(name[i:]) + 1) + NOTE_NAMES[name[:i]]


def hz(m):
    return A4 * 2 ** ((m - 69) / 12.0)


# ================================================================== upright bass

def bass(freq, dur, vel=0.8, rng=None, dead=False):
    """Pizzicato upright bass: additive string modes with a pluck spectrum, fast-dying
    highs, a little pitch sag at the attack, finger thump and wooden body resonance."""
    rng = rng or np.random.default_rng(0)
    tail = 0.35
    n = n_of(dur + tail)
    t = t_axis(n)
    y = np.zeros(n)
    if not dead:
        p = 0.2 + 0.04 * rng.random()          # pluck position along the string
        fc = 650.0 + 1100.0 * vel              # brightness
        tau1 = 1.5 * (98.0 / freq) ** 0.25
        bend = 0.012 * vel
        warp = t + bend * 0.04 * (1 - np.exp(-t / 0.04))   # slight sharp attack settling down
        inh = 0.00025
        for k in range(1, 30):
            fk = k * freq * np.sqrt(1 + inh * k * k)
            if fk > SR * 0.45:
                break
            amp = abs(np.sin(k * np.pi * p)) / k ** 1.05 / (1 + (fk / fc) ** 2)
            tau = tau1 / (1 + 0.55 * (k - 1)) ** 1.15
            y += amp * np.sin(2 * np.pi * fk * warp + rng.random() * 0.3) * np.exp(-t / tau)
        # Fast "thump" of the fundamental and second harmonic as the string leaves the finger.
        y += 0.35 * vel * np.sin(2 * np.pi * freq * t) * np.exp(-t / 0.09)
        y *= np.clip(t / 0.0025, 0, 1)
    # Finger noise: soft lowpassed burst.
    fn = rng.standard_normal(n_of(0.05))
    fn = dsp.lp(fn, 900.0 if not dead else 600.0, 2) * dsp.env_exp(fn.size, 0.012) * (0.5 if not dead else 1.4) * vel
    y[:fn.size] += fn
    if dead:
        y += 0.4 * np.sin(2 * np.pi * freq * t) * np.exp(-t / 0.04) * vel
    # Body: wooden box modes.
    body = dsp.resonator(y, 105.0, 3.0) * 0.6 + dsp.resonator(y, 210.0, 4.0) * 0.35 + dsp.resonator(y, 400.0, 5.0) * 0.15
    y = y + body
    y = dsp.release_at(y, n_of(dur), 0.06)
    y = dsp.hp(y, 32.0, 2)
    # Gentle tube-ish warmth only: the raw sum peaks around 2-3, so bring it down first.
    return dsp.saturate(y * 0.3, 0.9) * vel * 1.6


# ================================================================== drums (brushes)

def kick(vel=0.8, rng=None):
    rng = rng or np.random.default_rng(1)
    n = n_of(0.6)
    t = t_axis(n)
    f = 55.0 + 45.0 * np.exp(-t / 0.032)   # settles on A1, the dominant
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.sin(ph) * np.exp(-t / 0.21)
    click = dsp.lp(rng.standard_normal(n_of(0.006)), 3200.0, 2) * 0.35
    y[:click.size] += click * dsp.env_exp(click.size, 0.0015)
    y += 0.25 * dsp.resonator(rng.standard_normal(n) * np.exp(-t / 0.05), 140.0, 2.0)
    return dsp.saturate(y, 1.4) * vel


def brush_tap(vel=0.8, rng=None):
    rng = rng or np.random.default_rng(2)
    n = n_of(0.45)
    t = t_axis(n)
    w = rng.standard_normal(n)
    hit = dsp.bp(w, 900.0, 9000.0, 2) * np.clip(t / 0.0015, 0, 1) * np.exp(-t / 0.075)
    wires = dsp.bp(rng.standard_normal(n), 2200.0, 6500.0, 2) * np.exp(-t / 0.16) * 0.55
    head = (np.sin(2 * np.pi * 186 * t) + 0.6 * np.sin(2 * np.pi * 331 * t)) * np.exp(-t / 0.045) * 0.22
    y = hit + wires + head
    return y * vel * 0.9


def brush_swish(dur, vel=0.5, rng=None):
    """A circular brush stroke: a soft band of noise that swells and fades."""
    rng = rng or np.random.default_rng(3)
    n = n_of(dur)
    w = dsp.bp(rng.standard_normal(n), 1800.0, 7500.0, 2)
    x = np.linspace(0, 1, n)
    e = np.sin(np.pi * x) ** 1.5 * (0.6 + 0.4 * np.sin(2 * np.pi * 2 * x + rng.random() * 6.28) ** 2)
    return w * e * vel * 0.35


def hat_foot(vel=0.7, rng=None):
    rng = rng or np.random.default_rng(4)
    n = n_of(0.12)
    t = t_axis(n)
    y = dsp.bp(rng.standard_normal(n), 4500.0, 11000.0, 2) * np.exp(-t / 0.018)
    # A touch of metal: inharmonic squares like a closed hat cluster.
    met = np.zeros(n)
    for f in (317.0, 431.0, 553.0, 709.0, 829.0, 1011.0):
        met += np.sign(np.sin(2 * np.pi * f * 7.1 * t))
    met = dsp.hp(met, 6000.0, 2) * np.exp(-t / 0.012) * 0.06
    return (y + met) * vel * 0.6


def snap(vel=0.8, rng=None):
    """Finger snap: a sharp skin-on-skin crack with a tiny body."""
    rng = rng or np.random.default_rng(5)
    n = n_of(0.16)
    t = t_axis(n)
    w = rng.standard_normal(n)
    crack = dsp.resonator(w, 2300.0, 2.5) * np.exp(-t / 0.014) * np.clip(t / 0.0004, 0, 1)
    air = dsp.bp(w, 3000.0, 9000.0, 2) * np.exp(-t / 0.006) * 0.6
    body = np.sin(2 * np.pi * 1050 * t) * np.exp(-t / 0.01) * 0.25
    return (crack * 1.6 + air + body) * vel


def rim(vel=0.8, rng=None):
    rng = rng or np.random.default_rng(6)
    n = n_of(0.2)
    t = t_axis(n)
    y = (np.sin(2 * np.pi * 455 * t) * 0.6 + np.sin(2 * np.pi * 1690 * t) * 0.5) * np.exp(-t / 0.022)
    y += dsp.hp(rng.standard_normal(n), 2000.0, 2) * np.exp(-t / 0.004) * 0.6
    return y * vel


def _metal(seconds, seed, lo=350.0, hi=12500.0, count=110, tau=2.4, bell=(), ping=0.0):
    rng = np.random.default_rng(seed)
    n = n_of(seconds)
    t = t_axis(n)
    y = np.zeros(n)
    u = rng.random(count)
    freqs = lo * (hi / lo) ** (u ** 0.85)
    for f in freqs:
        a = (0.4 + rng.random()) / np.sqrt(1 + f / 2500.0)
        d = tau * (1000.0 / f) ** 0.45 * (0.5 + rng.random())
        y += a * np.sin(2 * np.pi * f * t + rng.random() * 6.28) * np.exp(-t / d)
    for f, d, a in bell:
        y += a * np.sin(2 * np.pi * f * t) * np.exp(-t / d)
    stick = dsp.hp(rng.standard_normal(n_of(0.004)), 2500.0, 2) * ping
    y[:stick.size] += stick * dsp.env_exp(stick.size, 0.001)
    y *= np.clip(t / 0.0008, 0, 1)
    y = dsp.hp(y, 380.0, 2)
    return y / (np.max(np.abs(y)) + 1e-9)


_RIDES = {}


def ride(vel=0.8, variant=0):
    key = variant % 3
    if key not in _RIDES:
        _RIDES[key] = _metal(3.2, 100 + key, tau=1.9, bell=((612.0, 2.6, 2.2), (1187.0, 1.9, 1.4), (2496.0, 1.2, 1.0), (3730.0, 0.7, 0.6)), ping=6.0)
    y = _RIDES[key]
    if vel < 0.75:
        y = dsp.lp(y, 4000.0 + 9000.0 * vel, 1)
    return y * vel * 0.5


_CRASH = {}


def crash(vel=1.0):
    if "c" not in _CRASH:
        c = _metal(5.0, 777, lo=420.0, hi=15000.0, count=180, tau=3.2, ping=3.0)
        rng = np.random.default_rng(778)
        n = c.size
        wash = dsp.bp(rng.standard_normal(n), 3000.0, 14000.0, 2) * dsp.env_ar(n, 0.002, 1.6) * 0.35
        _CRASH["c"] = dsp.normalize(c + wash, 1.0)
    return _CRASH["c"] * vel * 0.55


# ================================================================== Rhodes

def rhodes(freq, dur, vel=0.7):
    """FM electric piano: 1:1 tine pair with a decaying index (the bark), a short
    inharmonic tine ping, a little even-harmonic pickup growl, damper on release."""
    tail = 0.45
    n = n_of(dur + tail)
    t = t_axis(n)
    tau_a = 2.2 * (220.0 / freq) ** 0.35
    amp = np.clip(t / 0.0015, 0, 1) * (0.75 * np.exp(-t / tau_a) + 0.25 * np.exp(-t / 0.35))
    index = (0.6 + 2.2 * vel ** 1.5) * np.exp(-t / 0.22) + 0.35
    car = 2 * np.pi * freq * t
    y = np.sin(car + index * np.sin(car))
    y += 0.18 * vel * np.sin(2 * car) * np.exp(-t / 0.5)
    ping_f = freq * 7.21
    if ping_f < SR * 0.45:
        y += 0.22 * vel * np.sin(2 * np.pi * ping_f * t) * np.exp(-t / 0.011)
    y *= amp
    y = dsp.release_at(y, n_of(dur), 0.09)
    # A little bark when played hard, clean when soft.
    y = dsp.saturate(y * 0.7, 0.35 + 0.9 * vel * vel)
    return y * (0.35 + 0.65 * vel)


# ================================================================== vibraphone

def vibes(freq, dur, vel=0.7, rng=None):
    """Aluminium bar modes (1 : 4 : 10) with a soft mallet; the motor tremolo is applied on
    the bus so every bar pulses together."""
    rng = rng or np.random.default_rng(9)
    tail = 1.8
    n = n_of(dur + tail)
    t = t_axis(n)
    tau1 = 3.2 * (440.0 / freq) ** 0.45
    y = np.sin(2 * np.pi * freq * t) * np.exp(-t / tau1)
    y += (0.18 + 0.25 * vel) * np.sin(2 * np.pi * freq * 3.99 * t) * np.exp(-t / (tau1 / 4.5))
    if freq * 9.85 < SR * 0.45:
        y += (0.03 + 0.08 * vel) * np.sin(2 * np.pi * freq * 9.85 * t) * np.exp(-t / (tau1 / 12))
    mallet = dsp.lp(rng.standard_normal(n_of(0.01)), 2500.0 + 3000.0 * vel, 2) * 0.12 * vel
    y[:mallet.size] += mallet * dsp.env_exp(mallet.size, 0.002)
    y *= np.clip(t / 0.001, 0, 1)
    y = dsp.release_at(y, n_of(dur), 0.22)
    return y * vel


# ================================================================== pad, glock, effects

def _saw(freq, n, phase=0.0):
    t = t_axis(n)
    y = np.zeros(n)
    k = 1
    while k * freq < min(9000.0, SR * 0.45):
        y += np.sin(2 * np.pi * k * freq * t + phase * k) / k
        k += 1
    return y


def pad(freqs, dur, vel=0.5, attack=1.2, release=1.5, cutoff=1300.0, seed=11):
    """Dark ensemble pad: detuned band-limited saws, low-passed, slow swell."""
    rng = np.random.default_rng(seed)
    n = n_of(dur + release)
    out = np.zeros((2, n))
    for f in freqs:
        for d in (-0.07, -0.03, 0.0, 0.035, 0.08):
            ff = f * 2 ** (d / 12)
            s = _saw(ff, n, rng.random() * 6.28)
            out += dsp.pan(s, d * 9)
    t = t_axis(n)
    e = np.clip(t / attack, 0, 1) ** 2
    e *= np.where(t > dur, np.exp(-(t - dur) / (release / 3)), 1.0)
    out = dsp.lp(out, cutoff, 2) * e
    return out / (len(freqs) * 5) * vel


def riser(dur, rng=None):
    rng = rng or np.random.default_rng(12)
    n = n_of(dur)
    t = t_axis(n)
    x = t / dur
    w = rng.standard_normal(n)
    # Sweep a band upward in blocks.
    y = np.zeros(n)
    blocks = 48
    edges = np.linspace(0, n, blocks + 1).astype(int)
    for i in range(blocks):
        a, b = edges[i], edges[i + 1]
        c = 300.0 * (8000.0 / 300.0) ** ((i + 0.5) / blocks)
        seg = dsp.bp(w[max(0, a - 2000):b], c * 0.7, min(c * 1.5, 20000.0), 2)[-(b - a):]
        y[a:b] = seg
    f = 110.0 * 2 ** (3 * x ** 1.4)
    tone = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.25
    return (y * 0.8 + tone) * x ** 2.2


def boom(rng=None):
    rng = rng or np.random.default_rng(13)
    n = n_of(3.0)
    t = t_axis(n)
    f = 36.0 + 44.0 * np.exp(-t / 0.09)
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 1.1)
    y += dsp.lp(rng.standard_normal(n), 220.0, 2) * np.exp(-t / 0.35) * 1.4
    y += dsp.bp(rng.standard_normal(n), 900.0, 4000.0, 2) * np.exp(-t / 0.03) * 0.6
    return dsp.saturate(y, 1.5)


def reverse_cymbal(dur):
    c = crash(1.0)
    k = min(c.size, n_of(dur))
    y = c[:k][::-1].copy()
    return y * np.linspace(0, 1, k) ** 1.5


def bass_slide(f0, f1, dur, vel=0.9):
    """A comic upright-bass glissando ('bwomp'): plucked, then the finger slides down."""
    n = n_of(dur + 0.4)
    t = t_axis(n)
    x = np.clip(t / dur, 0, 1)
    f = f0 * (f1 / f0) ** (x ** 0.7)
    ph = 2 * np.pi * np.cumsum(f) / SR
    y = np.zeros(n)
    for k in range(1, 14):
        y += np.sin(k * ph) / k ** 1.3 * np.exp(-t / (1.2 / k ** 0.8))
    y *= np.clip(t / 0.003, 0, 1) * np.exp(-np.maximum(t - dur, 0) / 0.08)
    body = dsp.resonator(y, 105.0, 3.0) * 0.6
    return dsp.saturate((y + body) * 0.8, 1.3) * vel
