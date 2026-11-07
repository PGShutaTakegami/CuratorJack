import json
from dataclasses import dataclass
from pathlib import Path

TOOL_ROOT = Path(__file__).resolve().parent.parent
RECIPES_DIR = TOOL_ROOT / "recipes"
KINDS = ("se", "bgm", "image")


@dataclass(frozen=True)
class Config:
    unity_project: Path
    outputs: dict
    preview_dir: Path
    ogg_quality: dict
    mp3_bitrate: int

    def output_dir(self, kind: str) -> Path:
        return self.unity_project / self.outputs[kind]

    def unity_relative(self, path: Path) -> str:
        return path.resolve().relative_to(self.unity_project).as_posix()


def load_config() -> Config:
    data = json.loads((TOOL_ROOT / "config.json").read_text(encoding="utf-8"))
    return Config(
        unity_project=(TOOL_ROOT / data["unity_project"]).resolve(),
        outputs=data["outputs"],
        preview_dir=(TOOL_ROOT / data["preview_dir"]).resolve(),
        ogg_quality=data["ogg_quality"],
        mp3_bitrate=data["mp3_bitrate"],
    )
