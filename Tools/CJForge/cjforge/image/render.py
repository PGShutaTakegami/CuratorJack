"""Image recipe: layers of shapes / text / images composited (premultiplied alpha) onto a transparent or solid canvas."""

from __future__ import annotations

from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

from ..errors import ForgeError
from ..spec import Spec
from .color import Palette, parse_color
from .paint import Paint, _point
from .shapes import SDF_SHAPES, Shape

FONT_ALIASES = {
    "serif": "Assets/TextMesh Pro/Fonts/CuratorJackSerif-Bold.ttf",
    "mincho": "Assets/TextMesh Pro/Fonts/NewClear-mincho.ttf",
    "sans": "Assets/TextMesh Pro/Fonts/LiberationSans.ttf",
}
BLENDS = ("normal", "erase", "add")
STROKE_ALIGN = ("center", "inside", "outside")
TEXT_SS = 4


def _coverage(d: np.ndarray, threshold: float = 0.0) -> np.ndarray:
    return np.clip(threshold - d + 0.5, 0.0, 1.0)


def _premultiply(rgba: np.ndarray, coverage: np.ndarray) -> np.ndarray:
    alpha = rgba[..., 3] * coverage
    return np.concatenate([rgba[..., :3] * alpha[..., None], alpha[..., None]], axis=-1)


def _over(dst: np.ndarray, src: np.ndarray) -> np.ndarray:
    return src + dst * (1.0 - src[..., 3:4])


def _blur(alpha: np.ndarray, radius: float) -> np.ndarray:
    if radius <= 0:
        return alpha
    img = Image.fromarray(np.clip(alpha * 255.0, 0, 255).astype(np.uint8), "L")
    return np.asarray(img.filter(ImageFilter.GaussianBlur(radius)), dtype=np.float64) / 255.0


def _shift(alpha: np.ndarray, dx: int, dy: int) -> np.ndarray:
    out = np.zeros_like(alpha)
    h, w = alpha.shape
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = alpha[ys, xs]
    return out


class Canvas:
    def __init__(self, width: int, height: int, background: np.ndarray):
        self.width, self.height = width, height
        ys, xs = np.mgrid[0:height, 0:width]
        self.x = xs + 0.5
        self.y = ys + 0.5
        self.center = np.array([width / 2, height / 2])
        bg = _premultiply(np.broadcast_to(background, (height, width, 4)), np.ones((height, width)))
        self.pixels = bg.copy()
        self.transparent_bg = background[3] < 1.0


