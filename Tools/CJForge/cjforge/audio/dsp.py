"""Core signal building blocks. Signals are float64 numpy arrays (mono: (n,), stereo: (n, 2))."""

from __future__ import annotations

import numpy as np
from numpy.lib.stride_tricks import sliding_window_view

from ..errors import ForgeError
from ..spec import Spec, check_num

SAMPLE_RATE = 44100
OSCILLATORS = ("sine", "triangle", "saw", "square", "pulse", "noise", "pink")
FILTER_TYPES = ("lowpass", "highpass", "bandpass")


def seconds(n: int, sr: int) -> np.ndarray:
    return np.arange(n) / sr


# ---------- oscillators ----------

def _polyblep(t: np.ndarray, dt: np.ndarray) -> np.ndarray:
    out = np.zeros_like(t)
    lo = t < dt
    x = t[lo] / dt[lo]
    out[lo] = x + x - x * x - 1.0
    hi = t > 1.0 - dt
    x = (t[hi] - 1.0) / dt[hi]
    out[hi] = x * x + x + x + 1.0
    return out


def oscillator(kind: str, freq, n: int, sr: int, rng: np.random.Generator,
               phase: float = 0.0, duty: float = 0.5, pm: np.ndarray | None = None) -> np.ndarray:
    """Band-limited (PolyBLEP) oscillator. `freq` is Hz (scalar or per-sample), `pm` is phase modulation in cycles."""
    if kind == "noise":
        return rng.uniform(-1.0, 1.0, n)
    if kind == "pink":
        return pink_noise(n, rng)

    freq = np.broadcast_to(np.asarray(freq, dtype=np.float64), (n,))
    dt = np.clip(freq / sr, 1e-9, 0.5)
    cycles = phase + np.cumsum(dt) - dt[0]
    if pm is not None:
        cycles = cycles + pm
    t = cycles % 1.0

    if kind == "sine":
        return np.sin(2.0 * np.pi * t)
    if kind == "triangle":
        return 1.0 - 4.0 * np.abs(((t + 0.25) % 1.0) - 0.5)
    if kind == "saw":
        return 2.0 * t - 1.0 - _polyblep(t, dt)
    if kind in ("square", "pulse"):
        width = 0.5 if kind == "square" else duty
        wave = np.where(t < width, 1.0, -1.0)
        wave += _polyblep(t, dt)
        wave -= _polyblep((t + 1.0 - width) % 1.0, dt)
        return wave - (2.0 * width - 1.0)
    raise ForgeError(f"unknown oscillator '{kind}'")


def pink_noise(n: int, rng: np.random.Generator) -> np.ndarray:
    white = rng.standard_normal(n)
    spectrum = np.fft.rfft(white)
    f = np.arange(len(spectrum), dtype=np.float64)
    f[0] = 1.0
    out = np.fft.irfft(spectrum / np.sqrt(f), n)
    return out / (np.max(np.abs(out)) + 1e-12)


# ---------- envelopes ----------

class Envelope:
    """ADSR. Times in seconds; decay/release are exponential (reach ~1% at the given time)."""

    def __init__(self, a=0.005, d=0.1, s=1.0, r=0.1):
        self.a, self.d, self.s, self.r = a, d, s, r

    @staticmethod
    def parse(spec: Spec | None, default: "Envelope | None" = None) -> "Envelope":
        base = default or Envelope()
        if spec is None:
            return base
        env = Envelope(
            spec.num("a", base.a, 0, 30),
            spec.num("d", base.d, 0, 30),
            spec.num("s", base.s, 0, 1),
            spec.num("r", base.r, 0, 30),
        )
        spec.finish()
        return env

    def render(self, gate: int, sr: int) -> np.ndarray:
        """Envelope of length gate + release."""
        a = max(int(self.a * sr), 1)
        d = max(int(self.d * sr), 1)
        r = max(int(self.r * sr), 1)
        idx = np.arange(gate, dtype=np.float64)
        env = np.where(idx < a, idx / a, self.s + (1.0 - self.s) * np.exp(-4.6 * (idx - a) / d))
        level = env[-1] if gate > 0 else 0.0
        release = level * np.exp(-4.6 * np.arange(r) / r)
        out = np.concatenate([env, release])
        fade = min(64, len(out))
        out[-fade:] *= np.linspace(1.0, 0.0, fade)
        return out

    @property
    def release(self) -> float:
        return self.r


# ---------- curves (values changing over time) ----------

def parse_curve(value, at: str, lo=None, hi=None):
    """number | [from, to] | {"points": [[t, v], ...], "curve": "exp"|"lin"} -> Curve"""
    if isinstance(value, (int, float)) and not isinstance(value, bool):
        return Curve([(0.0, check_num(value, at, lo, hi))], "lin", glide=False)
    if isinstance(value, list):
        if len(value) != 2:
            raise ForgeError("[from, to] expected", at)
        a = check_num(value[0], f"{at}[0]", lo, hi)
        b = check_num(value[1], f"{at}[1]", lo, hi)
        return Curve([(0.0, a), (1.0, b)], "exp" if a > 0 and b > 0 else "lin", glide=True)
    if isinstance(value, dict):
        spec = Spec(value, at, "curve")
        points = []
        for i, p in enumerate(spec.list("points")):
            if not isinstance(p, list) or len(p) != 2:
                raise ForgeError("[time, value] expected", f"{at}.points[{i}]")
            points.append((check_num(p[0], f"{at}.points[{i}][0]", 0), check_num(p[1], f"{at}.points[{i}][1]", lo, hi)))
        if not points:
            raise ForgeError("at least one point is required", f"{at}.points")
        mode = spec.str("curve", "exp", ("exp", "lin"))
        spec.finish()
        if mode == "exp" and any(v <= 0 for _, v in points):
            mode = "lin"
        return Curve(sorted(points), mode, glide=False)
    raise ForgeError("number, [from, to] or {points} expected", at)


