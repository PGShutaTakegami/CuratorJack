"""Procedural melody: built only from a scale, the harmony and a seed (no stored phrases or songs).

The same parameters always give the same melody; change `seed` to get a different one.
Rhythm is generated per phrase from beat strength and repeated with variation, so phrases answer each other;
pitches move mostly by step, land on chord tones on the beat, and resolve to the tonic at the end.
"""

from __future__ import annotations

import math
from fractions import Fraction

import numpy as np

from ..errors import ForgeError
from ..spec import Spec
from .notation import Event, chord_intervals, note_to_midi, parse_chords, parse_step, tokens

SCALES = {
    "major": (0, 2, 4, 5, 7, 9, 11), "minor": (0, 2, 3, 5, 7, 8, 10), "harmonic_minor": (0, 2, 3, 5, 7, 8, 11),
    "dorian": (0, 2, 3, 5, 7, 9, 10), "mixolydian": (0, 2, 4, 5, 7, 9, 10), "phrygian": (0, 1, 3, 5, 7, 8, 10),
    "pentatonic_major": (0, 2, 4, 7, 9), "pentatonic_minor": (0, 3, 5, 7, 10), "blues": (0, 3, 5, 6, 7, 10),
}
_KEY = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}


def _key_root(text: str, at: str) -> int:
    if not text or text[0] not in _KEY or text[1:] not in ("", "#", "b"):
        raise ForgeError(f"key like 'D', 'Bb' or 'F#' expected, got '{text}'", at)
    return (_KEY[text[0]] + {"": 0, "#": 1, "b": -1}[text[1:]]) % 12


class _Harmony:
    """Chord pitch classes at any melody step (the chord pattern repeats)."""

    def __init__(self, text: str | None, chord_step: Fraction, at: str):
        self.chord_step = chord_step
        self.slots: list[frozenset | None] = []
        self.roots: list[int | None] = []
        if text is None:
            return
        parse_chords(text, at, 60)  # validates the text with token positions in errors
        for tok in tokens(text):
            if tok == "-" and self.slots:
                self.slots.append(self.slots[-1])
                self.roots.append(self.roots[-1])
            elif tok in (".", "-"):
                self.slots.append(None)
                self.roots.append(None)
            else:
                root, intervals, _ = chord_intervals(tok, at)
                self.slots.append(frozenset((root + i) % 12 for i in intervals))
                self.roots.append(root)

    def at(self, time: Fraction) -> tuple[frozenset | None, int | None]:
        if not self.slots:
            return None, None
        index = int(time / self.chord_step) % len(self.slots)
        return self.slots[index], self.roots[index]


