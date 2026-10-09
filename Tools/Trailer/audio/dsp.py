"""Small offline DSP toolkit for the trailer audio (48 kHz, float64, numpy/scipy only)."""
import numpy as np
from scipy import signal
from scipy.ndimage import maximum_filter1d, minimum_filter1d, uniform_filter1d

SR = 48000


def n_of(seconds):
    return max(1, int(round(seconds * SR)))


def t_axis(n):
    return np.arange(n) / SR


def db(x):
    return 20.0 * np.log10(np.maximum(np.abs(x), 1e-12))


def undb(d):
    return 10.0 ** (d / 20.0)


# ------------------------------------------------------------------ filters

def _sos(kind, f, order=2):
    f = np.atleast_1d(np.asarray(f, dtype=float))
    f = np.clip(f, 5.0, SR * 0.49)
    wn = f if f.size > 1 else f[0]
    return signal.butter(order, wn, btype=kind, fs=SR, output="sos")


def lp(x, f, order=2):
    return signal.sosfilt(_sos("lowpass", f, order), x, axis=-1)


def hp(x, f, order=2):
    return signal.sosfilt(_sos("highpass", f, order), x, axis=-1)


def bp(x, lo, hi, order=2):
    return signal.sosfilt(_sos("bandpass", [lo, hi], order), x, axis=-1)


def zlp(x, f, order=2):
    """Zero-phase low-pass (for envelopes and control signals)."""
    return signal.sosfiltfilt(_sos("lowpass", f, order), x, axis=-1)


def peak_eq(x, f0, gain_db, q=1.0):
    """RBJ peaking EQ."""
    a = 10 ** (gain_db / 40.0)
    w0 = 2 * np.pi * f0 / SR
    alpha = np.sin(w0) / (2 * q)
    b = [1 + alpha * a, -2 * np.cos(w0), 1 - alpha * a]
    aa = [1 + alpha / a, -2 * np.cos(w0), 1 - alpha / a]
    return signal.lfilter(np.array(b) / aa[0], np.array(aa) / aa[0], x, axis=-1)


def shelf(x, f0, gain_db, high=True, s=0.8):
    """RBJ shelving EQ."""
    a = 10 ** (gain_db / 40.0)
    w0 = 2 * np.pi * f0 / SR
    cw, sw = np.cos(w0), np.sin(w0)
    alpha = sw / 2 * np.sqrt((a + 1 / a) * (1 / s - 1) + 2)
    sa = 2 * np.sqrt(a) * alpha
    if high:
        b = [a * ((a + 1) + (a - 1) * cw + sa), -2 * a * ((a - 1) + (a + 1) * cw), a * ((a + 1) + (a - 1) * cw - sa)]
        aa = [(a + 1) - (a - 1) * cw + sa, 2 * ((a - 1) - (a + 1) * cw), (a + 1) - (a - 1) * cw - sa]
    else:
        b = [a * ((a + 1) - (a - 1) * cw + sa), 2 * a * ((a - 1) - (a + 1) * cw), a * ((a + 1) - (a - 1) * cw - sa)]
        aa = [(a + 1) + (a - 1) * cw + sa, -2 * ((a - 1) + (a + 1) * cw), (a + 1) + (a - 1) * cw - sa]
    return signal.lfilter(np.array(b) / aa[0], np.array(aa) / aa[0], x, axis=-1)


def resonator(x, f0, q):
    """Two-pole resonant band-pass (constant peak gain)."""
    w0 = 2 * np.pi * f0 / SR
    alpha = np.sin(w0) / (2 * q)
    b = [alpha, 0, -alpha]
    a = [1 + alpha, -2 * np.cos(w0), 1 - alpha]
    return signal.lfilter(np.array(b) / a[0], np.array(a) / a[0], x)


def pink(n, rng):
    """Pink-ish noise via the Paul Kellet filter."""
    w = rng.standard_normal(n)
    b = [0.049922035, -0.095993537, 0.050612699, -0.004408786]
    a = [1, -2.494956002, 2.017265875, -0.522189400]
    y = signal.lfilter(b, a, w)
    return y / (np.std(y) + 1e-12)


def brown(n, rng):
    y = np.cumsum(rng.standard_normal(n))
    y = hp(y, 20.0, 1)
    return y / (np.std(y) + 1e-12)


# ------------------------------------------------------------------ envelopes

def env_exp(n, tau):
    return np.exp(-t_axis(n) / max(tau, 1e-5))


def env_ar(n, attack, tau):
    t = t_axis(n)
    a = np.clip(t / max(attack, 1e-5), 0, 1)
    return a * np.exp(-np.maximum(t - attack, 0) / max(tau, 1e-5))


def release_at(y, start, tau):
    """Exponential release from sample `start` on (note-off damping)."""
    if start >= y.shape[-1]:
        return y
    n = y.shape[-1] - start
    e = np.ones(y.shape[-1])
    e[start:] = np.exp(-np.arange(n) / SR / max(tau, 1e-5))
    return y * e