class Curve:
    """Points are (time, value). A glide curve ([from, to]) stretches over the whole duration."""

    def __init__(self, points, mode: str, glide: bool):
        self.points = points
        self.mode = mode
        self.glide = glide

    @property
    def is_constant(self) -> bool:
        return len(self.points) == 1

    def start(self) -> float:
        return self.points[0][1]

    def render(self, n: int, sr: int, duration: float) -> np.ndarray:
        if self.is_constant:
            return np.full(n, self.points[0][1])
        times = np.array([p[0] * (duration if self.glide else 1.0) for p in self.points])
        values = np.array([p[1] for p in self.points])
        t = seconds(n, sr)
        if self.mode == "exp":
            return np.exp(np.interp(t, times, np.log(values)))
        return np.interp(t, times, values)


# ---------- filters (zero-phase, FFT based) ----------

def _response(ftype: str, ratio: np.ndarray, q: float, slope: int) -> np.ndarray:
    r2 = ratio * ratio
    denom = np.sqrt((1.0 - r2) ** 2 + (ratio / q) ** 2) + 1e-12
    if ftype == "lowpass":
        h = 1.0 / denom
    elif ftype == "highpass":
        h = r2 / denom
    else:
        h = (ratio / q) / denom
    return h * h if slope == 24 else h


def filter_signal(x: np.ndarray, sr: int, ftype: str, cutoff, q: float = 0.707, slope: int = 12) -> np.ndarray:
    """Filter mono or stereo `x`. `cutoff` is Hz, scalar or per-sample array (time-varying -> STFT)."""
    if x.ndim == 2:
        return np.stack([filter_signal(x[:, c], sr, ftype, cutoff, q, slope) for c in range(x.shape[1])], axis=1)
    n = len(x)
    if n == 0:
        return x
    nyquist = sr * 0.5 - 1.0
    if np.isscalar(cutoff) or np.ndim(cutoff) == 0:
        size = 1 << int(np.ceil(np.log2(n + 4096)))
        spectrum = np.fft.rfft(x, size)
        freqs = np.fft.rfftfreq(size, 1.0 / sr)
        h = _response(ftype, freqs / min(float(cutoff), nyquist), q, slope)
        return np.fft.irfft(spectrum * h, size)[:n]

    cutoff = np.clip(np.asarray(cutoff, dtype=np.float64), 10.0, nyquist)
    frame, hop = 1024, 256
    padded = np.concatenate([np.zeros(frame), x, np.zeros(frame + hop)])
    window = np.sqrt(np.hanning(frame + 1)[:-1])
    frames = sliding_window_view(padded, frame)[::hop] * window
    centers = np.clip(np.arange(len(frames)) * hop + frame // 2 - frame, 0, n - 1)
    freqs = np.fft.rfftfreq(frame, 1.0 / sr)
    h = _response(ftype, freqs[None, :] / cutoff[centers][:, None], q, slope)
    out_frames = np.fft.irfft(np.fft.rfft(frames, axis=1) * h, frame, axis=1) * window
    out = np.zeros(len(padded))
    for i, f in enumerate(out_frames):
        out[i * hop:i * hop + frame] += f
    return out[frame:frame + n] / 2.0


class FilterSpec:
    def __init__(self, ftype: str, cutoff: Curve, q: float, slope: int):
        self.ftype, self.cutoff, self.q, self.slope = ftype, cutoff, q, slope

    @staticmethod
    def parse(spec: Spec | None) -> "FilterSpec | None":
        if spec is None:
            return None
        result = FilterSpec(
            spec.str("type", "lowpass", FILTER_TYPES),
            parse_curve(spec.raw("cutoff"), spec.at("cutoff"), 10, 22000),
            spec.num("q", 0.707, 0.1, 30),
            spec.int("slope", 12, 12, 24),
        )
        if result.slope not in (12, 24):
            raise ForgeError("12 or 24 expected", spec.at("slope"))
        spec.finish()
        return result

    def apply(self, x: np.ndarray, sr: int, duration: float) -> np.ndarray:
        cutoff = self.cutoff.start() if self.cutoff.is_constant else self.cutoff.render(len(x), sr, duration)
        return filter_signal(x, sr, self.ftype, cutoff, self.q, self.slope)


# ---------- helpers ----------

def db_to_gain(db: float) -> float:
    return 10.0 ** (db / 20.0)


def pan_stereo(mono: np.ndarray, pan: float) -> np.ndarray:
    angle = (pan + 1.0) * np.pi / 4.0
    return np.stack([mono * np.cos(angle), mono * np.sin(angle)], axis=1) * np.sqrt(2.0)


def mix_into(dst: np.ndarray, src: np.ndarray, start: int) -> None:
    if start >= len(dst):
        return
    end = min(len(dst), start + len(src))
    dst[start:end] += src[:end - start]
