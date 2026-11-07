"""BGM recipe: tempo grid, instruments, tracks (notes / chords / drums), seamless loop."""

from __future__ import annotations

import math

import numpy as np

from ..errors import ForgeError
from ..spec import Spec
from .drums import DrumVoice
from .dsp import db_to_gain, mix_into, pan_stereo
from .effects import apply_effects, effects_tail, parse_effects
from .instruments import Instrument
from .master import Master, wrap_loop
from .melody import generate_melody
from .notation import Event, note_to_midi, parse_chords, parse_drums, parse_melody, parse_step

ARP_ORDERS = ("up", "down", "updown", "random")
CHORD_STYLES = ("block", "arp", "root")


class Grid:
    def __init__(self, tempo: float, beats_per_bar: int, swing: float):
        self.beat = 60.0 / tempo
        self.beats_per_bar = beats_per_bar
        self.swing = swing

    def bar_seconds(self, bars: float) -> float:
        return bars * self.beats_per_bar * self.beat

    def step_seconds(self, step) -> float:
        return float(step) * 4.0 * self.beat


class Track:
    def __init__(self, spec: Spec, grid: Grid, instruments: dict, song_bars: int):
        self.spec_path = spec.path
        self.gain = db_to_gain(spec.num("gain_db", 0.0, -60, 24))
        self.pan = spec.num("pan", 0.0, -1, 1)
        self.velocity = spec.num("velocity", 1.0, 0, 2)
        self.start_bar = spec.num("start_bar", 0, 0, song_bars)
        self.end_bar = spec.num("end_bar", song_bars, 0, song_bars)
        if self.end_bar <= self.start_bar:
            raise ForgeError("end_bar must be greater than start_bar", spec.at("end_bar"))
        self.step = parse_step(spec.str("step", "1/8"), spec.at("step"))
        self.swing = spec.num("swing", grid.swing, 0, 0.5)
        self.gate = spec.num("gate", 0.92, 0.05, 4)
        human = spec.obj("humanize", None, "bgm")
        self.humanize = (0.0, 0.0)
        if human is not None:
            self.humanize = (human.num("time", 0.0, 0, 0.1), human.num("velocity", 0.0, 0, 1))
            human.finish()
        self.effects = parse_effects(spec.objs("effects", [], topic="effects"))

        kinds = [k for k in ("notes", "chords", "drum") if spec.has(k)]
        if len(kinds) != 1:
            raise ForgeError("a track needs exactly one of 'notes', 'chords' or 'drum'", spec.path, "run `cjforge ref bgm`")
        self.kind = kinds[0]
        self.drum = None
        self.instrument = None
        if self.kind == "drum":
            self.drum = DrumVoice(spec.str("drum"), spec.num("tune", 1.0, 0.1, 8), spec.num("decay", 1.0, 0.05, 10),
                                  spec.num("tone", 0.5, 0, 1), spec.at("drum"))
            self.events, self.length = parse_drums(spec.str("pattern"), spec.at("pattern"))
        else:
            name = spec.str("instrument")
            if name not in instruments:
                raise ForgeError(f"instrument '{name}' is not defined in 'instruments'", spec.at("instrument"))
            self.instrument = instruments[name]
            if self.kind == "notes":
                if isinstance(spec.raw("notes"), dict):
                    wrapper = spec.obj("notes", topic="melody")
                    self.events, self.length = generate_melody(wrapper.obj("generate", topic="melody"), self.step,
                                                               grid.beats_per_bar)
                    wrapper.finish()
                else:
                    self.events, self.length = parse_melody(spec.str("notes"), spec.at("notes"))
                self.transpose = spec.int("transpose", 0, -48, 48)
            else:
                center = note_to_midi(spec.str("center", "C4"), spec.at("center"))
                self.events, self.length = parse_chords(spec.str("chords"), spec.at("chords"), center)
                self.transpose = spec.int("transpose", 0, -48, 48)
                self.style = spec.str("style", "block", CHORD_STYLES)
                arp = spec.obj("arp", None, "bgm")
                self.arp_step = self.step / 4
                self.arp_order = "up"
                self.arp_octaves = 1
                if arp is not None:
                    self.arp_step = parse_step(arp.str("step", "1/16"), arp.at("step"))
                    self.arp_order = arp.str("order", "up", ARP_ORDERS)
                    self.arp_octaves = arp.int("octaves", 1, 1, 4)
                    arp.finish()
        if self.length == 0:
            raise ForgeError("pattern is empty", spec.path)
        spec.finish()

    @property
    def release(self) -> float:
        return self.instrument.release if self.instrument else 1.5

    def _hits(self, grid: Grid):
        """Yield (time, duration, pitches, velocity) for the whole section, repeating the pattern."""
        step_sec = grid.step_seconds(self.step)
        start = grid.bar_seconds(self.start_bar)
        end = grid.bar_seconds(self.end_bar)
        pattern_sec = step_sec * self.length
        swing_on = self.step <= 0.125 and self.swing > 0
        reps = math.ceil((end - start) / pattern_sec)
        for r in range(reps):
            for ev in self.events:
                t = start + r * pattern_sec + ev.step * step_sec
                if t >= end - 1e-9:
                    continue
                if swing_on and (r * self.length + ev.step) % 2 == 1:
                    t += self.swing * step_sec
                duration = min(ev.length * step_sec, end - t)
                yield t, duration, ev.pitches, ev.velocity

    def render(self, grid: Grid, total: int, sr: int, rng: np.random.Generator) -> np.ndarray:
        out = np.zeros(total)
        jitter_t, jitter_v = self.humanize
        for t, duration, pitches, velocity in self._hits(grid):
            vel = velocity * self.velocity
            if jitter_t:
                t = max(0.0, t + rng.uniform(-jitter_t, jitter_t))
            if jitter_v:
                vel *= 1.0 + rng.uniform(-jitter_v, jitter_v)
            vel = float(np.clip(vel, 0.0, 1.0))
            for note_t, note_len, note_pitches in self._expand(t, duration, pitches, grid, rng):
                start = int(note_t * sr)
                if self.drum is not None:
                    mix_into(out, self.drum.hit(vel, sr, rng), start)
                    continue
                for pitch in note_pitches:
                    mix_into(out, self.instrument.note(pitch + self.transpose, note_len * self.gate, vel, sr, rng), start)
        stereo = pan_stereo(out * self.gain, self.pan)
        return apply_effects(stereo, self.effects, sr, rng)[:total]

    def _expand(self, t, duration, pitches, grid, rng):
        if self.kind != "chords" or self.style == "block":
            yield t, duration, pitches
            return
        if self.style == "root":
            yield t, duration, (pitches[0] - 12,)
            return
        tones = [p + 12 * o for o in range(self.arp_octaves) for p in pitches]
        if self.arp_order == "down":
            tones = tones[::-1]
        elif self.arp_order == "updown" and len(tones) > 2:
            tones = tones + tones[-2:0:-1]
        step = grid.step_seconds(self.arp_step)
        count = max(1, int(round(duration / step)))
        for i in range(count):
            tone = tones[rng.integers(len(tones))] if self.arp_order == "random" else tones[i % len(tones)]
            yield t + i * step, step, (tone,)


