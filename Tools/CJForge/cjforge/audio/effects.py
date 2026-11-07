"""Effects chain. Each effect works on stereo signals (n, 2) and may lengthen them (tails)."""

from __future__ import annotations

import numpy as np

from ..errors import ForgeError
from ..spec import Spec
from .dsp import FILTER_TYPES, db_to_gain, filter_signal

EFFECT_TYPES = ("reverb", "delay", "distortion", "bitcrush", "filter", "gain", "tremolo")


class Effect:
    tail: float = 0.0

    def apply(self, x: np.ndarray, sr: int, rng: np.random.Generator) -> np.ndarray:
        raise NotImplementedError


class Reverb(Effect):
    def __init__(self, spec: Spec):
        self.mix = spec.num("mix", 0.25, 0, 1)
        self.decay = spec.num("decay", 1.5, 0.05, 12)
        self.predelay = spec.num("predelay", 0.01, 0, 0.5)
        self.damping = spec.num("damping", 6000, 200, 20000)
        self.width = spec.num("width", 1.0, 0, 1)
        self.tail = self.decay + self.predelay

    def apply(self, x, sr, rng):
        n_ir = int(self.decay * sr)
        t = np.arange(n_ir) / sr
        envelope = np.exp(-6.9 * t / self.decay)
        common = rng.standard_normal(n_ir)
        irs = []
        for _ in range(2):
            own = rng.standard_normal(n_ir)
            ir = (common * (1.0 - self.width) + own * self.width) * envelope
            ir = filter_signal(ir, sr, "lowpass", self.damping)
            ir = np.concatenate([np.zeros(int(self.predelay * sr)), ir])
            irs.append(ir / (np.sqrt(np.sum(ir * ir)) + 1e-12))
        out_len = len(x) + len(irs[0])
        size = 1 << int(np.ceil(np.log2(out_len)))
        wet = np.zeros((out_len, 2))
        for c in range(2):
            wet[:, c] = np.fft.irfft(np.fft.rfft(x[:, c], size) * np.fft.rfft(irs[c], size), size)[:out_len]
        dry = np.zeros((out_len, 2))
        dry[:len(x)] = x
        return dry * (1.0 - self.mix * 0.5) + wet * self.mix


class Delay(Effect):
    def __init__(self, spec: Spec):
        self.time = spec.num("time", 0.25, 0.005, 4)
        self.feedback = spec.num("feedback", 0.35, 0, 0.95)
        self.mix = spec.num("mix", 0.3, 0, 1)
        self.pingpong = spec.bool("pingpong", False)
        repeats = int(np.ceil(np.log(1e-3) / np.log(max(self.feedback, 1e-3)))) if self.feedback > 0 else 1
        self.repeats = max(1, min(repeats, 64))
        self.tail = self.time * self.repeats

    def apply(self, x, sr, rng):
        step = int(self.time * sr)
        out = np.zeros((len(x) + step * self.repeats, 2))
        out[:len(x)] += x
        gain = self.mix
        for k in range(1, self.repeats + 1):
            echo = x * gain
            if self.pingpong and k % 2 == 1:
                echo = echo[:, ::-1]
            out[k * step:k * step + len(x)] += echo
            gain *= self.feedback
        return out


class Distortion(Effect):
    def __init__(self, spec: Spec):
        self.drive = spec.num("drive", 3.0, 0.1, 100)
        self.mix = spec.num("mix", 1.0, 0, 1)

    def apply(self, x, sr, rng):
        wet = np.tanh(x * self.drive) / np.tanh(self.drive)
        return x * (1.0 - self.mix) + wet * self.mix


class Bitcrush(Effect):
    def __init__(self, spec: Spec):
        self.bits = spec.int("bits", 8, 1, 16)
        self.rate = spec.num("rate", 11025, 500, 44100)

    def apply(self, x, sr, rng):
        hold = max(1, int(round(sr / self.rate)))
        held = np.repeat(x[::hold], hold, axis=0)[:len(x)]
        levels = 2 ** (self.bits - 1)
        return np.round(held * levels) / levels


class FilterEffect(Effect):
    def __init__(self, spec: Spec):
        self.ftype = spec.str("mode", "lowpass", FILTER_TYPES)
        self.cutoff = spec.num("cutoff", lo=10, hi=22000)
        self.q = spec.num("q", 0.707, 0.1, 30)
        self.slope = spec.int("slope", 12, 12, 24)

    def apply(self, x, sr, rng):
        return filter_signal(x, sr, self.ftype, self.cutoff, self.q, self.slope)


class Gain(Effect):
    def __init__(self, spec: Spec):
        self.gain = db_to_gain(spec.num("db", 0.0, -60, 24))

    def apply(self, x, sr, rng):
        return x * self.gain


class Tremolo(Effect):
    def __init__(self, spec: Spec):
        self.rate = spec.num("rate", 5.0, 0.05, 40)
        self.depth = spec.num("depth", 0.5, 0, 1)

    def apply(self, x, sr, rng):
        lfo = 1.0 - self.depth * 0.5 * (1.0 - np.cos(2.0 * np.pi * self.rate * np.arange(len(x)) / sr))
        return x * lfo[:, None]


_TYPES = {
    "reverb": Reverb, "delay": Delay, "distortion": Distortion, "bitcrush": Bitcrush,
    "filter": FilterEffect, "gain": Gain, "tremolo": Tremolo,
}


def parse_effects(specs: list[Spec]) -> list[Effect]:
    effects = []
    for spec in specs:
        kind = spec.str("type", choices=EFFECT_TYPES)
        try:
            effect = _TYPES[kind](spec)
        except KeyError as error:
            raise ForgeError(f"unknown effect '{kind}'", spec.at("type")) from error
        spec.finish()
        effects.append(effect)
    return effects


def apply_effects(x: np.ndarray, effects: list[Effect], sr: int, rng: np.random.Generator) -> np.ndarray:
    for effect in effects:
        x = effect.apply(x, sr, rng)
    return x


def effects_tail(effects: list[Effect]) -> float:
    return sum(e.tail for e in effects)
