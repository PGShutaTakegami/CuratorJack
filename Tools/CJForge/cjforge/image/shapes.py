"""Shapes as signed distance fields (pixels, negative = inside), with transforms, repeats and boolean ops."""

from __future__ import annotations

import numpy as np

from ..errors import ForgeError
from ..spec import Spec, check_num
from .paint import _point

SDF_SHAPES = ("circle", "ellipse", "ring", "rect", "polygon", "line", "polyline", "star", "arc")


class Transform:
    def __init__(self, spec: Spec, canvas_center: np.ndarray):
        self.rotate = spec.num("rotate", 0.0, -36000, 36000)
        self.origin = _point(spec.raw("origin"), spec.at("origin")) if spec.has("origin") else canvas_center
        self.offset = _point(spec.raw("offset"), spec.at("offset")) if spec.has("offset") else np.zeros(2)
        self.scale = spec.num("scale", 1.0, 0.001, 1000)

    @property
    def identity(self) -> bool:
        return self.rotate == 0 and self.scale == 1 and not self.offset.any()


def _inverse(x, y, origin, rotate_deg, offset, scale):
    px = (x - origin[0] - offset[0]) / scale
    py = (y - origin[1] - offset[1]) / scale
    if rotate_deg:
        a = np.radians(-rotate_deg)
        c, s = np.cos(a), np.sin(a)
        px, py = px * c - py * s, px * s + py * c
    return px + origin[0], py + origin[1]


class Shape:
    """Primitive SDF shape + optional union/subtract/intersect children, transform and repeat."""

    def __init__(self, spec: Spec, canvas_center: np.ndarray):
        self.kind = spec.str("shape", choices=SDF_SHAPES)
        self.params = self._parse(spec)
        self.transform = Transform(spec, canvas_center)
        rep = spec.obj("repeat", None, "image")
        self.repeat = None
        if rep is not None:
            self.repeat = (rep.int("count", 2, 1, 360), rep.num("rotate", 0.0, -3600, 3600),
                           _point(rep.raw("offset"), rep.at("offset")) if rep.has("offset") else np.zeros(2),
                           rep.num("scale", 1.0, 0.01, 100))
            rep.finish()
        self.ops = []
        for op in ("union", "subtract", "intersect"):
            for child in spec.objs(op, [], "image"):
                shape = Shape(child, canvas_center)
                child.finish()
                self.ops.append((op, shape))

    def _parse(self, spec: Spec) -> dict:
        k = self.kind
        p: dict = {}
        if k in ("circle", "ring", "arc", "star", "ellipse"):
            p["center"] = _point(spec.raw("center"), spec.at("center"))
        if k in ("circle", "ring", "arc"):
            p["radius"] = spec.num("radius", lo=0)
        if k in ("ring", "arc"):
            p["width"] = spec.num("width", lo=0.01)
        if k == "arc":
            p["start"] = spec.num("start", 0.0)
            p["end"] = spec.num("end", 90.0)
        if k == "ellipse":
            radii = spec.list("radii")
            p["radii"] = _point(radii, spec.at("radii"))
        if k == "rect":
            rect = spec.list("rect")
            if len(rect) != 4:
                raise ForgeError("[x, y, width, height] expected", spec.at("rect"))
            p["rect"] = np.array([check_num(v, f"{spec.at('rect')}[{i}]") for i, v in enumerate(rect)])
            p["radius"] = spec.num("radius", 0.0, 0)
        if k in ("polygon", "polyline"):
            pts = spec.list("points")
            if len(pts) < (3 if k == "polygon" else 2):
                raise ForgeError("not enough points", spec.at("points"))
            p["points"] = np.stack([_point(v, f"{spec.at('points')}[{i}]") for i, v in enumerate(pts)])
        if k in ("line", "polyline"):
            p["width"] = spec.num("width", 2.0, 0.01)
            p["cap"] = spec.str("cap", "round", ("round", "butt", "square"))
        if k == "line":
            p["from"] = _point(spec.raw("from"), spec.at("from"))
            p["to"] = _point(spec.raw("to"), spec.at("to"))
        if k == "star":
            p["points_count"] = spec.int("points", 5, 2, 64)
            p["outer"] = spec.num("outer", lo=0)
            p["inner"] = spec.num("inner", lo=0)
            p["angle"] = spec.num("angle", -90.0)
        return p

    def sdf(self, x: np.ndarray, y: np.ndarray) -> np.ndarray:
        count, rot, off, sc = self.repeat or (1, 0.0, np.zeros(2), 1.0)
        t = self.transform
        d = None
        for i in range(count):
            scale = t.scale * sc ** i
            px, py = _inverse(x, y, t.origin, t.rotate + rot * i, t.offset + off * i, scale)
            di = self._primitive(px, py) * scale
            d = di if d is None else np.minimum(d, di)
        for op, shape in self.ops:
            other = shape.sdf(x, y)
            if op == "union":
                d = np.minimum(d, other)
            elif op == "subtract":
                d = np.maximum(d, -other)
            else:
                d = np.maximum(d, other)
        return d

    def _primitive(self, x, y):
        p, k = self.params, self.kind
        if k == "circle":
            return np.hypot(x - p["center"][0], y - p["center"][1]) - p["radius"]
        if k == "ring":
            return np.abs(np.hypot(x - p["center"][0], y - p["center"][1]) - p["radius"]) - p["width"] / 2
        if k == "ellipse":
            rx, ry = p["radii"]
            return (np.hypot((x - p["center"][0]) / rx, (y - p["center"][1]) / ry) - 1.0) * min(rx, ry)
        if k == "rect":
            rx, ry, w, h = p["rect"]
            r = min(p["radius"], w / 2, h / 2)
            qx = np.abs(x - (rx + w / 2)) - (w / 2 - r)
            qy = np.abs(y - (ry + h / 2)) - (h / 2 - r)
            outside = np.hypot(np.maximum(qx, 0), np.maximum(qy, 0))
            return outside + np.minimum(np.maximum(qx, qy), 0) - r
        if k == "polygon":
            return polygon_sdf(x, y, p["points"])
        if k == "star":
            n = p["points_count"]
            angles = np.radians(p["angle"]) + np.arange(2 * n) * np.pi / n
            radii = np.where(np.arange(2 * n) % 2 == 0, p["outer"], p["inner"])
            pts = np.stack([p["center"][0] + radii * np.cos(angles), p["center"][1] + radii * np.sin(angles)], axis=1)
            return polygon_sdf(x, y, pts)
        if k == "line":
            return segment_sdf(x, y, p["from"], p["to"], p["width"], p["cap"])
        if k == "polyline":
            pts = p["points"]
            return np.minimum.reduce([segment_sdf(x, y, pts[i], pts[i + 1], p["width"], p["cap"]) for i in range(len(pts) - 1)])
        if k == "arc":
            return arc_sdf(x, y, p["center"], p["radius"], p["width"], p["start"], p["end"])
        raise ForgeError(f"unknown shape '{k}'")