def render_bgm(spec: Spec, sr: int, rng: np.random.Generator) -> tuple[np.ndarray, Master, bool]:
    tempo = spec.num("tempo", 100, 20, 300)
    grid = Grid(tempo, spec.int("beats_per_bar", 4, 1, 16), spec.num("swing", 0.0, 0, 0.5))
    bars = spec.int("bars", 8, 1, 512)
    loop = spec.bool("loop", True)

    instruments = {}
    inst_spec = spec.obj("instruments", {}, "instruments")
    for name in inst_spec.keys():
        data = inst_spec.raw(name)
        if not isinstance(data, dict):
            raise ForgeError("object expected", inst_spec.at(name))
        instruments[name] = Instrument(data, inst_spec.at(name))
    inst_spec.finish()

    tracks = [Track(s, grid, instruments, bars) for s in spec.objs("tracks", topic="bgm")]
    if not tracks:
        raise ForgeError("at least one track is required", spec.at("tracks"))
    effects = parse_effects(spec.objs("effects", [], topic="effects"))
    master = Master(spec.obj("master", None, "master"), default_channels=2)

    song = grid.bar_seconds(bars)
    loop_samples = int(round(song * sr))
    tail = max(t.release + effects_tail(t.effects) for t in tracks) + 0.5
    total = loop_samples + int(tail * sr)
    mix = np.zeros((total, 2))
    for track in tracks:
        mix += track.render(grid, total, sr, rng)
    mix = apply_effects(mix, effects, sr, rng)
    mix = wrap_loop(mix, loop_samples) if loop else mix
    return master.finish(mix, sr, loop), master, loop
