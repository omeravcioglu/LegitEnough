"""Trailer versions of the game's sounds (same vocabulary as Scripts/Game/Audio/Sfx.cs, built
with more care) plus room ambience. Every function returns mono or stereo float arrays."""
import numpy as np

from . import dsp
from .dsp import SR, n_of, t_axis


def _rng(seed):
    return np.random.default_rng(seed)


# ------------------------------------------------------------------ UI and hands

def click(seed=0, vel=1.0):
    """A chunky button: plastic tick on the press, softer tock on the release."""
    r = _rng(seed)
    n = n_of(0.09)
    t = t_axis(n)
    press = dsp.bp(r.standard_normal(n), 2500.0, 7000.0, 2) * np.exp(-t / 0.0025)
    body = np.sin(2 * np.pi * (1150 + 120 * r.random()) * t) * np.exp(-t / 0.008) * 0.5
    thock = np.sin(2 * np.pi * 240 * t) * np.exp(-t / 0.012) * 0.35
    y = press + body + thock
    rel = n_of(0.055)
    y[rel:] += (dsp.bp(r.standard_normal(n - rel), 3000.0, 8000.0, 2) * np.exp(-t[:n - rel] / 0.0018) * 0.35)
    return y * vel * 0.8


def mouse(seed=0, up=False):
    r = _rng(seed + (7 if up else 0))
    n = n_of(0.03)
    t = t_axis(n)
    y = dsp.bp(r.standard_normal(n), 2000.0, 9000.0, 2) * np.exp(-t / 0.0016)
    y += np.sin(2 * np.pi * 1900 * t) * np.exp(-t / 0.003) * 0.4
    return y * (0.25 if up else 0.4)


def key(seed=0, space=False):
    """Beige 90s keyboard: a click, a hollow plastic body, a bottom-out."""
    r = _rng(seed)
    n = n_of(0.12)
    t = t_axis(n)
    f = 700.0 if space else 1300.0 + 500.0 * r.random()
    y = dsp.bp(r.standard_normal(n), 2500.0, 9000.0, 2) * np.exp(-t / 0.002) * 0.9
    y += np.sin(2 * np.pi * f * t) * np.exp(-t / 0.01) * 0.45
    y += dsp.resonator(r.standard_normal(n) * np.exp(-t / 0.004), 380.0 if not space else 230.0, 6.0) * 1.6
    ret = n_of(0.045 + 0.02 * r.random())
    y[ret:] += dsp.bp(r.standard_normal(n - ret), 3000.0, 9000.0, 2) * np.exp(-t[:n - ret] / 0.0015) * 0.3
    return y * (0.55 + 0.25 * r.random())


def pop(seed=0):
    n = n_of(0.12)
    t = t_axis(n)
    f = 620.0 * np.exp(-t / 0.05) + 380.0
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) * np.exp(-t / 0.035)
    return y * 0.45


def notify():
    """Late-90s OS chime: two square-ish tones, slightly soft."""
    y = np.zeros(n_of(0.6))
    for i, (f, start) in enumerate(((880.0, 0.0), (1320.0, 0.11))):
        n = n_of(0.35)
        t = t_axis(n)
        tone = np.tanh(3 * np.sin(2 * np.pi * f * t)) * np.exp(-t / 0.12) * np.clip(t / 0.003, 0, 1)
        s = n_of(start)
        y[s:s + n] += dsp.lp(tone, 5000.0, 2)[: y.size - s]
    return y * 0.25


def error():
    n = n_of(0.3)
    t = t_axis(n)
    y = np.tanh(4 * np.sin(2 * np.pi * 140 * t)) * np.clip(1 - t / 0.3, 0, 1) * np.clip(t / 0.005, 0, 1)
    return dsp.lp(y, 2500.0, 2) * 0.3


# ------------------------------------------------------------------ voices

_VOWELS = [  # (F1, F2, F3) Hz
    (730, 1090, 2440), (530, 1840, 2480), (270, 2290, 3010), (570, 840, 2410),
    (300, 870, 2240), (660, 1720, 2410), (440, 1020, 2240),
]


