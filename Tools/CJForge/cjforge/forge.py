"""Pipeline: recipe file -> render -> write (Unity or preview) -> compact result dict."""

from __future__ import annotations

import functools
import hashlib
import json
import re
from pathlib import Path

import numpy as np

from . import VERSION
from .config import KINDS, TOOL_ROOT, Config
from .errors import ForgeError
from .spec import Spec

_NAME = re.compile(r"^[A-Za-z0-9_\-]+(/[A-Za-z0-9_\-]+)*$")


def load_recipe(path: Path) -> tuple[Spec, str]:
    text = path.read_text(encoding="utf-8")
    try:
        data = json.loads(text)
    except json.JSONDecodeError as error:
        raise ForgeError(f"invalid JSON: {error.msg}", f"line {error.lineno} col {error.colno}") from error
    return Spec(data, "$"), text


def recipe_name(spec: Spec, path: Path) -> str:
    name = spec.str("name", path.stem)
    if not _NAME.match(name):
        raise ForgeError("name may contain letters, digits, '_', '-' and '/' (subfolders)", spec.at("name"))
    return name


@functools.cache
def _code_fingerprint() -> str:
    digest = hashlib.sha256(VERSION.encode("utf-8"))
    for source in sorted(Path(__file__).resolve().parent.rglob("*.py")):
        digest.update(source.read_bytes())
    return digest.hexdigest()


def _remove_other_formats(target: Path, extensions: tuple[str, ...]) -> list[Path]:
    """When a recipe switches format (e.g. loop on/off), delete the stale file and its Unity .meta."""
    removed = []
    for extension in extensions:
        stale = target.with_suffix("." + extension)
        if stale != target and stale.is_file():
            stale.unlink()
            stale.with_name(stale.name + ".meta").unlink(missing_ok=True)
            removed.append(stale)
    return removed


def display(path: Path) -> str:
    try:
        return path.resolve().relative_to(TOOL_ROOT).as_posix()
    except ValueError:
        return str(path)


class Forge:
    def __init__(self, config: Config, force: bool = False):
        self.config = config
        self.force = force
        self.cache_path = TOOL_ROOT / "out" / "cache.json"
        self.cache = json.loads(self.cache_path.read_text(encoding="utf-8")) if self.cache_path.exists() else {}

    def save_cache(self) -> None:
        self.cache_path.parent.mkdir(parents=True, exist_ok=True)
        self.cache_path.write_text(json.dumps(self.cache, indent=1), encoding="utf-8")

    def check(self, path: Path) -> dict:
        spec, _ = load_recipe(path)
        kind = spec.str("kind", choices=KINDS)
        self._render(spec, kind, path)
        return {"ok": True, "recipe": display(path), "kind": kind}

    def run(self, path: Path, preview: bool) -> dict:
        spec, text = load_recipe(path)
        kind = spec.str("kind", choices=KINDS)
        name = recipe_name(spec, path)
        folder = (self.config.preview_dir / kind) if preview else self.config.output_dir(kind)
        settings = json.dumps([self.config.outputs, self.config.ogg_quality, self.config.mp3_bitrate])
        digest = hashlib.sha256((_code_fingerprint() + text + settings).encode("utf-8")).hexdigest()[:16]
        key = display(path) + (" [preview]" if preview else "")
        cached = self.cache.get(key)
        if not self.force and cached and cached.get("hash") == digest and Path(cached.get("target", "")).is_file():
            return dict(cached["result"], skipped=True)

        rendered, info = self._render(spec, kind, path)
        result = {"ok": True, "recipe": display(path), "kind": kind}
        if kind == "image":
            target = folder / f"{name}.png"
            target.parent.mkdir(parents=True, exist_ok=True)
            rendered.save(target, optimize=True)
            result.update(info)
        else:
            from .audio.analysis import metrics, waveform_png
            from .audio.encode import AUDIO_EXTENSIONS, audio_extension, write_mp3, write_ogg

            signal, sr, loop = rendered
            target = folder / f"{name}.{audio_extension(kind, loop)}"
            if target.suffix == ".ogg":
                write_ogg(target, signal, sr, self.config.ogg_quality[kind])
            else:
                write_mp3(target, signal, sr, self.config.mp3_bitrate)
            removed = _remove_other_formats(target, AUDIO_EXTENSIONS)
            if removed:
                result["removed"] = [p.name for p in removed]
            result.update(metrics(signal, sr, loop))
            wave = self.config.preview_dir / kind / f"{name}_wave.png"
            waveform_png(wave, signal, sr, name)
            result["wave"] = display(wave)
        result["out"] = display(target) if preview else self.config.unity_relative(target)
        self.cache[key] = {"hash": digest, "target": str(target), "result": result}
        return result

    def _render(self, spec: Spec, kind: str, path: Path):
        spec.str("name", None)
        seed = spec.int("seed", 1, 0, 2**31 - 1)
        rng = np.random.default_rng(seed)
        if kind == "image":
            from .image.render import render_image

            image, info = render_image(spec, path.parent, self.config.unity_project)
            spec.finish()
            return image, info

        sr = spec.int("sample_rate", 44100, 8000, 96000)
        if kind == "se":
            from .audio.se import render_se

            signal, _ = render_se(spec, sr, rng)
            loop = False
        else:
            from .audio.bgm import render_bgm

            signal, _, loop = render_bgm(spec, sr, rng)
        spec.finish()
        return (signal, sr, loop), None
