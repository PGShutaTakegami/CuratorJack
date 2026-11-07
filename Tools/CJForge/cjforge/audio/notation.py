"""Text notation for patterns.

Melody tokens (one token = one step):  C4  Eb3  F#5  C4:0.6 (velocity)  [C4 E4 G4] (chord)  . (rest)  - (tie/extend)  | (ignored)
Chord tokens:  Cm7  F  G7  Bbmaj7  Ddim  Esus4  C/G  .  -
Drum tokens:   X (accent) x (hit) o (ghost) . (rest)   e.g. "x...x...x...x..."
"""

from __future__ import annotations

import re
from dataclasses import dataclass
from fractions import Fraction

from ..errors import ForgeError

_NOTE = re.compile(r"^([A-Ga-g])([#b]?)(-?\d)$")
_CHORD = re.compile(r"^([A-G])([#b]?)([^/]*)(?:/([A-G][#b]?))?$")
_TOKEN = re.compile(r"\[[^\]]*\]|\S+")
_PITCH_CLASS = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}

CHORD_QUALITIES = {
    "": (0, 4, 7), "maj": (0, 4, 7), "m": (0, 3, 7), "min": (0, 3, 7), "dim": (0, 3, 6), "aug": (0, 4, 8),
    "sus2": (0, 2, 7), "sus4": (0, 5, 7), "6": (0, 4, 7, 9), "m6": (0, 3, 7, 9), "7": (0, 4, 7, 10),
    "maj7": (0, 4, 7, 11), "m7": (0, 3, 7, 10), "mmaj7": (0, 3, 7, 11), "m7b5": (0, 3, 6, 10), "dim7": (0, 3, 6, 9),
    "7sus4": (0, 5, 7, 10), "9": (0, 4, 7, 10, 14), "maj9": (0, 4, 7, 11, 14), "m9": (0, 3, 7, 10, 14),
    "add9": (0, 4, 7, 14), "7b9": (0, 4, 7, 10, 13), "13": (0, 4, 7, 10, 14, 21),
}


def parse_step(text: str, at: str) -> Fraction:
    """'1/16' -> length as a fraction of a whole note (4 beats)."""
    try:
        value = Fraction(text)
    except (ValueError, ZeroDivisionError) as error:
        raise ForgeError(f"step like '1/8' expected, got '{text}'", at) from error
    if value <= 0 or value > 4:
        raise ForgeError("step must be between 0 and 4 whole notes", at)
    return value


def note_to_midi(text: str, at: str) -> int:
    m = _NOTE.match(text)
    if not m:
        raise ForgeError(f"note like 'C4', 'Eb3' or 'F#5' expected, got '{text}'", at)
    letter, accidental, octave = m.groups()
    pc = _PITCH_CLASS[letter.upper()] + (1 if accidental == "#" else -1 if accidental == "b" else 0)
    return 12 * (int(octave) + 1) + pc


def tokens(text: str) -> list[str]:
    return [t for t in _TOKEN.findall(text) if t != "|"]


@dataclass
class Event:
    step: int
    length: int
    pitches: tuple
    velocity: float


def parse_melody(text: str, at: str) -> tuple[list[Event], int]:
    events: list[Event] = []
    toks = tokens(text)
    for i, tok in enumerate(toks):
        where = f"{at} token {i + 1} '{tok}'"
        if tok == ".":
            continue
        if tok == "-":
            if not events or events[-1].step + events[-1].length != i:
                raise ForgeError("'-' (tie) must directly follow a note or another '-'", where)
            events[-1].length += 1
            continue
        velocity = 0.8
        body = tok
        if ":" in tok and not tok.startswith("["):
            body, vel_text = tok.split(":", 1)
            velocity = _velocity(vel_text, where)
        elif tok.startswith("[") and "]:" in tok:
            body, vel_text = tok.rsplit(":", 1)
            velocity = _velocity(vel_text, where)
        if body.startswith("["):
            inner = body.strip("[]").split()
            if not inner:
                raise ForgeError("empty chord", where)
            pitches = tuple(note_to_midi(p, where) for p in inner)
        else:
            pitches = (note_to_midi(body, where),)
        events.append(Event(i, 1, pitches, velocity))
    return events, len(toks)


def _velocity(text: str, at: str) -> float:
    try:
        value = float(text)
    except ValueError as error:
        raise ForgeError("velocity 0..1 expected after ':'", at) from error
    if not 0 <= value <= 1:
        raise ForgeError("velocity must be 0..1", at)
    return value


def chord_intervals(symbol: str, at: str) -> tuple[int, tuple, int | None]:
    m = _CHORD.match(symbol)
    if not m:
        raise ForgeError(f"chord like 'Cm7' or 'F/A' expected, got '{symbol}'", at)
    letter, accidental, quality, bass = m.groups()
    if quality not in CHORD_QUALITIES:
        raise ForgeError(f"unknown chord quality '{quality}' (known: {', '.join(q or 'maj' for q in CHORD_QUALITIES)})", at)
    root = _PITCH_CLASS[letter] + (1 if accidental == "#" else -1 if accidental == "b" else 0)
    bass_pc = None
    if bass:
        bass_pc = _PITCH_CLASS[bass[0]] + (1 if bass[1:] == "#" else -1 if bass[1:] == "b" else 0)
    return root % 12, CHORD_QUALITIES[quality], bass_pc


def voice_chord(root: int, intervals: tuple, bass_pc: int | None, center: int) -> tuple:
    """Place chord tones close to `center` (MIDI) for smooth voice leading."""
    pitches = []
    for interval in intervals:
        pc = (root + interval) % 12
        pitch = center - 6 + ((pc - (center - 6)) % 12)
        pitches.append(pitch)
    pitches.sort()
    if bass_pc is not None:
        bass = pitches[0] - ((pitches[0] - bass_pc) % 12 or 12)
        pitches.insert(0, bass)
    return tuple(pitches)


def parse_chords(text: str, at: str, center: int) -> tuple[list[Event], int]:
    events: list[Event] = []
    toks = tokens(text)
    for i, tok in enumerate(toks):
        where = f"{at} token {i + 1} '{tok}'"
        if tok == ".":
            continue
        if tok == "-":
            if not events or events[-1].step + events[-1].length != i:
                raise ForgeError("'-' (tie) must directly follow a chord or another '-'", where)
            events[-1].length += 1
            continue
        root, intervals, bass = chord_intervals(tok, where)
        events.append(Event(i, 1, voice_chord(root, intervals, bass, center), 0.8))
    return events, len(toks)


def parse_drums(text: str, at: str) -> tuple[list[Event], int]:
    levels = {"X": 1.0, "x": 0.8, "o": 0.45}
    chars = [c for c in text if not c.isspace() and c != "|"]
    events = []
    for i, c in enumerate(chars):
        if c == ".":
            continue
        if c not in levels:
            raise ForgeError(f"drum pattern uses X x o . only (got '{c}' at step {i + 1})", at)
        events.append(Event(i, 1, (), levels[c]))
    return events, len(chars)
