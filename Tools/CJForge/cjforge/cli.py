"""Command line. Every recipe result is printed as one JSON line (ok / error with location)."""

from __future__ import annotations

import argparse
import json
import sys
import traceback
from pathlib import Path

from .config import KINDS, RECIPES_DIR, TOOL_ROOT, load_config
from .errors import ForgeError
from .forge import Forge, display
from .reference import topic_text

PACKAGE_DIR = Path(__file__).resolve().parent

TEMPLATES = {
    "se": {"kind": "se", "layers": [{"osc": "sine", "freq": [880, 1320], "duration": 0.12}], "effects": [], "master": {"peak_db": -1}},
    "bgm": {"kind": "bgm", "tempo": 96, "bars": 4, "loop": True,
            "instruments": {"keys": {"preset": "epiano"}, "bass": {"preset": "upright_bass"}},
            "tracks": [{"instrument": "keys", "chords": "Cm7 - - - | Fm7 - - - | Bb7 - - - | Ebmaj7 - - -", "step": "1/4"},
                       {"instrument": "bass", "notes": "C2 . G1 . | F1 . C2 . | Bb1 . F1 . | Eb2 . Bb1 .", "step": "1/4"},
                       {"drum": "brush", "pattern": "x.x.", "step": "1/8", "gain_db": -10}],
            "effects": [{"type": "reverb", "mix": 0.2}]},
    "image": {"kind": "image", "size": [128, 128], "background": "transparent",
              "layers": [{"shape": "circle", "center": [64, 64], "radius": 50, "fill": "#DEB86A", "stroke": {"color": "#26180E", "width": 4}}]},
}


def emit(data: dict) -> None:
    sys.stdout.write(json.dumps(data, ensure_ascii=False) + "\n")
    sys.stdout.flush()


def resolve(arg: str) -> list[Path]:
    candidates = [Path(arg), TOOL_ROOT / arg, RECIPES_DIR / arg]
    for base in candidates:
        for path in (base, base.with_suffix(".json")):
            if path.is_file():
                return [path.resolve()]
            if path.is_dir():
                return sorted(p.resolve() for p in path.rglob("*.json"))
    matches = sorted(RECIPES_DIR.glob(arg if arg.endswith(".json") else arg + ".json"))
    if matches:
        return [m.resolve() for m in matches]
    raise ForgeError(f"recipe not found: {arg}", hint="pass a path, 'se/name', a folder like 'se', or a glob like 'se/ui_*'")


def collect(args) -> list[Path]:
    if getattr(args, "all", False) or not args.recipes:
        return sorted(p.resolve() for p in RECIPES_DIR.rglob("*.json"))
    paths: list[Path] = []
    for arg in args.recipes:
        for p in resolve(arg):
            if p not in paths:
                paths.append(p)
    return paths


def main(argv: list[str]) -> int:
    parser = argparse.ArgumentParser(prog="cjforge", description="Recipe driven SE/BGM (ogg/mp3) and image (png) generator")
    sub = parser.add_subparsers(dest="command", required=True)
    for name, help_text in (("render", "generate and place into Unity"), ("preview", "generate into Tools/CJForge/out/preview"),
                            ("check", "validate and render in memory only")):
        p = sub.add_parser(name, help=help_text)
        p.add_argument("recipes", nargs="*", help="recipe paths / 'se/name' / folder / glob (none = all)")
        p.add_argument("--all", action="store_true", help="all recipes (same as no recipe argument)")
        if name != "check":
            p.add_argument("--force", action="store_true", help="ignore cache")
        if name == "render":
            p.add_argument("--refresh", action="store_true", help="make the running Unity editor import now (uloop)")
    p = sub.add_parser("ref", help="parameter reference")
    p.add_argument("topic", nargs="?", default="overview")
    p = sub.add_parser("new", help="create a recipe from a template")
    p.add_argument("kind", choices=KINDS)
    p.add_argument("name")
    sub.add_parser("list", help="list recipes")
    args = parser.parse_args(argv)

    if args.command == "ref":
        print(topic_text(args.topic))
        return 0
    if args.command == "list":
        for p in sorted(RECIPES_DIR.rglob("*.json")):
            print(p.relative_to(RECIPES_DIR).as_posix())
        return 0
    if args.command == "new":
        target = RECIPES_DIR / args.kind / f"{args.name}.json"
        if target.exists():
            emit({"ok": False, "error": "already exists", "recipe": display(target)})
            return 1
        target.parent.mkdir(parents=True, exist_ok=True)
        target.write_text(json.dumps(TEMPLATES[args.kind], ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
        emit({"ok": True, "recipe": display(target)})
        return 0

    config = load_config()
    forge = Forge(config, force=getattr(args, "force", False))
    try:
        paths = collect(args)
    except ForgeError as error:
        emit({"ok": False, **error.to_dict()})
        return 1

    failed = 0
    placed: list[str] = []
    for path in paths:
        try:
            if args.command == "check":
                emit(forge.check(path))
                continue
            result = forge.run(path, preview=args.command == "preview")
            emit(result)
            if args.command == "render" and not result.get("skipped"):
                placed.append(result["out"])
        except ForgeError as error:
            failed += 1
            emit({"ok": False, "recipe": display(path), **error.to_dict()})
        except Exception as error:  # report tool bugs compactly instead of a long traceback
            failed += 1
            frames = traceback.extract_tb(error.__traceback__)
            own = [f for f in frames if Path(f.filename).resolve().is_relative_to(PACKAGE_DIR)]
            frame = (own or frames)[-1]
            emit({"ok": False, "recipe": display(path), "error": f"internal: {type(error).__name__}: {error}",
                  "where": f"{Path(frame.filename).name}:{frame.lineno}"})
    forge.save_cache()

    if args.command == "render" and getattr(args, "refresh", False) and placed:
        from .unity import refresh

        emit({"refresh": refresh(config, placed)})
    return 1 if failed else 0
