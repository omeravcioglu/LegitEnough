"""Checks the rendered cues by numbers and pictures (nobody here can listen):
loudness (BS.1770), true peak, clipping, DC, mono fold-down, low end, loop seams (sample jump,
level step, loudness step, spectral flux), the Vorbis decode (length, offset, error), and
spectrogram / loudness PNGs into Tools/Music/out/.

Usage:  python Tools/Music/analyze.py            (all cues in Tools/Music/out + the game .ogg)
"""
import json
import os
import subprocess
import sys

import numpy as np
from PIL import Image, ImageDraw
from scipy import signal
from scipy.io import wavfile

HERE = os.path.dirname(os.path.abspath(__file__))
ROOT = os.path.abspath(os.path.join(HERE, "..", ".."))
sys.path.insert(0, os.path.join(ROOT, "Tools", "Trailer"))
from audio import dsp  # noqa: E402
from audio.dsp import SR, n_of  # noqa: E402

OUT = os.path.join(HERE, "out")
GAME = os.path.join(ROOT, "Assets", "LegitEnough", "Resources", "LegitEnough", "Music")
CUES = [("title_night", True), ("shop_day", True), ("shop_busy", True), ("demo_end", False)]


def load_wav(p):
    sr, x = wavfile.read(p)
    assert sr == SR, p
    return x.T.astype(np.float64)


def decode(p):
    raw = subprocess.run(["ffmpeg", "-hide_banner", "-loglevel", "error", "-i", p, "-f", "f32le", "-ac", "2", "-ar", str(SR), "-"],
                         check=True, capture_output=True).stdout
    return np.frombuffer(raw, dtype="<f4").reshape(-1, 2).T.astype(np.float64)


def momentary(x, win=0.4, hop=0.1, circular=False):
    y = dsp._k_weight(np.concatenate([x[:, -n_of(3):], x], axis=1) if circular else x)
    if circular:
        y = y[:, n_of(3):]
    if circular:
        y = np.concatenate([y, y[:, :n_of(win)]], axis=1)
    blk, hp = n_of(win), n_of(hop)
    starts = np.arange(0, y.shape[1] - blk + 1, hp)
    ms = np.array([np.sum(np.mean(y[:, s:s + blk] ** 2, axis=1)) for s in starts])
    return -0.691 + 10 * np.log10(np.maximum(ms, 1e-12)), starts


