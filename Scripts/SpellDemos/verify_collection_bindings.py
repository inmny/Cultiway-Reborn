#!/usr/bin/env python3
"""验证真实分件集合、逐片流散、数量守恒及全部等待。"""
import json
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view
import verify_output_bindings


def main():
    """在同一私有游戏完成集合专项与原单项、操作和正式资料回归。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/collection-bindings"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.04真实分件集合及逐片流散；世界确认与既有部位操作输出继续实现"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    result = view.baseline.call("run", str(script), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("集合绑定检查未通过：" + str(folder / "checks.json"))
    verify_output_bindings.main()


if __name__ == "__main__":
    main()
