"""Melodic instruments: presets + per-note synthesis (oscillator layers, unison, FM, filter envelope, vibrato)."""

from __future__ import annotations

import copy

import numpy as np

from ..errors import ForgeError
from ..spec import Spec
from .dsp import OSCILLATORS, Envelope, FILTER_TYPES, db_to_gain, filter_signal, oscillator, seconds

PRESETS: dict[str, dict] = {
    "epiano": {"osc": "sine", "fm": {"ratio": 1, "index": 1.8, "decay": 0.8},
               "env": {"a": 0.002, "d": 1.6, "s": 0.15, "r": 0.5}, "gain_db": -2},
    "piano": {"osc": [{"osc": "triangle"}, {"osc": "sine", "semitones": 12, "gain_db": -10}],
              "env": {"a": 0.002, "d": 2.0, "s": 0.05, "r": 0.4},
              "filter": {"type": "lowpass", "cutoff": 2500, "env_amount": 3000},
              "noise": {"gain_db": -28, "decay": 0.015}},
    "upright_bass": {"osc": [{"osc": "triangle"}, {"osc": "sine", "gain_db": -4}],
                     "env": {"a": 0.004, "d": 0.6, "s": 0.25, "r": 0.12},
                     "filter": {"type": "lowpass", "cutoff": 500, "env_amount": 900, "q": 0.9},
                     "noise": {"gain_db": -30, "decay": 0.02}},
    "synth_bass": {"osc": "saw", "unison": 2, "detune": 8, "env": {"a": 0.003, "d": 0.3, "s": 0.6, "r": 0.1},
                   "filter": {"type": "lowpass", "cutoff": 300, "env_amount": 1500, "q": 2}},
    "pad": {"osc": "saw", "unison": 4, "detune": 14, "env": {"a": 0.6, "d": 1.0, "s": 0.8, "r": 1.5},
            "filter": {"type": "lowpass", "cutoff": 1400, "q": 0.7}, "gain_db": -6},
    "strings": {"osc": "saw", "unison": 5, "detune": 10, "vibrato": {"rate": 5.2, "depth": 8, "delay": 0.25},
                "env": {"a": 0.25, "d": 0.5, "s": 0.85, "r": 0.6},
                "filter": {"type": "lowpass", "cutoff": 3200, "q": 0.6}, "gain_db": -6},
    "lead": {"osc": "square", "vibrato": {"rate": 5.5, "depth": 12, "delay": 0.15},
             "env": {"a": 0.01, "d": 0.2, "s": 0.7, "r": 0.15},
             "filter": {"type": "lowpass", "cutoff": 2800, "q": 1.0}, "gain_db": -6},
    "pluck": {"osc": "saw", "env": {"a": 0.002, "d": 0.35, "s": 0, "r": 0.2},
              "filter": {"type": "lowpass", "cutoff": 600, "env_amount": 4000, "q": 1.2}},
    "bell": {"osc": "sine", "fm": {"ratio": 3.5, "index": 3.0, "decay": 1.2},
             "env": {"a": 0.001, "d": 2.5, "s": 0, "r": 1.5}, "gain_db": -4},
    "music_box": {"osc": "sine", "fm": {"ratio": 5, "index": 1.2, "decay": 0.3},
                  "env": {"a": 0.001, "d": 1.2, "s": 0, "r": 0.8}, "gain_db": -4},
    "marimba": {"osc": "sine", "fm": {"ratio": 4, "index": 2, "decay": 0.06},
                "env": {"a": 0.001, "d": 0.5, "s": 0, "r": 0.3}},
    "organ": {"osc": [{"osc": "sine"}, {"osc": "sine", "semitones": 12, "gain_db": -4},
                      {"osc": "sine", "semitones": 19, "gain_db": -8}, {"osc": "sine", "semitones": 24, "gain_db": -10}],
              "env": {"a": 0.01, "d": 0.1, "s": 1, "r": 0.08}, "vibrato": {"rate": 6.5, "depth": 6}, "gain_db": -4},
    "brass": {"osc": "saw", "unison": 2, "detune": 6, "env": {"a": 0.06, "d": 0.3, "s": 0.75, "r": 0.2},
              "filter": {"type": "lowpass", "cutoff": 700, "env_amount": 2500, "q": 1.0}, "gain_db": -4},
    "chip": {"osc": "pulse", "duty": 0.25, "env": {"a": 0.001, "d": 0.1, "s": 0.6, "r": 0.05}, "gain_db": -8},
}


def midi_to_freq(midi: float) -> float:
    return 440.0 * 2.0 ** ((midi - 69.0) / 12.0)


def merge(base: dict, override: dict) -> dict:
    result = copy.deepcopy(base)
    for key, value in override.items():
        if isinstance(value, dict) and isinstance(result.get(key), dict):
            result[key] = merge(result[key], value)
        else:
            result[key] = copy.deepcopy(value)
    return result


class OscLayer:
    def __init__(self, kind: str, semitones: float, cents: float, gain: float, duty: float):
        self.kind, self.semitones, self.cents, self.gain, self.duty = kind, semitones, cents, gain, duty