def fade(y, fin=0.0, fout=0.0):
    y = y.copy()
    n = y.shape[-1]
    if fin > 0:
        k = min(n, n_of(fin))
        y[..., :k] *= np.sin(np.linspace(0, np.pi / 2, k)) ** 2
    if fout > 0:
        k = min(n, n_of(fout))
        y[..., n - k:] *= np.cos(np.linspace(0, np.pi / 2, k)) ** 2
    return y


# ------------------------------------------------------------------ placement

def pan(x, p):
    """Equal-power pan, p in [-1, 1]. Mono → stereo (2, n)."""
    ang = (np.clip(p, -1, 1) + 1) * np.pi / 4
    return np.vstack([x * np.cos(ang), x * np.sin(ang)])


def place(bus, x, start, gain=1.0):
    """Mix x (mono or stereo) into bus (stereo (2, N)) at sample `start`."""
    if x.ndim == 1:
        x = np.vstack([x, x]) * np.sqrt(0.5)
    n = x.shape[1]
    s = int(start)
    if s >= bus.shape[1] or s + n <= 0:
        return
    a0 = max(0, -s)
    b0 = max(0, s)
    k = min(n - a0, bus.shape[1] - b0)
    bus[:, b0:b0 + k] += x[:, a0:a0 + k] * gain


def stereo(n):
    return np.zeros((2, n))


# ------------------------------------------------------------------ space

def reverb_ir(rt60=1.8, length=None, predelay=0.02, damp=0.55, width=1.0, seed=1, early=6, bright=9000.0):
    """Stereo IR: sparse early taps + decorrelated noise tail whose highs die faster."""
    rng = np.random.default_rng(seed)
    if length is None:
        length = rt60 * 1.15
    n = n_of(length)
    t = t_axis(n)
    ir = np.zeros((2, n))
    # Frequency-dependent decay: split noise into bands, each with its own RT60.
    bands = [(20, 400, 1.15), (400, 1800, 1.0), (1800, 5000, 0.78 - 0.25 * damp), (5000, min(bright, 20000), 0.5 - 0.25 * damp)]
    for ch in range(2):
        w = rng.standard_normal(n)
        tail = np.zeros(n)
        for lo, hi, k in bands:
            band = bp(w, lo, hi, 2)
            rt = max(0.05, rt60 * k)
            tail += band * np.exp(-6.9078 * t / rt)
        onset = np.clip((t - predelay) / 0.03, 0, 1)
        tail *= onset
        ir[ch] = tail
    # Early reflections.
    for i in range(early):
        d = predelay * (0.35 + 0.65 * rng.random()) + 0.004 * i
        g = 0.55 * (0.8 ** i)
        k = n_of(d)
        if k < n:
            ir[0, k] += g * (rng.random() * 0.6 + 0.4)
            ir[1, min(n - 1, k + rng.integers(0, 40))] += g * (rng.random() * 0.6 + 0.4)
    mid = (ir[0] + ir[1]) / 2
    side = (ir[0] - ir[1]) / 2 * width
    ir = np.vstack([mid + side, mid - side])
    ir /= np.sqrt(np.sum(ir ** 2) / 2) + 1e-12
    return ir


def convolve(x, ir):
    """x stereo (2, n) or mono; ir stereo. Returns stereo of len(x) + len(ir) - 1."""
    if x.ndim == 1:
        x = np.vstack([x, x]) * np.sqrt(0.5)
    out = np.vstack([signal.oaconvolve(x[0], ir[0]), signal.oaconvolve(x[1], ir[1])])
    return out


def wet(x, ir, mix):
    """Convolve and crop back to len(x)."""
    y = convolve(x, ir)[:, :x.shape[-1]]
    return y * mix


# ------------------------------------------------------------------ dynamics

def _follow(level, attack, release, step):
    """Asymmetric one-pole follower on a decimated level signal."""
    a_att = np.exp(-step / (SR * max(attack, 1e-4)))
    a_rel = np.exp(-step / (SR * max(release, 1e-4)))
    out = np.empty_like(level)
    y = level[0] if level.size else 0.0
    for i, v in enumerate(level):
        a = a_att if v > y else a_rel
        y = a * y + (1 - a) * v
        out[i] = y
    return out


def compressor(x, thresh_db=-18.0, ratio=3.0, attack=0.01, release=0.15, knee=6.0, makeup_db=0.0, key=None, step=32):
    """Feed-forward compressor; `key` is an optional side-chain signal (mono or stereo)."""
    src = x if key is None else key
    mono = np.max(np.abs(src), axis=0) if src.ndim == 2 else np.abs(src)
    n = mono.size
    pad = (-n) % step
    blocks = np.pad(mono, (0, pad)).reshape(-1, step).max(axis=1)
    env = _follow(blocks, attack, release, step)
    lvl = db(env)
    over = lvl - thresh_db
    gr = np.where(over <= -knee / 2, 0.0,
                  np.where(over >= knee / 2, over * (1 - 1 / ratio),
                           (1 - 1 / ratio) * (over + knee / 2) ** 2 / (2 * knee)))
    g = undb(-gr + makeup_db)
    gs = np.interp(np.arange(n), np.arange(blocks.size) * step + step / 2, g)
    return x * gs


