#!/usr/bin/env python3
"""验证原部位操作与共同步骤的真实输出互通。"""
import json
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view
import verify_world_material_inputs


def main():
    """同一私有游戏验证数值、真实修士和原有相关回归。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/native-outputs"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.04原部位操作输出及共同步骤互通，构造部分第五版"))
    for suffix in ("", "_world"):
        script = Path(__file__).with_name(Path(__file__).stem + suffix + ".script")
        shutil.copyfile(script, folder / ("input" + suffix + ".script"))
        result = view.baseline.call("run", str(script), "120000")
        print(json.dumps(dict(suffix=suffix, result=result), ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError("原操作输出检查未通过：" + str(folder))
    verify_world_material_inputs.main()


if __name__ == "__main__":
    main()