def seam_report(x):
    """Compare what happens across the loop point with what happens anywhere else."""
    L = x.shape[1]
    d = np.abs(np.diff(x, axis=1))
    jump = float(np.max(np.abs(x[:, 0] - x[:, -1])))
    typical = float(np.percentile(d, 50))
    p999 = float(np.percentile(d, 99.9))
    pct = float(np.mean(d.max(axis=0) < jump) * 100)
    # 20 ms RMS just before vs just after, against all neighbouring 20 ms windows.
    w = n_of(0.02)
    k = L // w
    r = np.sqrt(np.mean(x[:, :k * w].reshape(2, k, w) ** 2, axis=(0, 2))) + 1e-9
    steps = np.abs(20 * np.log10(r[1:] / r[:-1]))
    pre = np.sqrt(np.mean(x[:, -w:] ** 2)) + 1e-9
    post = np.sqrt(np.mean(x[:, :w] ** 2)) + 1e-9
    seam_step = abs(20 * np.log10(post / pre))
    # Momentary loudness, circularly: the window that straddles the seam vs its neighbours.
    m, starts = momentary(x, circular=True)
    dm = np.abs(np.diff(m))
    i = int(np.argmin(np.abs(starts - (L - n_of(0.2)))))
    seam_dm = float(max(dm[max(0, i - 3):i + 3]))
    # Spectral flux: the frame across the seam vs every frame.
    j = np.concatenate([x[:, -n_of(3):], x[:, :n_of(3)]], axis=1).mean(axis=0)
    full = np.concatenate([x.mean(axis=0), x.mean(axis=0)[:n_of(3)]])
    nfft, hop = 2048, 512

    def flux(sig):
        _, _, Z = signal.stft(sig, SR, nperseg=nfft, noverlap=nfft - hop, boundary=None, padded=False)
        a = np.log1p(np.abs(Z) * 1000)
        return np.sum(np.maximum(a[:, 1:] - a[:, :-1], 0), axis=0)

    fall = flux(full)
    fj = flux(j)
    c = (n_of(3) - nfft // 2) // hop
    seam_flux = float(max(fj[c - 2:c + 3]))
    return dict(
        sample_jump=jump, median_step=typical, p999_step=p999, jump_percentile=pct,
        rms20_step_db=float(seam_step), rms20_step_p50_db=float(np.percentile(steps, 50)), rms20_step_p99_db=float(np.percentile(steps, 99)),
        momentary_step_lu=seam_dm, momentary_step_p50_lu=float(np.percentile(dm, 50)), momentary_step_p95_lu=float(np.percentile(dm, 95)),
        flux_at_seam=seam_flux, flux_p50=float(np.percentile(fall, 50)), flux_p90=float(np.percentile(fall, 90)), flux_p99=float(np.percentile(fall, 99)),
    )


def basic(x):
    mono = x.mean(axis=0)
    st = dsp.lufs(x)
    mono_l = dsp.lufs(np.vstack([mono, mono]))
    L, R = x
    corr = float(np.sum(L * R) / np.sqrt(np.sum(L * L) * np.sum(R * R)))
    f, P = signal.welch(mono, SR, nperseg=16384)
    tot = np.sum(P)
    sub = np.sum(P[f < 60]) / tot
    low = np.sum(P[(f >= 60) & (f < 250)]) / tot
    high = np.sum(P[f > 8000]) / tot
    s_lufs, _ = momentary(x, win=3.0, hop=1.0)
    return dict(
        seconds=x.shape[1] / SR, samples=int(x.shape[1]),
        lufs_i=st, true_peak_dbtp=dsp.true_peak_db(x), sample_peak_dbfs=float(dsp.db(np.max(np.abs(x)))),
        clipped_samples=int(np.sum(np.abs(x) >= 0.999)), dc=float(np.max(np.abs(x.mean(axis=1)))),
        lra_short_term_p10_p95=[float(np.percentile(s_lufs[s_lufs > -60], 10)), float(np.percentile(s_lufs[s_lufs > -60], 95))],
        mono_fold_lufs_delta=float(mono_l - st), lr_correlation=corr,
        energy_below_60hz_pct=float(sub * 100), energy_60_250hz_pct=float(low * 100), energy_above_8k_pct=float(high * 100),
    )


# ---------------------------------------------------------------- pictures

def _cmap(v):
    """0..1 -> RGB, a dark-to-amber ramp."""
    stops = np.array([[0, 0, 0], [30, 10, 60], [110, 20, 90], [200, 60, 50], [245, 150, 40], [255, 240, 170]], float)
    x = np.clip(v, 0, 1) * (len(stops) - 1)
    i = np.minimum(x.astype(int), len(stops) - 2)
    f = (x - i)[..., None]
    return (stops[i] * (1 - f) + stops[i + 1] * f).astype(np.uint8)


def spectrogram_png(x, path, title, width=1600, height=420, fmin=30.0, fmax=16000.0, nfft=4096, hop=None, mark=None):
    mono = x.mean(axis=0)
    hop = hop or max(256, mono.size // width)
    f, t, Z = signal.stft(mono, SR, nperseg=nfft, noverlap=max(0, nfft - hop), boundary=None, padded=False)
    S = 20 * np.log10(np.abs(Z) * 2 / nfft * 4 + 1e-10)
    rows = np.geomspace(fmin, fmax, height)[::-1]
    idx = np.clip(np.searchsorted(f, rows), 0, f.size - 1)
    img = S[idx, :]
    cols = np.linspace(0, img.shape[1] - 1, width).astype(int)
    img = img[:, cols]
    v = (img + 110) / 90.0
    rgb = _cmap(v)
    pad_l, pad_b, pad_t = 60, 90, 24
    canvas = Image.new("RGB", (width + pad_l + 10, height + pad_b + pad_t), (18, 16, 20))
    canvas.paste(Image.fromarray(rgb), (pad_l, pad_t))
    d = ImageDraw.Draw(canvas)
    d.text((pad_l, 4), title, fill=(230, 220, 200))
    for fr in (50, 100, 200, 500, 1000, 2000, 5000, 10000):
        y = pad_t + int(np.argmin(np.abs(rows - fr)))
        d.line([(pad_l - 5, y), (pad_l, y)], fill=(200, 200, 200))
        d.text((4, y - 6), f"{fr if fr < 1000 else str(fr // 1000) + 'k'}", fill=(200, 200, 200))
    dur = mono.size / SR
    step = 10 if dur < 120 else 30
    if dur < 20:
        step = 1
    for s in np.arange(0, dur + 1e-6, step):
        xx = pad_l + int(s / dur * (width - 1))
        d.line([(xx, pad_t + height), (xx, pad_t + height + 5)], fill=(200, 200, 200))
        d.text((xx - 8, pad_t + height + 7), f"{s:.0f}", fill=(200, 200, 200))
    # Short-term loudness strip under it.
    m, starts = momentary(x, win=3.0 if dur > 20 else 0.4, hop=0.25 if dur > 20 else 0.05)
    y0 = pad_t + height + 24
    hh = pad_b - 30
    d.rectangle([pad_l, y0, pad_l + width, y0 + hh], outline=(80, 80, 80))
    lo, hi = -40.0, -10.0
    pts = []
    for mm, s in zip(m, starts):
        xx = pad_l + int(s / mono.size * (width - 1))
        yy = y0 + hh - int((np.clip(mm, lo, hi) - lo) / (hi - lo) * hh)
        pts.append((xx, yy))
    if len(pts) > 1:
        d.line(pts, fill=(240, 180, 60), width=1)
    for ref in (-20, -30):
        yy = y0 + hh - int((ref - lo) / (hi - lo) * hh)
        d.line([(pad_l, yy), (pad_l + width, yy)], fill=(70, 70, 90))
        d.text((pad_l + width - 60, yy - 11), f"{ref} LUFS", fill=(120, 120, 150))
    if mark is not None:
        xx = pad_l + int(mark / dur * (width - 1))
        d.line([(xx, pad_t), (xx, y0 + hh)], fill=(80, 220, 255))
    canvas.save(path)


def seam_png(x, path, title, side=4.0):
    j = np.concatenate([x[:, -n_of(side):], x[:, :n_of(side)]], axis=1)
    spectrogram_png(j, path, title + "  (loop end | loop start)", width=1200, nfft=2048, hop=128, mark=side)


def main():
    report = {}
    for name, loop in CUES:
        wav = os.path.join(OUT, name + ".wav")
        if not os.path.exists(wav):
            continue
        x = load_wav(wav)
        r = basic(x)
        if loop:
            r["seam"] = seam_report(x)
        ogg = os.path.join(GAME, name + ".ogg")
        if os.path.exists(ogg):
            y = decode(ogg)
            r["ogg_bytes"] = os.path.getsize(ogg)
            r["ogg_samples"] = int(y.shape[1])
            n = min(y.shape[1], x.shape[1])
            seg = slice(0, min(n, n_of(5)))
            cc = signal.correlate(y[0, seg], x[0, seg], mode="full", method="fft")
            r["ogg_offset_samples"] = int(np.argmax(cc) - (seg.stop - 1))
            err = y[:, :n] - x[:, :n]
            r["ogg_snr_db"] = float(10 * np.log10(np.sum(x[:, :n] ** 2) / max(np.sum(err ** 2), 1e-20)))
            r["ogg_true_peak_dbtp"] = dsp.true_peak_db(y)
            r["ogg_lufs_i"] = dsp.lufs(y)
            if loop:
                r["ogg_seam"] = seam_report(y)
        spectrogram_png(x, os.path.join(OUT, name + "_spectrogram.png"), f"{name}  {r['seconds']:.1f} s  {r['lufs_i']:.1f} LUFS  TP {r['true_peak_dbtp']:.1f} dBTP")
        if loop:
            seam_png(x, os.path.join(OUT, name + "_seam.png"), name)
        report[name] = r
    with open(os.path.join(OUT, "analysis.json"), "w", newline="") as fh:
        json.dump(report, fh, indent=1)
    for k, r in report.items():
        print(f"== {k}")
        for kk, vv in r.items():
            if isinstance(vv, dict):
                print("   " + kk + ": " + ", ".join(f"{a}={b:.4g}" for a, b in vv.items()))
            elif isinstance(vv, float):
                print(f"   {kk}: {vv:.4g}")
            else:
                print(f"   {kk}: {vv}")


if __name__ == "__main__":
    main()
