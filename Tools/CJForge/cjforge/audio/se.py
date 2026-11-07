"""Sound effect recipe: layered one-shot sounds (oscillator sweeps, noise, drum voices) + effects."""

from __future__ import annotations

import numpy as np

from ..errors import ForgeError
from ..spec import Spec
from .drums import DrumVoice
from .dsp import OSCILLATORS, Envelope, FilterSpec, db_to_gain, mix_into, oscillator, pan_stereo, parse_curve, seconds
from .effects import apply_effects, parse_effects
from .master import Master


class Layer:
    def __init__(self, spec: Spec):
        self.start = spec.num("start", 0.0, 0, 60)
        self.duration = spec.num("duration", 0.2, 0.001, 60)
        self.gain = db_to_gain(spec.num("gain_db", 0.0, -60, 24))
        self.pan = spec.num("pan", 0.0, -1, 1)
        drum = spec.str("drum", None)
        self.drum = None
        if drum is not None:
            self.drum = DrumVoice(drum, spec.num("tune", 1.0, 0.1, 8), spec.num("decay", 1.0, 0.05, 10),
                                  spec.num("tone", 0.5, 0, 1), spec.at("drum"))
            self.osc = None
        else:
            self.osc = spec.str("osc", "sine", OSCILLATORS)
            self.freq = parse_curve(spec.raw("freq", 440), spec.at("freq"), 1, 22000)
            self.duty = spec.num("duty", 0.5, 0.01, 0.99)
            self.env = Envelope.parse(spec.obj("env", None, "se"), Envelope(0.002, self.duration, 0.0, 0.05))
            fm = spec.obj("fm", None, "se")
            self.fm = None
            if fm is not None:
                self.fm = (fm.num("ratio", 2.0, 0.01, 32), fm.num("index", 2.0, 0, 50), fm.num("decay", None, 0.001, 30))
                fm.finish()
            vib = spec.obj("vibrato", None, "se")
            self.vibrato = None
            if vib is not None:
                self.vibrato = (vib.num("rate", 6.0, 0.1, 60), vib.num("depth", 30.0, 0, 2400))
                vib.finish()
        self.filter = FilterSpec.parse(spec.obj("filter", None, "se"))
        rep = spec.obj("repeat", None, "se")
        self.repeat = (1, 0.0, 0.0, 0.0)
        if rep is not None:
            self.repeat = (rep.int("count", 2, 1, 64), rep.num("interval", 0.08, 0, 10),
                           rep.num("pitch_step", 0.0, -48, 48), rep.num("gain_step_db", 0.0, -24, 24))
            rep.finish()
        spec.finish()

    def render(self, sr: int, rng: np.random.Generator, pitch_shift: float) -> np.ndarray:
        if self.drum is not None:
            sig = self.drum.hit(1.0, sr, rng)
        else:
            gate = max(1, int(self.duration * sr))
            env = self.env.render(gate, sr)
            n = len(env)
            freq = self.freq.render(n, sr, self.duration) * 2.0 ** (pitch_shift / 12.0)
            if self.vibrato is not None:
                rate, depth = self.vibrato
                freq = freq * 2.0 ** (depth * np.sin(2.0 * np.pi * rate * seconds(n, sr)) / 1200.0)
            pm = None
            if self.fm is not None:
                ratio, index, decay = self.fm
                t = seconds(n, sr)
                mod_index = index * (np.exp(-t / decay) if decay else 1.0)
                mod_phase = np.cumsum(freq * ratio) / sr
                pm = mod_index * np.sin(2.0 * np.pi * mod_phase) / (2.0 * np.pi)
            sig = oscillator(self.osc, freq, n, sr, rng, 0.0, self.duty, pm) * env
        if self.filter is not None:
            sig = self.filter.apply(sig, sr, self.duration)
        return sig * self.gain


def render_se(spec: Spec, sr: int, rng: np.random.Generator) -> tuple[np.ndarray, Master]:
    layers = [Layer(s) for s in spec.objs("layers", topic="se")]
    if not layers:
        raise ForgeError("at least one layer is required", spec.at("layers"))
    effects = parse_effects(spec.objs("effects", [], topic="effects"))
    master = Master(spec.obj("master", None, "master"), default_channels=1)

    rendered = []
    for layer in layers:
        count, interval, pitch_step, gain_step = layer.repeat
        for k in range(count):
            sig = layer.render(sr, rng, pitch_step * k) * db_to_gain(gain_step * k)
            rendered.append((int((layer.start + interval * k) * sr), pan_stereo(sig, layer.pan)))
    length = max(start + len(sig) for start, sig in rendered)
    mix = np.zeros((length, 2))
    for start, sig in rendered:
        mix_into(mix, sig, start)
    mix = apply_effects(mix, effects, sr, rng)
    return master.finish(mix, sr, loop=False), master
