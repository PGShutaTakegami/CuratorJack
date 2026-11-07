"""Final stage: loop wrap, trimming, fades, loudness / peak normalization, soft limiting."""

from __future__ import annotations

import numpy as np

from ..spec import Spec
from .dsp import db_to_gain


class Master:
    def __init__(self, spec: Spec | None, default_channels: int):
        spec = spec or Spec({}, "$.master", "master")
        self.peak_db = spec.num("peak_db", -1.0, -40, 0)
        self.loudness_db = spec.num("loudness_db", None, -40, -3)
        self.channels = spec.int("channels", default_channels, 1, 2)
        self.fade_in = spec.num("fade_in", 0.0, 0, 30)
        self.fade_out = spec.num("fade_out", 0.0, 0, 30)
        self.trim = spec.bool("trim", True)
        spec.finish()

    def finish(self, x: np.ndarray, sr: int, loop: bool) -> np.ndarray:
        if self.trim and not loop:
            x = trim_tail(x)
        n = len(x)
        if self.fade_in > 0:
            k = min(n, int(self.fade_in * sr))
            x[:k] *= np.linspace(0.0, 1.0, k)[:, None]
        if self.fade_out > 0 and not loop:
            k = min(n, int(self.fade_out * sr))
            x[n - k:] *= np.linspace(1.0, 0.0, k)[:, None]

        ceiling = db_to_gain(self.peak_db)
        if self.loudness_db is not None:
            rms = np.sqrt(np.mean(x * x)) + 1e-12
            x = soft_limit(x * db_to_gain(self.loudness_db) / rms, ceiling)
        else:
            x = x * ceiling / (np.max(np.abs(x)) + 1e-12)
        if self.channels == 1:
            x = x.mean(axis=1, keepdims=True)
            x = x * ceiling / (np.max(np.abs(x)) + 1e-12) if self.loudness_db is None else soft_limit(x, ceiling)
        return x


def soft_limit(x: np.ndarray, ceiling: float) -> np.ndarray:
    knee = ceiling * 0.8
    mag = np.abs(x)
    over = mag > knee
    out = x.copy()
    out[over] = np.sign(x[over]) * (knee + (ceiling - knee) * np.tanh((mag[over] - knee) / (ceiling - knee)))
    return out


def trim_tail(x: np.ndarray, threshold_db: float = -70.0) -> np.ndarray:
    level = np.max(np.abs(x), axis=1)
    peak = np.max(level) + 1e-12
    above = np.nonzero(level > peak * db_to_gain(threshold_db))[0]
    if len(above) == 0:
        return x
    end = min(len(x), above[-1] + 1 + 256)
    x = x[:end].copy()
    fade = min(256, len(x))
    x[-fade:] *= np.linspace(1.0, 0.0, fade)[:, None]
    return x


def wrap_loop(x: np.ndarray, loop_samples: int) -> np.ndarray:
    """Fold everything after the loop end (release / reverb tails) back onto the start so the loop is seamless."""
    out = x[:loop_samples].copy()
    tail = x[loop_samples:]
    while len(tail) > 0:
        k = min(len(tail), loop_samples)
        out[:k] += tail[:k]
        tail = tail[k:]
    return out
