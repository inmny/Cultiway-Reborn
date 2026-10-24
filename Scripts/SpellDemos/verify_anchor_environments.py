#!/usr/bin/env python3
"""补核原版实际环境、施法者自身第一人称与器物支持边界。"""
import json
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """在同一独立游戏中运行剩余场景，保存实际来源及结果。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connected = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/body-anchors/environments"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected,
               scope="原版真实环境、本人第一人称及器物支持边界；不代表全部身体制作完成"))
    result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not json.loads((folder / "checks.json").read_text())["passed"]:
        raise RuntimeError("挂点环境检查未通过：" + str(folder / "checks.json"))


if __name__ == "__main__":
    main()