def duck(x, key, depth_db=-6.0, thresh_db=-30.0, attack=0.008, release=0.25):
    """Side-chain ducking: x drops by up to depth_db while key is above thresh."""
    mono = np.max(np.abs(key), axis=0) if key.ndim == 2 else np.abs(key)
    step = 64
    n = mono.size
    pad = (-n) % step
    blocks = np.pad(mono, (0, pad)).reshape(-1, step).max(axis=1)
    env = _follow(blocks, attack, release, step)
    amt = np.clip((db(env) - thresh_db) / 12.0, 0, 1)
    g = undb(depth_db * amt)
    gs = np.interp(np.arange(n), np.arange(blocks.size) * step + step / 2, g)
    return x * gs


def limiter(x, ceiling_db=-1.0, lookahead=0.004, release=0.08):
    """Look-ahead brickwall limiter on a stereo signal; returns (2, n)."""
    c = undb(ceiling_db)
    peak = np.max(np.abs(x), axis=0)
    la = max(1, n_of(lookahead))
    need = np.minimum(1.0, c / np.maximum(peak, 1e-9))
    g = minimum_filter1d(need, size=2 * la + 1, mode="nearest")
    # Release: let gain recover smoothly (decimated follower), never above the requirement.
    step = 16
    n = g.size
    pad = (-n) % step
    gb = np.pad(g, (0, pad), constant_values=1.0).reshape(-1, step).min(axis=1)
    sm = -_follow(-gb, 1e-4, release, step)
    gs = np.interp(np.arange(n), np.arange(gb.size) * step, sm)
    gs = np.minimum(gs, g)
    gs = uniform_filter1d(gs, size=la, mode="nearest")
    gs = np.minimum(gs, minimum_filter1d(need, size=la, mode="nearest") * 1.0 + 0.0)
    y = x * gs
    return np.clip(y, -c, c)


def saturate(x, drive=1.0):
    if drive <= 0:
        return x
    return np.tanh(x * drive) / np.tanh(drive)


# ------------------------------------------------------------------ loudness (ITU-R BS.1770-4)

def _k_weight(x):
    # Stage 1: high-shelf (+4 dB @ ~1.68 kHz); stage 2: RLB high-pass (~38 Hz). 48 kHz coefficients.
    b1 = [1.53512485958697, -2.69169618940638, 1.19839281085285]
    a1 = [1.0, -1.69065929318241, 0.73248077421585]
    b2 = [1.0, -2.0, 1.0]
    a2 = [1.0, -1.99004745483398, 0.99007225036621]
    return signal.lfilter(b2, a2, signal.lfilter(b1, a1, x, axis=-1), axis=-1)


def lufs(x):
    if x.ndim == 1:
        x = x[None, :]
    y = _k_weight(x)
    blk = n_of(0.4)
    hop = n_of(0.1)
    if y.shape[1] < blk:
        return -70.0
    starts = np.arange(0, y.shape[1] - blk + 1, hop)
    ms = np.array([np.sum(np.mean(y[:, s:s + blk] ** 2, axis=1)) for s in starts])
    l = -0.691 + 10 * np.log10(np.maximum(ms, 1e-12))
    keep = l > -70
    if not np.any(keep):
        return -70.0
    rel = -0.691 + 10 * np.log10(np.mean(ms[keep])) - 10
    keep &= l > rel
    return float(-0.691 + 10 * np.log10(np.mean(ms[keep])))


def true_peak_db(x):
    up = signal.resample_poly(x, 4, 1, axis=-1)
    return float(db(np.max(np.abs(up))))


# ------------------------------------------------------------------ time

def tape_stop(x, start, dur, power=1.6):
    """Slow the signal to a halt over `dur` seconds from sample `start`; silence after."""
    n = x.shape[-1]
    k = n_of(dur)
    if start >= n:
        return x
    y = x.copy()
    tt = np.arange(min(k, n - start)) / k
    rate = (1 - tt) ** power
    pos = start + np.cumsum(rate)
    idx = np.arange(n)
    seg = slice(start, start + tt.size)
    if x.ndim == 2:
        for ch in range(x.shape[0]):
            y[ch, seg] = np.interp(pos, idx, x[ch])
            y[ch, start + tt.size:] = 0
    else:
        y[seg] = np.interp(pos, idx, x)
        y[start + tt.size:] = 0
    return y


def normalize(x, peak=0.9):
    m = np.max(np.abs(x))
    return x if m < 1e-12 else x * (peak / m)