def voice_blip(pitch=1.0, seed=0, length=0.075):
    """One syllable of gibberish: a glottal pulse train through vowel formants."""
    r = _rng(seed)
    n = n_of(length + 0.03)
    t = t_axis(n)
    f0 = 165.0 * pitch * (1.0 + 0.08 * (r.random() - 0.5))
    contour = f0 * (1.0 + 0.06 * np.sin(np.pi * t / (length + 0.03)) * (1 if r.random() > 0.4 else -1))
    ph = 2 * np.pi * np.cumsum(contour) / SR
    src = np.zeros(n)
    for k in range(1, 40):
        if k * f0 > 6000:
            break
        src += np.sin(k * ph) / k ** 1.1
    src += r.standard_normal(n) * 0.05
    v = _VOWELS[r.integers(0, len(_VOWELS))]
    y = sum(dsp.resonator(src, f, 6.0 + i * 2) * g for i, (f, g) in enumerate(zip(v, (1.0, 0.6, 0.25))))
    e = np.sin(np.pi * np.clip(t / length, 0, 1)) ** 0.6 * (t < length)
    e = dsp.zlp(e, 200.0, 1)
    return y * e * 0.5


def voice(blips, pitch, seed=0):
    """A line of dialogue: n blips at the game's jittered spacing (Sfx.QueueVoice)."""
    r = _rng(seed)
    parts = []
    t = 0.0
    for i in range(int(blips)):
        parts.append((t, voice_blip(pitch * (0.85 + 0.35 * r.random()) ** 0.5, seed * 31 + i, 0.06 + 0.03 * r.random())))
        t += 0.07 + r.random() * 0.05
    y = np.zeros(n_of(t + 0.2))
    for s, b in parts:
        k = n_of(s)
        y[k:k + b.size] += b[: y.size - k]
    return y


# ------------------------------------------------------------------ shop

def bell(seed=0):
    """Brass shop bell on a spring: the clapper hits a few times as it swings."""
    r = _rng(seed)
    out = np.zeros(n_of(2.6))
    strikes = [(0.0, 1318.5, 1.0), (0.16, 1046.5, 0.8), (0.31, 1318.5, 0.5), (0.47, 1046.5, 0.32), (0.64, 1318.5, 0.18)]
    for start, f, a in strikes:
        n = n_of(2.0)
        t = t_axis(n)
        y = (np.sin(2 * np.pi * f * t) * np.exp(-t / 0.9)
             + 0.5 * np.sin(2 * np.pi * f * 2.76 * t) * np.exp(-t / 0.35)
             + 0.25 * np.sin(2 * np.pi * f * 5.40 * t) * np.exp(-t / 0.15)
             + 0.12 * np.sin(2 * np.pi * f * 8.93 * t) * np.exp(-t / 0.07))
        y += dsp.hp(r.standard_normal(n), 4000.0, 2) * np.exp(-t / 0.002) * 0.5
        s = n_of(start + 0.01 * r.random())
        out[s:s + n] += (y * a)[: out.size - s]
    return out * 0.28


def door(seed=0):
    """Door opening: latch, hinge creak, a draught of the street."""
    r = _rng(seed)
    n = n_of(1.1)
    t = t_axis(n)
    latch = dsp.bp(r.standard_normal(n), 1500.0, 6000.0, 2) * np.exp(-t / 0.006) * 0.6
    latch += np.sin(2 * np.pi * 900 * t) * np.exp(-t / 0.015) * 0.3
    creak_t = np.clip((t - 0.08) / 0.7, 0, 1)
    f = 190.0 + 140.0 * creak_t + 25.0 * np.sin(2 * np.pi * 7 * t)
    stick = (np.sin(2 * np.pi * np.cumsum(f) / SR) > 0.6).astype(float)
    creak = dsp.bp(stick, 300.0, 2500.0, 2) * np.sin(np.pi * creak_t) ** 2 * 0.25
    thud = np.sin(2 * np.pi * 85 * t) * np.exp(-t / 0.12) * 0.3
    return latch + creak + thud