def generate_melody(spec: Spec, step: Fraction, beats_per_bar: int) -> tuple[list[Event], int]:
    root = _key_root(spec.str("key", "C"), spec.at("key"))
    scale = SCALES[spec.str("scale", "major", tuple(SCALES))]
    bars = spec.int("bars", 4, 1, 64)
    low = note_to_midi(spec.str("low", "C4"), spec.at("low"))
    high = note_to_midi(spec.str("high", "C6"), spec.at("high"))
    if high - low < 7:
        raise ForgeError("high must be at least a fifth above low", spec.at("high"))
    density = spec.num("density", 0.5, 0.05, 1)
    phrase_bars = spec.int("phrase_bars", 2, 1, bars)
    variation = spec.num("variation", 0.3, 0, 1)
    max_leap = spec.int("max_leap", 7, 2, 12)
    rng = np.random.default_rng(spec.int("seed", 1, 0, 2**31 - 1))
    harmony = _Harmony(spec.str("chords", None), parse_step(spec.str("chord_step", "1/4"), spec.at("chord_step")),
                       spec.at("chords"))
    spec.finish()

    steps_per_bar = Fraction(beats_per_bar, 4) / step
    if steps_per_bar.denominator != 1:
        raise ForgeError(f"step {step} does not divide a bar of {beats_per_bar} beats", spec.path)
    steps_per_bar = int(steps_per_bar)
    steps_per_beat = max(1, steps_per_bar // beats_per_bar)
    phrase_steps = phrase_bars * steps_per_bar
    total = bars * steps_per_bar

    onsets = _rhythm(rng, total, phrase_steps, steps_per_bar, steps_per_beat, density, variation)
    scale_pcs = frozenset((root + i) % 12 for i in scale)
    events: list[Event] = []
    prev = None
    last_interval = 0
    starts = [s for s in range(total) if onsets[s]]
    for n, s in enumerate(starts):
        chord, chord_root = harmony.at(s * step)
        phrase = s // phrase_steps
        arch = math.sin(math.pi * (s % phrase_steps) / phrase_steps)
        height = 0.3 + 0.5 * arch if phrase % 2 == 0 else 0.65 - 0.35 * arch
        target = low + (high - low) * height
        on_beat = s % steps_per_beat == 0
        next_start = starts[n + 1] if n + 1 < len(starts) else total
        phrase_end = next_start % phrase_steps == 0 or next_start == total
        if n == len(starts) - 1:
            allowed = {root} if chord is None or root in chord else {chord_root}
        elif chord is not None and (on_beat or phrase_end):
            allowed = set(chord)
        else:
            allowed = {pc for pc in scale_pcs | (chord or set())
                       if chord is None or pc in chord or (pc - 1) % 12 not in chord}
        pool = [p for p in range(low, high + 1) if p % 12 in allowed] or \
            [p for p in range(low, high + 1) if p % 12 in scale_pcs]
        if prev is None:
            middle = (low + high) / 2
            pitch = min(pool, key=lambda p: abs(p - middle) + rng.uniform(0, 3))
        else:
            pitch = _next_pitch(rng, pool, prev, last_interval, target, max_leap)
            last_interval = pitch - prev
        prev = pitch

        length = next_start - s
        if length >= 3 and not phrase_end and rng.random() < 0.3:
            length -= 1
        strong = s % steps_per_bar == 0
        velocity = 0.85 if s % phrase_steps == 0 else 0.78 if strong else 0.64 + rng.uniform(-0.05, 0.05)
        events.append(Event(s, length, (int(pitch),), float(velocity)))
    return events, total


def _rhythm(rng, total, phrase_steps, steps_per_bar, steps_per_beat, density, variation) -> list[bool]:
    def strength(pos: int) -> float:
        if pos % steps_per_bar == 0:
            return 1.0
        if pos % (steps_per_bar // 2 or 1) == 0:
            return 0.8
        if pos % steps_per_beat == 0:
            return 0.6
        return 0.35 if pos % max(1, steps_per_beat // 2) == 0 else 0.2

    def roll(pos: int) -> bool:
        breath = 0.4 if pos % phrase_steps >= phrase_steps * 0.75 else 1.0
        return rng.random() < min(0.97, density * strength(pos) * 1.6 * breath)

    base = [roll(p) for p in range(phrase_steps)]
    base[0] = True
    onsets: list[bool] = []
    for k in range(math.ceil(total / phrase_steps)):
        phrase = list(base)
        if k % 2 == 1 or k == math.ceil(total / phrase_steps) - 1:
            for p in range(1, phrase_steps):
                if rng.random() < variation * 0.5:
                    phrase[p] = roll(p)
        onsets.extend(phrase)
    onsets = onsets[:total]
    last_bar = total - steps_per_bar
    for s in range(last_bar + 1, total):
        onsets[s] = False
    onsets[last_bar] = True
    return onsets


def _next_pitch(rng, pool, prev, last_interval, target, max_leap) -> int:
    """Prefer steps, avoid repeated notes, drift toward the phrase's arch-shaped target, recover after leaps."""
    best, best_cost = None, math.inf
    for p in pool:
        interval = p - prev
        size = abs(interval)
        if size > max_leap:
            continue
        if size == 0:
            cost = 3.5 + (3.0 if last_interval == 0 else 0.0)
        else:
            cost = 0.5 if size <= 2 else 1.5 if size <= 4 else 3.0 if size <= 7 else 5.0
        cost += 0.12 * abs(p - target) + rng.uniform(0, 1.2)
        if abs(last_interval) > 4 and interval * last_interval > 0:
            cost += 3.0
        if cost < best_cost:
            best, best_cost = p, cost
    return best if best is not None else min(pool, key=lambda p: abs(p - prev))
