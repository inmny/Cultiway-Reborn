#!/usr/bin/env python3
"""在已有独立原版游戏验证实际器官动作、接触、保存和编辑。"""
import json
from pathlib import Path
import baseline_scenarios as baseline
from common import PROJECT, write_json, version_stamp


def main():
    """复用原版测试安装，不修改玩家配置或打开三维试稿。"""
    folder = PROJECT / "artifacts/skill_rebuild/body-organs"
    folder.mkdir(parents=True, exist_ok=True)
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for d in ("Source", "Content", "GameResources")
           for p in (PROJECT / d).rglob("*") if p.is_file()):
        raise RuntimeError("代码或资源晚于本进程，请先退出这一独立游戏再启动")
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    try:
        result = baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    write_json(folder / "result.json", result)
    print(json.dumps(dict(passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
