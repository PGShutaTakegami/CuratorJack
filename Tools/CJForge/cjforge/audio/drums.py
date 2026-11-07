"""Synthesized drum voices. Every voice accepts `tune` (pitch multiplier), `decay` (length multiplier), `tone` (0..1 brightness)."""

from __future__ import annotations

import numpy as np

from ..errors import ForgeError
from .dsp import filter_signal, oscillator, seconds


def _exp(n: int, sr: int, decay: float) -> np.ndarray:
    return np.exp(-seconds(n, sr) / max(decay, 1e-4))


def _noise(n, rng):
    return rng.uniform(-1.0, 1.0, n)


def kick(sr, rng, tune, decay, tone):
    n = int(0.6 * decay * sr)
    t = seconds(n, sr)
    freq = 45.0 * tune + 110.0 * tune * np.exp(-t / 0.04)
    body = oscillator("sine", freq, n, sr, rng) * _exp(n, sr, 0.28 * decay)
    click = filter_signal(_noise(n, rng), sr, "highpass", 2500) * _exp(n, sr, 0.004) * (0.2 + 0.4 * tone)
    return body + click


def snare(sr, rng, tune, decay, tone):
    n = int(0.35 * decay * sr)
    body = oscillator("triangle", 190.0 * tune, n, sr, rng) * _exp(n, sr, 0.06 * decay) * 0.6
    noise = filter_signal(_noise(n, rng), sr, "bandpass", 2500 + 3000 * tone, 0.6) * _exp(n, sr, 0.12 * decay)
    return body + noise * 1.2


def hat(sr, rng, tune, decay, tone, length=0.05):
    n = int(max(length * 4, 0.1) * decay * sr)
    metal = sum(oscillator("square", f * tune, n, sr, rng) for f in (205.3, 304.4, 369.6, 522.7, 540.0, 800.0))
    sig = filter_signal(metal / 6.0 + _noise(n, rng) * 0.5, sr, "highpass", 6000 + 3000 * tone, 0.8, 24)
    return sig * _exp(n, sr, length * decay) * 1.6


def open_hat(sr, rng, tune, decay, tone):
    return hat(sr, rng, tune, decay, tone, 0.28)


def clap(sr, rng, tune, decay, tone):
    n = int(0.35 * decay * sr)
    env = np.zeros(n)
    for i, offset in enumerate((0.0, 0.011, 0.022)):
        start = int(offset * sr)
        env[start:] += _exp(n - start, sr, 0.008 if i < 2 else 0.12 * decay)
    return filter_signal(_noise(n, rng), sr, "bandpass", 1200 * tune + 800 * tone, 1.2) * env * 1.5


def rim(sr, rng, tune, decay, tone):
    n = int(0.08 * decay * sr)
    tone_sig = oscillator("sine", 1700.0 * tune, n, sr, rng) * 0.6
    return (tone_sig + filter_signal(_noise(n, rng), sr, "bandpass", 3000, 2) * 0.5) * _exp(n, sr, 0.012 * decay)


def tom(sr, rng, tune, decay, tone):
    n = int(0.5 * decay * sr)
    t = seconds(n, sr)
    freq = 95.0 * tune + 40.0 * tune * np.exp(-t / 0.08)
    return oscillator("sine", freq, n, sr, rng) * _exp(n, sr, 0.22 * decay)


def shaker(sr, rng, tune, decay, tone):
    n = int(0.15 * decay * sr)
    t = seconds(n, sr)
    env = np.minimum(t / 0.012, 1.0) * _exp(n, sr, 0.045 * decay)
    return filter_signal(_noise(n, rng), sr, "highpass", 5000 * tune + 2000 * tone, 0.9) * env


def brush(sr, rng, tune, decay, tone):
    n = int(0.35 * decay * sr)
    t = seconds(n, sr)
    env = np.minimum(t / 0.03, 1.0) * _exp(n, sr, 0.12 * decay)
    return filter_signal(_noise(n, rng), sr, "bandpass", 3500 * tune + 1500 * tone, 0.5) * env * 0.8


def cymbal(sr, rng, tune, decay, tone, length=1.6):
    n = int(length * 1.5 * decay * sr)
    metal = sum(oscillator("square", f * tune, n, sr, rng) for f in (263.0, 400.0, 421.0, 474.0, 587.0, 845.0))
    sig = filter_signal(metal / 6.0 * 0.6 + _noise(n, rng) * 0.6, sr, "highpass", 4000 + 3000 * tone, 0.7)
    return sig * _exp(n, sr, length * 0.4 * decay)


def ride(sr, rng, tune, decay, tone):
    bell = oscillator("sine", 2200.0 * tune, int(1.2 * decay * sr), sr, rng) * 0.15
    body = cymbal(sr, rng, tune, decay, tone * 0.5, 1.0) * 0.6
    n = max(len(bell), len(body))
    out = np.zeros(n)
    out[:len(body)] += body
    out[:len(bell)] += bell * _exp(len(bell), sr, 0.4 * decay)
    return out


DRUMS = {
    "kick": kick, "snare": snare, "hat": hat, "open_hat": open_hat, "clap": clap, "rim": rim,
    "tom": tom, "shaker": shaker, "brush": brush, "crash": cymbal, "ride": ride,
}


class DrumVoice:
    def __init__(self, name: str, tune: float, decay: float, tone: float, at: str):
        if name not in DRUMS:
            raise ForgeError(f"unknown drum '{name}' (use one of {list(DRUMS)})", at, "run `cjforge ref drums`")
        self.name, self.tune, self.decay, self.tone = name, tune, decay, tone
        self._cache: dict = {}

    def hit(self, velocity: float, sr: int, rng: np.random.Generator) -> np.ndarray:
        cached = self._cache.get(sr)
        if cached is None:
            cached = DRUMS[self.name](sr, rng, self.tune, self.decay, self.tone)
            peak = np.max(np.abs(cached)) + 1e-12
            cached = cached / peak
            self._cache[sr] = cached
        return cached * velocity