def step(seed=0, scale=1.0):
    """A shoe on old linoleum."""
    r = _rng(seed)
    n = n_of(0.18)
    t = t_axis(n)
    heel = dsp.bp(r.standard_normal(n), 1200.0, 5000.0, 2) * np.exp(-t / 0.008)
    thud = dsp.lp(r.standard_normal(n), 300.0, 2) * np.exp(-t / 0.03) * 3.0
    scuff = dsp.bp(r.standard_normal(n), 2000.0, 7000.0, 2) * np.exp(-np.abs(t - 0.04) / 0.02) * 0.15
    return (heel * 0.6 + thud + scuff) * (0.35 + 0.35 * scale) * (0.8 + 0.4 * r.random())


def paper(seed=0, pitch=1.0, length=0.3):
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    w = r.standard_normal(n)
    flutter = 0.6 + 0.4 * np.abs(np.sin(2 * np.pi * (18 + 10 * r.random()) * t))
    y = dsp.bp(w, 900.0 * pitch, 7000.0, 2) * flutter
    e = np.sin(np.pi * np.clip(t / length, 0, 1)) ** 1.5
    return y * e * 0.35


def stamp(seed=0, big=False):
    """Binding press / rubber stamp: a hard thunk on wood with a paper slap."""
    r = _rng(seed)
    n = n_of(0.9 if big else 0.5)
    t = t_axis(n)
    w = r.standard_normal(n)
    body = sum(dsp.resonator(w * np.exp(-t / 0.004), f, q) * g
               for f, q, g in ((210.0, 9.0, 1.0), (395.0, 11.0, 0.6), (690.0, 14.0, 0.35), (1210.0, 16.0, 0.2)))
    thump = np.sin(2 * np.pi * (58 + 40 * np.exp(-t / 0.02)) * t) * np.exp(-t / (0.22 if big else 0.13)) * (1.4 if big else 0.8)
    slap = dsp.bp(w, 900.0, 5000.0, 2) * np.exp(-t / 0.012) * 0.6
    y = body * 3.0 + thump + slap
    return dsp.saturate(y, 1.3) * (0.9 if big else 0.6)


def shutter(seed=0):
    """Cheap compact camera: a click-clack of the leaf shutter and the advance motor tick."""
    r = _rng(seed)
    n = n_of(0.35)
    t = t_axis(n)
    y = np.zeros(n)
    for s, g, f in ((0.0, 1.0, 2400.0), (0.065, 0.75, 1700.0)):
        k = n_of(s)
        m = n - k
        tt = t[:m]
        y[k:] += (dsp.bp(r.standard_normal(m), f * 0.6, f * 2.5, 2) * np.exp(-tt / 0.004)
                  + np.sin(2 * np.pi * f * 0.45 * tt) * np.exp(-tt / 0.012) * 0.5) * g
    k = n_of(0.12)
    y[k:] += dsp.bp(r.standard_normal(n - k), 300.0, 2000.0, 2) * np.exp(-t[:n - k] / 0.05) * 0.18
    return y * 0.7


def flash(seed=0):
    """Flash pop, then the capacitor whine climbing as it recharges."""
    r = _rng(seed)
    n = n_of(2.2)
    t = t_axis(n)
    pop_ = dsp.bp(r.standard_normal(n), 1500.0, 12000.0, 2) * np.exp(-t / 0.01) * 0.5
    f = 3200.0 + 6800.0 * (1 - np.exp(-t / 0.8))
    whine = np.sin(2 * np.pi * np.cumsum(f) / SR) * 0.035 * np.clip((t - 0.15) / 0.3, 0, 1) * np.exp(-np.maximum(t - 1.4, 0) / 0.15)
    return pop_ + whine