class Layer:
    def __init__(self, spec: Spec, canvas: Canvas, palette: Palette, recipe_dir: Path, project: Path):
        self.spec_path = spec.path
        kind = spec.str("shape", choices=SDF_SHAPES + ("text", "image"))
        self.kind = kind
        self.opacity = spec.num("opacity", 1.0, 0, 1)
        self.blend = spec.str("blend", "normal", BLENDS)
        self.fill = Paint(spec.raw("fill"), spec.at("fill"), palette) if spec.has("fill") else None
        stroke = spec.obj("stroke", None, "image")
        self.stroke = None
        if stroke is not None:
            self.stroke = (Paint(stroke.raw("color"), stroke.at("color"), palette), stroke.num("width", 2.0, 0.01),
                           stroke.str("align", "center" if kind != "text" else "outside", STROKE_ALIGN))
            stroke.finish()
        self.shadow = self._effect(spec, "shadow", palette, default_offset=(3, 3))
        self.glow = self._effect(spec, "glow", palette, default_offset=(0, 0))

        if kind in SDF_SHAPES:
            self.shape = Shape(spec, canvas.center)
            if self.fill is None and self.stroke is None:
                raise ForgeError("a shape needs 'fill' and/or 'stroke'", spec.path)
        elif kind == "text":
            self.text = spec.str("text")
            font = spec.str("font", "serif")
            path = project / FONT_ALIASES[font] if font in FONT_ALIASES else (recipe_dir / font)
            if not path.exists():
                raise ForgeError(f"font not found: {path}", spec.at("font"), f"aliases: {list(FONT_ALIASES)}")
            self.font_path = path
            self.size = spec.num("size", 32, 1, 2000)
            self.pos = _point(spec.raw("pos"), spec.at("pos"))
            self.anchor = spec.str("anchor", "mm")
            self.rotate = spec.num("rotate", 0.0)
            self.spacing = spec.num("line_spacing", 4.0)
            if self.fill is None:
                self.fill = Paint("white", spec.at("fill"), palette)
        else:
            self.image_path = recipe_dir / spec.str("path")
            if not self.image_path.exists():
                raise ForgeError(f"image not found: {self.image_path}", spec.at("path"))
            rect = spec.list("rect")
            if len(rect) != 4:
                raise ForgeError("[x, y, width, height] expected", spec.at("rect"))
            self.rect = [int(round(v)) for v in rect]
        spec.finish()

    @staticmethod
    def _effect(spec: Spec, key: str, palette: Palette, default_offset):
        eff = spec.obj(key, None, "image")
        if eff is None:
            return None
        color = parse_color(eff.raw("color", "#000000A0" if key == "shadow" else "#FFFFFFC0"), eff.at("color"), palette)
        offset = _point(eff.raw("offset", list(default_offset)), eff.at("offset"))
        blur = eff.num("blur", 3.0 if key == "shadow" else 6.0, 0, 200)
        strength = eff.num("strength", 1.0, 0, 10)
        eff.finish()
        return color, (int(round(offset[0])), int(round(offset[1]))), blur, strength

    def render(self, canvas: Canvas) -> np.ndarray:
        if self.kind == "image":
            out = self._render_image(canvas)
        elif self.kind == "text":
            out = self._render_text(canvas)
        else:
            out = self._render_shape(canvas)
        alpha = out[..., 3]
        under = np.zeros_like(out)
        for effect in (self.shadow, self.glow):
            if effect is None:
                continue
            color, (dx, dy), blur, strength = effect
            a = np.clip(_blur(_shift(alpha, dx, dy), blur) * strength, 0.0, 1.0)
            under = _over(under, _premultiply(np.broadcast_to(color, out.shape), a))
        return _over(under, out) * self.opacity

    def _render_shape(self, canvas: Canvas) -> np.ndarray:
        d = self.shape.sdf(canvas.x, canvas.y)
        out = np.zeros((canvas.height, canvas.width, 4))
        if self.fill is not None:
            out = _premultiply(self.fill.render(canvas.x, canvas.y), _coverage(d))
        if self.stroke is not None:
            paint, width, align = self.stroke
            lo, hi = {"center": (-width / 2, width / 2), "inside": (-width, 0.0), "outside": (0.0, width)}[align]
            band = np.clip(_coverage(d, hi) - _coverage(d, lo), 0.0, 1.0)
            out = _over(out, _premultiply(paint.render(canvas.x, canvas.y), band))
        return out

    def _text_mask(self, canvas: Canvas, stroke_width: int) -> np.ndarray:
        size = (canvas.width * TEXT_SS, canvas.height * TEXT_SS)
        img = Image.new("L", size, 0)
        font = ImageFont.truetype(str(self.font_path), int(round(self.size * TEXT_SS)))
        ImageDraw.Draw(img).multiline_text(tuple(self.pos * TEXT_SS), self.text, fill=255, font=font, anchor=self.anchor,
                                           spacing=self.spacing * TEXT_SS, stroke_width=stroke_width * TEXT_SS,
                                           stroke_fill=255, align="center")
        if self.rotate:
            img = img.rotate(-self.rotate, resample=Image.BICUBIC, center=tuple(self.pos * TEXT_SS))
        return np.asarray(img.reduce(TEXT_SS), dtype=np.float64) / 255.0

    def _render_text(self, canvas: Canvas) -> np.ndarray:
        fill_mask = self._text_mask(canvas, 0)
        out = np.zeros((canvas.height, canvas.width, 4))
        if self.stroke is not None:
            paint, width, _ = self.stroke
            outline = self._text_mask(canvas, max(1, int(round(width))))
            out = _premultiply(paint.render(canvas.x, canvas.y), outline)
        return _over(out, _premultiply(self.fill.render(canvas.x, canvas.y), fill_mask))

    def _render_image(self, canvas: Canvas) -> np.ndarray:
        x, y, w, h = self.rect
        src = Image.open(self.image_path).convert("RGBA").resize((max(1, w), max(1, h)), Image.LANCZOS)
        layer = Image.new("RGBA", (canvas.width, canvas.height), (0, 0, 0, 0))
        layer.paste(src, (x, y))
        rgba = np.asarray(layer, dtype=np.float64) / 255.0
        return _premultiply(rgba, np.ones(rgba.shape[:2]))


def render_image(spec: Spec, recipe_dir: Path, project: Path) -> tuple[Image.Image, dict]:
    size = spec.list("size")
    if len(size) != 2 or not all(isinstance(v, int) and 1 <= v <= 4096 for v in size):
        raise ForgeError("[width, height] integers 1..4096 expected", spec.at("size"))
    palette = Palette(spec.raw("palette", None), spec.at("palette"))
    background = parse_color(spec.raw("background", "transparent"), spec.at("background"), palette)
    canvas = Canvas(size[0], size[1], background)
    layers = [Layer(s, canvas, palette, recipe_dir, project) for s in spec.objs("layers", topic="image")]
    if not layers:
        raise ForgeError("at least one layer is required", spec.at("layers"))

    for layer in layers:
        src = layer.render(canvas)
        if layer.blend == "normal":
            canvas.pixels = _over(canvas.pixels, src)
        elif layer.blend == "erase":
            canvas.pixels = canvas.pixels * (1.0 - src[..., 3:4])
        else:
            canvas.pixels = np.clip(canvas.pixels + src, 0.0, 1.0)

    alpha = np.clip(canvas.pixels[..., 3], 0.0, 1.0)
    rgb = np.where(alpha[..., None] > 1e-6, canvas.pixels[..., :3] / np.maximum(alpha[..., None], 1e-6), 0.0)
    rgba = np.concatenate([np.clip(rgb, 0, 1), alpha[..., None]], axis=-1)
    image = Image.fromarray(np.round(rgba * 255.0).astype(np.uint8), "RGBA")
    return image, image_metrics(alpha, canvas.transparent_bg)


def image_metrics(alpha: np.ndarray, transparent_bg: bool) -> dict:
    h, w = alpha.shape
    result = {"size": [w, h], "transparent": bool(transparent_bg)}
    visible = alpha > 0.01
    if transparent_bg and visible.any():
        ys, xs = np.nonzero(visible)
        result["bbox"] = [int(xs.min()), int(ys.min()), int(xs.max()) + 1, int(ys.max()) + 1]
        edges = [name for name, edge in (("top", visible[0]), ("bottom", visible[-1]), ("left", visible[:, 0]), ("right", visible[:, -1])) if edge.any()]
        if edges:
            result["touches_edge"] = edges
        result["coverage"] = round(float(alpha.mean()), 3)
    return result
