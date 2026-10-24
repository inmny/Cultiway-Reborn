#!/usr/bin/env python3
"""通过实际修士验证世界操作确认，再执行相关构造回归。"""
import json
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view
import verify_collection_bindings


def main():
    """使用私有游戏核对留置、转换、入账及逐波导电的真实回执。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/world-receipts"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.04世界主线程确认；精确材料输入和既有部位操作输出继续实现"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    result = view.baseline.call("run", str(script), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("世界回执检查未通过：" + str(folder / "checks.json"))
    verify_collection_bindings.main()


if __name__ == "__main__":
    main()
