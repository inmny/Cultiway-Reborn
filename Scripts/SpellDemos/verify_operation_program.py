#!/usr/bin/env python3
"""先保存旧内核基线，再用新编译表验证相同资料和数值执行耗时。"""
import argparse
import json
from pathlib import Path
from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record-baseline", action="store_true")
    args = parser.parse_args()
    folder = PROJECT / "artifacts/skill_rebuild/operation-program"
    folder.mkdir(parents=True, exist_ok=True)
    (folder / "mode.txt").write_text("record" if args.record_baseline else "verify")
    private_installation("cultiway", root=view.ROOT / "environment", worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    version, _ = version_stamp()
    write_json(folder / ("before-metadata.json" if args.record_baseline else "after-metadata.json"), dict(version=version, connection=connection))
    result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("运行资料重组改变了执行结果")


if __name__ == "__main__":
    main()