def polygon_sdf(x, y, pts: np.ndarray) -> np.ndarray:
    d2 = np.full(x.shape, np.inf)
    inside = np.zeros(x.shape, dtype=bool)
    for i in range(len(pts)):
        ax, ay = pts[i - 1]
        bx, by = pts[i]
        ex, ey = bx - ax, by - ay
        wx, wy = x - ax, y - ay
        t = np.clip((wx * ex + wy * ey) / (ex * ex + ey * ey + 1e-12), 0.0, 1.0)
        d2 = np.minimum(d2, (wx - ex * t) ** 2 + (wy - ey * t) ** 2)
        crosses = ((ay > y) != (by > y)) & (x < (bx - ax) * (y - ay) / (by - ay + 1e-12) + ax)
        inside ^= crosses
    d = np.sqrt(d2)
    return np.where(inside, -d, d)


def segment_sdf(x, y, a, b, width, cap) -> np.ndarray:
    ex, ey = b[0] - a[0], b[1] - a[1]
    length = np.hypot(ex, ey) + 1e-12
    ux, uy = ex / length, ey / length
    u = (x - a[0]) * ux + (y - a[1]) * uy
    v = -(x - a[0]) * uy + (y - a[1]) * ux
    if cap == "round":
        t = np.clip(u, 0.0, length)
        return np.hypot(u - t, v) - width / 2
    extend = width / 2 if cap == "square" else 0.0
    qx = np.abs(u - length / 2) - (length / 2 + extend)
    qy = np.abs(v) - width / 2
    return np.hypot(np.maximum(qx, 0), np.maximum(qy, 0)) + np.minimum(np.maximum(qx, qy), 0)


def arc_sdf(x, y, center, radius, width, start, end) -> np.ndarray:
    """Angles in degrees, 0 = +x (right), increasing clockwise (screen y is down). Round caps."""
    dx, dy = x - center[0], y - center[1]
    angle = np.degrees(np.arctan2(dy, dx)) % 360.0
    span = (end - start) % 360.0 or 360.0
    inside = ((angle - start) % 360.0) <= span
    ring = np.abs(np.hypot(dx, dy) - radius) - width / 2
    caps = []
    for a in (start, start + span):
        r = np.radians(a)
        caps.append(np.hypot(x - (center[0] + radius * np.cos(r)), y - (center[1] + radius * np.sin(r))) - width / 2)
    return np.where(inside, ring, np.minimum(caps[0], caps[1]))
