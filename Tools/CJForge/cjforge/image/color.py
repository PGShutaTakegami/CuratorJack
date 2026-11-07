"""Colors: '#RGB' '#RRGGBB' '#RRGGBBAA' '$palette_name' 'transparent' 'white' 'black' or [r, g, b(, a)] (0-255)."""

from __future__ import annotations

import numpy as np

from ..errors import ForgeError

NAMED = {"transparent": (0, 0, 0, 0), "white": (255, 255, 255, 255), "black": (0, 0, 0, 255)}


class Palette:
    def __init__(self, raw: dict | None, at: str):
        self._colors: dict[str, np.ndarray] = {}
        for name, value in (raw or {}).items():
            self._colors[name] = parse_color(value, f"{at}.{name}", None)

    def get(self, name: str, at: str) -> np.ndarray:
        if name not in self._colors:
            raise ForgeError(f"palette color '{name}' is not defined", at)
        return self._colors[name]


def parse_color(value, at: str, palette: Palette | None) -> np.ndarray:
    """Returns straight RGBA floats 0..1."""
    if isinstance(value, str):
        if value.startswith("$"):
            if palette is None:
                raise ForgeError("palette references are not allowed here", at)
            return palette.get(value[1:], at)
        if value in NAMED:
            return np.array(NAMED[value], dtype=np.float64) / 255.0
        text = value.lstrip("#")
        if value.startswith("#") and len(text) in (3, 4):
            text = "".join(c * 2 for c in text)
        if value.startswith("#") and len(text) in (6, 8):
            try:
                channels = [int(text[i:i + 2], 16) for i in range(0, len(text), 2)]
            except ValueError as error:
                raise ForgeError(f"bad color '{value}'", at) from error
            if len(channels) == 3:
                channels.append(255)
            return np.array(channels, dtype=np.float64) / 255.0
        raise ForgeError(f"bad color '{value}' (use #RRGGBB, #RRGGBBAA, $name or {list(NAMED)})", at)
    if isinstance(value, list) and len(value) in (3, 4) and all(isinstance(v, (int, float)) for v in value):
        channels = list(value) + ([255] if len(value) == 3 else [])
        return np.clip(np.array(channels, dtype=np.float64) / 255.0, 0.0, 1.0)
    raise ForgeError("color expected", at)
