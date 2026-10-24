#!/usr/bin/env python3
"""在独立游戏核对构造缺项、真实目标和受影响的共同编译。"""
import json
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """运行字段缺项与实际受理专项，再检查操作、命名输出及正式资料。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/construction-requirements"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection))
    for name in ("verify_construction_requirements", "verify_construction_completion", "verify_output_bindings", "verify_operation_contracts", "verify_definition_contract"):
        script = Path(__file__).with_name(name + ".script")
        result = view.baseline.call("run", str(script), "120000")
        count = result["checks"]
        print(json.dumps(dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count)), ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError("构造缺项或相关回归未通过：" + name)


if __name__ == "__main__":
    main()