def zoom_motor(seed=0, length=0.22):
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    f = 1900.0 + 300.0 * np.sin(2 * np.pi * 30 * t)
    y = np.tanh(2 * np.sin(2 * np.pi * np.cumsum(f) / SR)) * 0.08 + dsp.bp(r.standard_normal(n), 3000.0, 8000.0, 2) * 0.03
    return dsp.fade(y, 0.02, 0.04)


def roller(seed=0, length=1.2):
    """Backdrop roll pulled down: ratchet clicks and fabric."""
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    y = dsp.bp(r.standard_normal(n), 600.0, 4000.0, 2) * 0.12 * np.sin(np.pi * t / length)
    rate = 18.0
    for k in range(int(length * rate)):
        s = n_of(k / rate * (1 + 0.1 * r.random()))
        m = min(n_of(0.02), n - s)
        if m <= 0:
            break
        tt = t[:m]
        y[s:s + m] += np.sin(2 * np.pi * 2600 * tt) * np.exp(-tt / 0.003) * 0.25
    return y


def rustle(seed=0):
    """Cloth / hair rustle as the head is turned."""
    r = _rng(seed)
    n = n_of(0.22)
    t = t_axis(n)
    return dsp.bp(r.standard_normal(n), 1500.0, 6000.0, 2) * np.sin(np.pi * t / 0.22) ** 2 * 0.12


def glasses(seed=0):
    r = _rng(seed)
    n = n_of(0.25)
    t = t_axis(n)
    y = np.zeros(n)
    for s, f in ((0.0, 3100.0), (0.12, 2400.0)):
        k = n_of(s)
        m = n - k
        tt = t[:m]
        y[k:] += (np.sin(2 * np.pi * f * tt) * np.exp(-tt / 0.006) + dsp.hp(r.standard_normal(m), 3000.0, 2) * np.exp(-tt / 0.002) * 0.5)
    return y * 0.35


def printer_run(length, seed=0, tier=1):
    """Inkjet at work: the carriage zips across, the line-feed stepper ticks, the motor whines."""
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    w = r.standard_normal(n)
    sweep_hz = 1.1 if tier == 1 else 1.7
    pos = 0.5 - 0.5 * np.cos(2 * np.pi * sweep_hz * t)
    speed = np.abs(np.sin(2 * np.pi * sweep_hz * t))
    carriage = dsp.bp(w, 700.0, 3500.0, 2) * (0.25 + 0.75 * speed) * 0.3
    motor = np.sin(2 * np.pi * np.cumsum(420.0 + 260.0 * speed) / SR) * 0.06 * speed
    y = carriage + motor
    for k in range(int(length * sweep_hz * 2)):
        s = n_of((k + 0.5) / (sweep_hz * 2))
        m = min(n_of(0.05), n - s)
        if m <= 0:
            break
        tt = t[:m]
        y[s:s + m] += (np.sin(2 * np.pi * 160 * tt) * np.exp(-tt / 0.012) * 0.3
                       + dsp.bp(r.standard_normal(m), 1000.0, 4000.0, 2) * np.exp(-tt / 0.004) * 0.25)
    out = dsp.pan(y, 0.0)
    out[0] *= 0.7 + 0.3 * (1 - pos)
    out[1] *= 0.7 + 0.3 * pos
    return dsp.fade(out, 0.03, 0.06)


def printer_done(seed=0):
    r = _rng(seed)
    y = paper(seed, 1.3, 0.35) * 1.3
    out = np.zeros(n_of(0.8))
    out[:y.size] += y
    n = n_of(0.4)
    t = t_axis(n)
    k = n_of(0.3)
    out[k:k + n] += (np.sin(2 * np.pi * 1320 * t) * np.exp(-t / 0.09) * 0.18)[: out.size - k]
    return out


