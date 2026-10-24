#!/usr/bin/env python3
"""实测世界材料输出作为后续转换、收束和导电输入。"""
import json
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view
import verify_world_receipts


def main():
    """同一私有游戏验证明确材料输入，再执行世界回执及相关回归。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/world-material-inputs"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.04明确世界材料输出输入；原部位操作输出继续接入"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    result = view.baseline.call("run", str(script), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("世界材料输入检查未通过：" + str(folder / "checks.json"))
    verify_world_receipts.main()


if __name__ == "__main__":
    main()
