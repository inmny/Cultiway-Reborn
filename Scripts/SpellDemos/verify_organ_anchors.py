#!/usr/bin/env python3
"""在独立游戏中核对器官逐帧出口、正式付费释放与延迟中的身体变化。"""
import json
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """复用独立配置与正式游戏连接，记录本次加载来源并运行检查。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connected = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/body-anchors/organ-outlets"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected,
               scope="原版实际器官帧及正式施法出口；不代表三维器官已经接合"))
    result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    report = json.loads((folder / "checks.json").read_text())
    if not report["passed"]:
        raise RuntimeError("器官出口检查未通过：" + str(folder / "checks.json"))


if __name__ == "__main__":
    main()