def jam(seed=0):
    """Paper crumples into the rollers, gears grind, the printer beeps at you."""
    r = _rng(seed)
    n = n_of(1.3)
    t = t_axis(n)
    w = r.standard_normal(n)
    crunch = dsp.bp(w, 400.0, 5000.0, 2) * (np.abs(np.sin(2 * np.pi * 23 * t)) ** 3) * np.exp(-t / 0.25) * 0.8
    grind = np.tanh(3 * np.sin(2 * np.pi * np.cumsum(110 - 35 * t) / SR)) * np.exp(-t / 0.3) * 0.25
    clunk = np.sin(2 * np.pi * 70 * t) * np.exp(-t / 0.08) * 0.6
    y = crunch + grind + clunk
    for s in (0.45, 0.62, 0.79):
        k = n_of(s)
        m = n_of(0.1)
        tt = t[:m]
        y[k:k + m] += np.sign(np.sin(2 * np.pi * 2093 * tt)) * 0.06 * (tt < 0.08)
    return y


def cash(seed=0):
    """Cash into the box: drawer slide, a bright ding, coins settling."""
    r = _rng(seed)
    out = np.zeros(n_of(1.6))
    n = out.size
    t = t_axis(n)
    out += dsp.bp(r.standard_normal(n), 500.0, 3000.0, 2) * np.exp(-t / 0.08) * 0.25
    for f, s, a in ((2093.0, 0.05, 0.5), (2637.0, 0.12, 0.45)):
        k = n_of(s)
        m = n - k
        tt = t[:m]
        out[k:] += (np.sin(2 * np.pi * f * tt) * np.exp(-tt / 0.45) + 0.3 * np.sin(2 * np.pi * f * 2.76 * tt) * np.exp(-tt / 0.12)) * a
    for i in range(14):
        s = 0.1 + 0.5 * r.random() ** 1.6
        f = 3500.0 + 5000.0 * r.random()
        k = n_of(s)
        m = min(n_of(0.08), n - k)
        tt = t[:m]
        out[k:k + m] += np.sin(2 * np.pi * f * tt) * np.exp(-tt / 0.012) * 0.08
    return out * 0.6


def crt_on(seed=0):
    """CRT monitor wakes: relay click, the degauss 'bwonng', the 15 kHz-ish whistle."""
    r = _rng(seed)
    n = n_of(1.2)
    t = t_axis(n)
    relay = dsp.bp(r.standard_normal(n), 1000.0, 6000.0, 2) * np.exp(-t / 0.004) * 0.5
    deg = np.sin(2 * np.pi * 60 * t) * np.exp(-t / 0.35) * np.clip((t - 0.02) / 0.03, 0, 1)
    deg = np.tanh(3 * deg) * 0.35 + dsp.bp(np.sign(np.sin(2 * np.pi * 60 * t)), 100.0, 800.0, 2) * np.exp(-t / 0.3) * 0.25
    whistle = np.sin(2 * np.pi * 12500 * t) * 0.006 * np.clip((t - 0.2) / 0.3, 0, 1)
    return relay + deg + whistle


def power_down(seed=0):
    """Everything dies at once: a relay clunk and the hum sliding down."""
    r = _rng(seed)
    n = n_of(1.6)
    t = t_axis(n)
    clunk = (dsp.lp(r.standard_normal(n), 400.0, 2) * np.exp(-t / 0.05) * 2.0 + np.sin(2 * np.pi * 55 * t) * np.exp(-t / 0.12))
    f = 100.0 * np.exp(-t / 0.35) + 5
    hum = (np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.4 * np.sin(4 * np.pi * np.cumsum(f) / SR)) * np.exp(-t / 0.4) * 0.35
    zap = dsp.bp(r.standard_normal(n), 2000.0, 9000.0, 2) * np.exp(-t / 0.03) * 0.3
    return clunk * 0.7 + hum + zap


