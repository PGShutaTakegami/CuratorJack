"""Paint = solid color or gradient. Produces straight RGBA (h, w, 4)."""

from __future__ import annotations

import numpy as np

from ..errors import ForgeError
from ..spec import Spec, check_num
from .color import Palette, parse_color


def _point(value, at: str) -> np.ndarray:
    if not isinstance(value, list) or len(value) != 2:
        raise ForgeError("[x, y] expected", at)
    return np.array([check_num(value[0], f"{at}[0]"), check_num(value[1], f"{at}[1]")])


class Paint:
    def __init__(self, value, at: str, palette: Palette):
        self.color = None
        self.kind = "solid"
        if not isinstance(value, dict):
            self.color = parse_color(value, at, palette)
            return
        spec = Spec(value, at, "image")
        stops = []
        for i, stop in enumerate(spec.list("stops")):
            if not isinstance(stop, list) or len(stop) != 2:
                raise ForgeError("[position 0..1, color] expected", f"{at}.stops[{i}]")
            stops.append((check_num(stop[0], f"{at}.stops[{i}][0]", 0, 1), parse_color(stop[1], f"{at}.stops[{i}][1]", palette)))
        if len(stops) < 2:
            raise ForgeError("at least two stops are required", f"{at}.stops")
        stops.sort(key=lambda s: s[0])
        self.positions = np.array([s[0] for s in stops])
        self.colors = np.stack([s[1] for s in stops])
        if spec.has("linear"):
            lin = spec.obj("linear")
            self.kind = "linear"
            self.start = _point(lin.raw("from"), lin.at("from"))
            self.end = _point(lin.raw("to"), lin.at("to"))
            lin.finish()
        elif spec.has("radial"):
            rad = spec.obj("radial")
            self.kind = "radial"
            self.center = _point(rad.raw("center"), rad.at("center"))
            self.radius = rad.num("radius", lo=0.001)
            rad.finish()
        else:
            raise ForgeError("gradient needs 'linear' or 'radial'", at, "run `cjforge ref image`")
        spec.finish()

    def render(self, x: np.ndarray, y: np.ndarray) -> np.ndarray:
        if self.kind == "solid":
            return np.broadcast_to(self.color, x.shape + (4,))
        if self.kind == "linear":
            d = self.end - self.start
            t = ((x - self.start[0]) * d[0] + (y - self.start[1]) * d[1]) / (d @ d + 1e-12)
        else:
            t = np.hypot(x - self.center[0], y - self.center[1]) / self.radius
        t = np.clip(t, 0.0, 1.0)
        return np.stack([np.interp(t, self.positions, self.colors[:, c]) for c in range(4)], axis=-1)
