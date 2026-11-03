#!/usr/bin/env python3
"""复用已准备的独立Worldfall安装，验证真实三维器官与共同接触。"""
import json
from pathlib import Path
import verify_view_switch as view
from common import PROJECT, version_stamp, write_json


def main():
    """不从下载目录重新安装，也不启用旧身体比例试稿。"""
    folder = PROJECT / "artifacts/skill_rebuild/spatial-organs"
    folder.mkdir(parents=True, exist_ok=True)
    connected = view.baseline.connect(True, validate_runtime=False)
    started = json.loads((view.baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for directory in ("Source", "Content", "GameResources")
           for p in (PROJECT / directory).rglob("*") if p.is_file()):
        raise RuntimeError("代码或资源晚于本进程，请先退出这一独立游戏再启动")
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    try:
        result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    result["checks"] = json.loads((folder / "checks.json").read_text())["checks"]
    write_json(folder / "result.json", result)
    print(json.dumps(dict(passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