def power_up(seed=0):
    """Fluorescent tubes struggle back: starter ticks, a flicker, the hum swells."""
    r = _rng(seed)
    n = n_of(1.8)
    t = t_axis(n)
    y = np.zeros(n)
    for s in (0.0, 0.17, 0.28, 0.52):
        k = n_of(s)
        m = n_of(0.06)
        tt = t[:m]
        y[k:k + m] += (dsp.bp(r.standard_normal(m), 2000.0, 8000.0, 2) * np.exp(-tt / 0.004) + np.sin(2 * np.pi * 3800 * tt) * np.exp(-tt / 0.01) * 0.3) * 0.6
    hum_env = np.clip((t - 0.5) / 0.6, 0, 1)
    y += (np.sin(2 * np.pi * 100 * t) + 0.5 * np.sin(2 * np.pi * 200 * t) + 0.25 * np.sin(2 * np.pi * 300 * t)) * 0.12 * hum_env
    y += dsp.lp(r.standard_normal(n), 300.0, 2) * np.exp(-np.abs(t - 0.5) / 0.04) * 1.2
    return y


# ------------------------------------------------------------------ ambience

def rain(length, seed=0, inside=True, intensity=1.0):
    """Rain on the street and the window: muffled wash, gusts, drops ticking on glass."""
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    wash = np.vstack([dsp.pink(n, r), dsp.pink(n, r)])
    cut = 2600.0 if inside else 7000.0
    wash = dsp.hp(dsp.lp(wash, cut, 2), 140.0, 2)
    gust = 0.75 + 0.25 * dsp.zlp(r.standard_normal(n), 0.25, 1) / 0.1
    gust = np.clip(gust, 0.45, 1.2)
    y = wash * gust * 0.12
    drops = np.zeros((2, n))
    count = int(length * (55 if inside else 90))
    for _ in range(count):
        s = r.integers(0, n)
        f = 1800.0 + 4000.0 * r.random()
        m = min(n_of(0.02), n - s)
        tt = t[:m]
        d = np.sin(2 * np.pi * f * tt) * np.exp(-tt / (0.002 + 0.006 * r.random())) * (0.01 + 0.03 * r.random() ** 2)
        p = r.random() * 2 - 1
        drops[:, s:s + m] += dsp.pan(d, p)
    drops = dsp.lp(drops, 6000.0 if inside else 12000.0, 2)
    return (y + drops) * intensity


def room_tone(length, seed=0, hum=0.02, neon=0.0):
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    flick = 1.0 + 0.04 * dsp.zlp(r.standard_normal(n), 3.0, 1) / 0.05
    h = (np.sin(2 * np.pi * 100 * t) + 0.45 * np.sin(2 * np.pi * 200 * t) + 0.2 * np.sin(2 * np.pi * 300 * t) + 0.1 * np.sin(2 * np.pi * 400 * t)) * hum * flick
    y = h
    if neon > 0:
        buzz = np.abs(np.sin(2 * np.pi * 60 * t)) ** 6
        y = y + dsp.hp(buzz, 300.0, 2) * neon
    air = dsp.lp(dsp.pink(n, r), 500.0, 2) * 0.01
    return dsp.pan(y + air, 0.0)


def traffic(length, seed=0):
    """A distant city: a low rumble with a car hissing past now and then."""
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    rumble = dsp.lp(dsp.brown(n, r), 120.0, 2) * 0.04
    out = dsp.pan(rumble, 0.0)
    k = 0.0
    while k < length:
        k += 4.0 + 6.0 * r.random()
        s = n_of(k)
        dur = 3.5
        m = min(n_of(dur), n - s)
        if m <= 0:
            break
        tt = t[:m]
        hiss = dsp.bp(r.standard_normal(m), 400.0, 2500.0, 2) * np.sin(np.pi * tt / dur) ** 3 * 0.05
        direction = 1 if r.random() > 0.5 else -1
        p = np.clip(direction * (tt / dur * 2 - 1), -1, 1)
        car = np.vstack([hiss * np.cos((p + 1) * np.pi / 4), hiss * np.sin((p + 1) * np.pi / 4)])
        out[:, s:s + m] += car
    return out


