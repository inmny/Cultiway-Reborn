#!/usr/bin/env python3
"""验证命名步骤参与真实数值改造，包含正式支付、接触和保存。"""
import json
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """在独立游戏运行步骤改造和共同构造回归，不把本批计为完整成本规则验收。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/magnitude-steps"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.09前置：原部位与命名步骤共同改造，统一成本规则继续实现"))
    for name in ("verify_magnitude_steps", "verify_output_bindings", "verify_operation_contracts", "verify_derivation"):
        result = view.baseline.call("run", str(Path(__file__).with_name(name + ".script")), "120000")
        count = result["checks"]
        print(json.dumps(dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count)), ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError("数值改造或相关回归未通过：" + name)


if __name__ == "__main__":
    main()
