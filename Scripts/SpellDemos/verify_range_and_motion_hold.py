#!/usr/bin/env python3
"""在同一独立游戏验证3.09收尾及受影响的需求、命名步骤和字段检查。"""
import json
from pathlib import Path
from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view
import verify_construction_pricing as pricing


def main():
    private_installation("cultiway", root=view.ROOT / "environment", worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    version, _ = version_stamp()
    folder = PROJECT / "artifacts/skill_rebuild/range-motion-hold"
    folder.mkdir(parents=True, exist_ok=True)
    write_json(folder / "metadata.json", dict(version=version, connection=connection))
    for name in ("verify_range_and_motion_hold", "verify_construction_pricing", "verify_magnitude_steps", "verify_operation_contracts", "verify_construction_requirements"):
        result = view.baseline.call("run", str(Path(__file__).with_name(name + ".script")), "120000")
        count = result["checks"]
        print(json.dumps(dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count)), ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError("动作维持、实际范围或相关回归未通过：" + name)
    pricing.cold_restart()


if __name__ == "__main__":
    main()