def siren(length, seed=0):
    """A far-off two-tone siren, mostly reflections."""
    n = n_of(length)
    t = t_axis(n)
    f = np.where((t % 1.4) < 0.7, 740.0, 587.0)
    f = dsp.zlp(f, 8.0, 1)
    y = np.sin(2 * np.pi * np.cumsum(f) / SR) + 0.3 * np.sin(4 * np.pi * np.cumsum(f) / SR)
    y = dsp.lp(y, 1600.0, 2) * np.sin(np.pi * np.clip(t / length, 0, 1)) ** 2 * 0.02
    return dsp.pan(y, -0.4)


def vinyl(length, seed=0, level=1.0):
    r = _rng(seed)
    n = n_of(length)
    hiss = dsp.hp(dsp.pink(n, r), 2500.0, 2) * 0.0035
    y = np.vstack([hiss, np.roll(hiss, 31)])
    count = int(length * 14)
    for _ in range(count):
        s = r.integers(0, n - 200)
        a = (0.004 + 0.03 * r.random() ** 4)
        click_ = dsp.hp(r.standard_normal(60), 1500.0, 2) * np.exp(-np.arange(60) / 8.0) * a
        y[:, s:s + 60] += dsp.pan(click_, r.random() * 1.2 - 0.6)
    return y * level


def wind(length, seed=0, level=1.0):
    """Winter outside the glass: a low, slow, breathy wind."""
    r = _rng(seed)
    n = n_of(length)
    w = np.vstack([dsp.pink(n, r), dsp.pink(n, r)])
    w = dsp.lp(dsp.hp(w, 90.0, 2), 900.0, 2)
    gust = np.clip(0.6 + 0.4 * dsp.zlp(r.standard_normal(n), 0.15, 1) / 0.06, 0.25, 1.3)
    return w * gust * 0.05 * level


def whack(seed=0):
    """A palm slapped on the printer's plastic lid: thump, rattle, a tired motor coming back."""
    r = _rng(seed)
    n = n_of(0.9)
    t = t_axis(n)
    w = r.standard_normal(n)
    slap = dsp.bp(w, 400.0, 3500.0, 2) * np.exp(-t / 0.012) * 1.2
    thump = np.sin(2 * np.pi * (75 + 60 * np.exp(-t / 0.015)) * t) * np.exp(-t / 0.09) * 1.0
    shell = sum(dsp.resonator(w * np.exp(-t / 0.003), f, 12.0) * g for f, g in ((340.0, 1.0), (760.0, 0.6), (1530.0, 0.3)))
    rattle = dsp.bp(w, 1500.0, 6000.0, 2) * (np.abs(np.sin(2 * np.pi * 31 * t)) ** 4) * np.exp(-t / 0.12) * 0.25
    return dsp.saturate(slap + thump + shell * 2.0 + rattle, 1.2) * 0.8


def typewriter(seed=0):
    """Caption key: a lighter, drier version of the keyboard."""
    return key(seed) * 0.6


def blip(seed=0, freq=1800.0):
    """A soft glassy blip: one letter of the studio wordmark appearing."""
    n = n_of(0.12)
    t = t_axis(n)
    y = (np.sin(2 * np.pi * freq * t) + 0.25 * np.sin(2 * np.pi * freq * 2.01 * t)) * np.exp(-t / 0.025)
    return y * np.clip(t / 0.0015, 0, 1) * 0.18


def shimmer(seed=0, length=0.45):
    """Pixels settling into place: a fine, rising sparkle."""
    r = _rng(seed)
    n = n_of(length)
    t = t_axis(n)
    y = np.zeros(n)
    for _ in range(26):
        s0 = r.integers(0, int(n * 0.7))
        f = 2500.0 + 5000.0 * (s0 / n) + 1500.0 * r.random()
        m = min(n_of(0.03), n - s0)
        tt = t[:m]
        y[s0:s0 + m] += np.sin(2 * np.pi * f * tt) * np.exp(-tt / 0.006) * (0.3 + 0.7 * r.random())
    return y * 0.06