class Instrument:
    def __init__(self, data: dict, path: str):
        preset_name = data.get("preset")
        if preset_name is not None:
            if preset_name not in PRESETS:
                raise ForgeError(f"unknown preset '{preset_name}'", f"{path}.preset", "run `cjforge ref instruments`")
            data = merge(PRESETS[preset_name], {k: v for k, v in data.items() if k != "preset"})
            data["preset"] = preset_name
        spec = Spec(data, path, "instruments")
        spec.str("preset", None)
        self.layers = self._parse_layers(spec)
        self.unison = spec.int("unison", 1, 1, 16)
        self.detune = spec.num("detune", 10.0, 0, 200)
        self.env = Envelope.parse(spec.obj("env", None))
        self.gain = db_to_gain(spec.num("gain_db", 0.0, -60, 24))
        self.velocity_sens = spec.num("velocity_sens", 0.7, 0, 1)
        self.octave = spec.int("octave", 0, -4, 4)

        fm = spec.obj("fm", None)
        self.fm = None
        if fm is not None:
            self.fm = (fm.num("ratio", 1.0, 0.01, 32), fm.num("index", 1.0, 0, 50), fm.num("decay", None, 0.001, 30))
            fm.finish()

        vib = spec.obj("vibrato", None)
        self.vibrato = None
        if vib is not None:
            self.vibrato = (vib.num("rate", 5.0, 0.1, 20), vib.num("depth", 10.0, 0, 200), vib.num("delay", 0.0, 0, 5))
            vib.finish()

        flt = spec.obj("filter", None)
        self.filter = None
        if flt is not None:
            self.filter = {
                "type": flt.str("type", "lowpass", FILTER_TYPES),
                "cutoff": flt.num("cutoff", lo=10, hi=22000),
                "q": flt.num("q", 0.707, 0.1, 30),
                "slope": flt.int("slope", 12, 12, 24),
                "env_amount": flt.num("env_amount", 0.0, -20000, 20000),
                "key_track": flt.num("key_track", 0.0, 0, 1),
            }
            flt.finish()

        noise = spec.obj("noise", None)
        self.noise = None
        if noise is not None:
            self.noise = (db_to_gain(noise.num("gain_db", -24.0, -80, 12)), noise.num("decay", 0.02, 0.001, 2))
            noise.finish()
        spec.finish()
        self._cache: dict = {}

    def _parse_layers(self, spec: Spec) -> list[OscLayer]:
        osc = spec.raw("osc", "sine")
        duty = spec.num("duty", 0.5, 0.01, 0.99)
        if isinstance(osc, str):
            if osc not in OSCILLATORS:
                raise ForgeError(f"'{osc}' is not one of {list(OSCILLATORS)}", spec.at("osc"))
            return [OscLayer(osc, 0.0, 0.0, 1.0, duty)]
        layers = []
        for layer in spec.objs("osc", topic="instruments"):
            layers.append(OscLayer(layer.str("osc", choices=OSCILLATORS), layer.num("semitones", 0.0, -48, 48),
                                   layer.num("cents", 0.0, -1200, 1200), db_to_gain(layer.num("gain_db", 0.0, -60, 24)),
                                   layer.num("duty", duty, 0.01, 0.99)))
            layer.finish()
        if not layers:
            raise ForgeError("at least one oscillator is required", spec.at("osc"))
        return layers

    @property
    def release(self) -> float:
        return self.env.release

    def note(self, midi: float, gate_seconds: float, velocity: float, sr: int, rng: np.random.Generator) -> np.ndarray:
        midi += 12 * self.octave
        gate = max(1, int(gate_seconds * sr))
        key = (round(midi, 3), gate, round(velocity, 2))
        cached = self._cache.get(key)
        if cached is None:
            cached = self._render(midi, gate, velocity, sr, rng)
            self._cache[key] = cached
        return cached

    def _render(self, midi: float, gate: int, velocity: float, sr: int, rng: np.random.Generator) -> np.ndarray:
        env = self.env.render(gate, sr)
        n = len(env)
        t = seconds(n, sr)
        base = midi_to_freq(midi)
        freq = np.full(n, base)
        if self.vibrato is not None:
            rate, depth, delay = self.vibrato
            ramp = np.clip((t - delay) / 0.2, 0.0, 1.0)
            freq = freq * 2.0 ** (depth * ramp * np.sin(2.0 * np.pi * rate * t) / 1200.0)

        pm = None
        if self.fm is not None:
            ratio, index, decay = self.fm
            mod_index = index * (np.exp(-t / decay) if decay else 1.0)
            pm = mod_index * np.sin(2.0 * np.pi * base * ratio * t) / (2.0 * np.pi)

        out = np.zeros(n)
        for layer in self.layers:
            layer_freq = freq * 2.0 ** ((layer.semitones + layer.cents / 100.0) / 12.0)
            voices = np.zeros(n)
            for v in range(self.unison):
                spread = 0.0 if self.unison == 1 else (v / (self.unison - 1) - 0.5) * self.detune
                phase = 0.0 if self.unison == 1 else rng.uniform()
                voices += oscillator(layer.kind, layer_freq * 2.0 ** (spread / 1200.0), n, sr, rng, phase, layer.duty, pm)
            out += voices * layer.gain / np.sqrt(self.unison)

        if self.noise is not None:
            gain, decay = self.noise
            out += rng.uniform(-1.0, 1.0, n) * gain * np.exp(-t / decay)

        if self.filter is not None:
            f = self.filter
            cutoff = f["cutoff"] * (base / 261.63) ** f["key_track"]
            if f["env_amount"]:
                shape = env / (np.max(env) + 1e-12)
                cutoff = cutoff + f["env_amount"] * shape * (0.5 + 0.5 * velocity)
            out = filter_signal(out, sr, f["type"], cutoff, f["q"], f["slope"])

        amp = (1.0 - self.velocity_sens) + self.velocity_sens * velocity
        return out * env * amp * self.gain
