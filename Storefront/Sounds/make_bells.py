"""Builds drake_door_jingle.wav and drake_desk_ding.wav from one real bell strike (source/bell_strike.wav: "bell",
freesound_community on Pixabay, free to use in projects). Layers pitch-shifted copies of the strike like the clapper
bouncing around a cluster of small bells. Pure Python, no dependencies.   python make_bells.py [output folder]
"""
import math
import random
import struct
import sys
import wave
import os

HERE = os.path.dirname(os.path.abspath(__file__))
RATE = 44100


def load(path):
    w = wave.open(path)
    n = w.getnframes()
    d = struct.unpack("<%dh" % n, w.readframes(n))
    x = [v / 32768 for v in d]
    start = next(i for i, v in enumerate(x) if abs(v) > 0.02)   # drop the lead-in silence
    return x[max(0, start - 40):]


def resample(x, rate, length):
    """Play x at `rate` (1.0 = original, 1.25 = a bit over a major third up and shorter), at most `length` seconds."""
    n = int(min(len(x) / rate, length * RATE))
    out = []
    for i in range(n):
        p = i * rate
        j = int(p)
        if j + 1 >= len(x):
            break
        f = p - j
        out.append(x[j] * (1 - f) + x[j + 1] * f)
    fade = int(RATE * 0.25)
    for i in range(min(fade, len(out))):
        out[len(out) - 1 - i] *= i / fade
    return out


def mix(dst, src, at, gain):
    s = int(at * RATE)
    for i, v in enumerate(src):
        if s + i >= len(dst):
            break
        dst[s + i] += v * gain


def finish(x, peak=0.85):
    m = max(abs(v) for v in x) or 1.0
    return [v / m * peak for v in x]


def save(path, x):
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(RATE)
        w.writeframes(b"".join(struct.pack("<h", int(max(-1, min(1, v)) * 32767)) for v in x))


def door_jingle(strike, rng):
    """Old shop bell on a spring: a quick shaken cluster of hits on three small bells, softening as it settles."""
    out = [0.0] * int(RATE * 3.6)
    bells = {r: resample(strike, r, 2.4) for r in (1.0, 1.12, 1.26, 1.5)}
    t, gap, gain = 0.0, 0.07, 1.0
    for k in range(11):
        rate = rng.choice((1.0, 1.12, 1.26, 1.26, 1.5))
        mix(out, bells[rate], t, gain * rng.uniform(0.75, 1.0))
        t += gap * rng.uniform(0.6, 1.4)
        gap *= 1.09
        gain *= 0.86
    return finish(out)


def desk_ding(strike, rng):
    out = [0.0] * int(RATE * 2.8)
    hit = resample(strike, 1.25, 2.4)
    mix(out, hit, 0.0, 1.0)
    mix(out, hit, 0.085, 0.14)          # the hammer rebounding
    return finish(out)


if __name__ == "__main__":
    folder = sys.argv[1] if len(sys.argv) > 1 else HERE
    strike = load(os.path.join(HERE, "source", "bell_strike.wav"))
    save(os.path.join(folder, "drake_door_jingle.wav"), door_jingle(strike, random.Random(5)))
    save(os.path.join(folder, "drake_desk_ding.wav"), desk_ding(strike, random.Random(5)))
    print("written")
