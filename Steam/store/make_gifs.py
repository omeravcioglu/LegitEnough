"""Turns the store-harness clips into the description GIFs (description/gif_<name>.gif).

The harness (harness/StoreShots.cs) saves every rendered frame at half size (960x540), so the
shop's 4x pixel art lands on an exact 2x. Room clips are cropped to the 616 px About column;
the photo session keeps the whole screen and Steam scales it into the column.

Clip frames are exactly 1/15 s of game time apart: store-shots.ps1 locks the copy's game clock to
Time.captureDeltaTime, and the "rec stop" lines in its log.txt confirm every step. So every
`step`-th frame shown for `ms` each plays at step * 1000 / 15 / ms times real speed: step 1 at
70 ms or step 2 at 130 ms is about real time (GIF delays are whole 10 ms). The cuts below are frame
numbers in one particular capture, so check them against the clips after recapturing.

Usage: python make_gifs.py <shots folder from store-shots.ps1>     (needs Pillow)
"""
import os
import sys

from PIL import Image

HERE = os.path.dirname(os.path.abspath(__file__))

# Steam rejects description GIFs longer than 12 s ("exceeding max duration"), so long clips keep
# fewer frames where little moves. Walking plays every frame in real time; the rest plays in real
# time at every other frame, or as a few held stills per line of dialogue. Picks skip the
# one-frame tube flickers (walkin 1, 145, 316). name: (crop box in the 960x540 frames or None,
# [(first, last, step, ms per frame)])
CLIPS = {
    # the sign flips to OPEN, Samir walks in, his four lines, then his offer with the reply cards
    "walkin": ((236, 60, 852, 360), [
        (4, 16, 6, 200), (16, 56, 20, 250),  # closed, open
        (56, 112, 1, 70),  # walks in
        (116, 174, 20, 400), (180, 210, 15, 450), (216, 277, 20, 350), (282, 321, 19, 370),  # lines
        (372, None, 1, 1500),  # reply cards
    ]),
    "photo": (None, [(1, 116, 2, 130)]),  # frame 0 is still the shop; the review screen is 77 on
    "handover": ((220, 60, 836, 360), [(2, 7, 4, 200), (7, 40, 1, 70), (40, None, 2, 130)]),
    "busy": ((0, 40, 568, 360), [(0, None, 1, 70)]),  # stops short of the reply cards
}
MAX_SECONDS = 11.5
MAX_FRAMES = 100  # the other Steam limit (STORE_PAGE.md); counted in the written file, after
                  # Pillow has merged identical neighbouring frames
HOLD_MS = 1500  # rest on the last frame before looping


def make(shots, name, box, segments):
    src = os.path.join(shots, "rec", name)
    files = sorted(f for f in os.listdir(src) if f.endswith(".png"))
    frames, durations = [], []
    for first, last, step, ms in segments:
        for f in files[first:last][::step]:
            im = Image.open(os.path.join(src, f)).convert("RGB")
            frames.append(im.crop(box) if box else im)
            durations.append(ms)
    # One shared palette (from a strip of sampled frames) keeps colours steady between frames.
    sample = frames[:: max(1, len(frames) // 12)]
    strip = Image.new("RGB", (frames[0].width, frames[0].height * len(sample)))
    for i, im in enumerate(sample):
        strip.paste(im, (0, i * im.height))
    pal = strip.quantize(colors=128, method=Image.MEDIANCUT, dither=Image.NONE)
    out = [im.quantize(palette=pal, dither=Image.NONE) for im in frames]
    durations[-1] = HOLD_MS
    assert sum(durations) <= MAX_SECONDS * 1000, f"{name}: {sum(durations) / 1000:.1f} s"
    path = os.path.join(HERE, "description", f"gif_{name}.gif")
    out[0].save(path, save_all=True, append_images=out[1:], duration=durations, loop=0, optimize=True, disposal=1)
    with Image.open(path) as gif:
        written = gif.n_frames
    assert written <= MAX_FRAMES, f"{name}: {written} frames"
    print(f"gif_{name}.gif {out[0].size} {written} frames {sum(durations) / 1000:.1f} s {os.path.getsize(path) // 1024} KB")


if __name__ == "__main__":
    shots = sys.argv[1]
    os.makedirs(os.path.join(HERE, "description"), exist_ok=True)
    for n, args in CLIPS.items():
        make(shots, n, *args)
