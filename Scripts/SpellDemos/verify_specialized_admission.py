#!/usr/bin/env python3
"""在独立游戏中验证专属能力支付，并检查直接受影响的共同支付与序列。"""
import argparse
import json
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """复用同一实际游戏；可以只重跑指定的验证脚本。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("scripts", nargs="*", default=[
        "verify_specialized_payments", "verify_yuanshen_admission", "verify_source_sessions",
        "verify_cast_payments", "verify_sequence_admission"])
    args = parser.parse_args()
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connected = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/specialized-admission"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected))
    reports = []
    for name in args.scripts:
        source = Path(__file__).with_name(name + ".script")
        result = view.baseline.call("run", str(source), "180000")
        write_json(folder / (name + ".json"), result)
        count = result["checks"]
        summary = dict(name=name, passed=result["passed"],
                       checks=count if isinstance(count, int) else len(count))
        reports.append(summary)
        write_json(folder / "reports.json", reports)
        print(json.dumps(summary, ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError(name + " 未通过")


if __name__ == "__main__":
    main()
