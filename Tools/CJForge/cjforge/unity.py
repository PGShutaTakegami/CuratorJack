"""Unity side: ask the running editor (via uloop) to import freshly written assets right away."""

from __future__ import annotations

import shutil
import subprocess

from .config import TOOL_ROOT, Config

_CSX = """using UnityEditor;
foreach (var path in new[] {{ {paths} }})
    AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
AssetDatabase.Refresh();
return "imported {count}";
"""


def refresh(config: Config, asset_paths: list[str]) -> dict:
    """Returns {"refreshed": bool, "detail": str}. Unity also imports on its own when the editor regains focus."""
    uloop = shutil.which("uloop")
    if uloop is None:
        return {"refreshed": False, "detail": "uloop not found; Unity will import when the editor is focused"}
    paths = ", ".join('"' + p.replace('"', '\\"') + '"' for p in asset_paths)
    csx = TOOL_ROOT / "out" / "refresh.csx"
    csx.parent.mkdir(parents=True, exist_ok=True)
    csx.write_text(_CSX.format(paths=paths, count=len(asset_paths)), encoding="utf-8")
    try:
        result = subprocess.run([uloop, "execute-dynamic-code", "--code-file", str(csx)], cwd=config.unity_project,
                                capture_output=True, text=True, encoding="utf-8", timeout=180)
    except (OSError, subprocess.TimeoutExpired) as error:
        return {"refreshed": False, "detail": str(error)}
    ok = result.returncode == 0 and '"Success": true' in result.stdout
    return {"refreshed": ok, "detail": "ok" if ok else (result.stdout + result.stderr).strip()[-300:]}
