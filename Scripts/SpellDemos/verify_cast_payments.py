#!/usr/bin/env python3
"""验证启动支付的提交、撤销、实际本金及已有来源回归。"""
import json
from pathlib import Path
from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/cast-payments"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection))
    result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("启动支付检查未通过")
    reports = []
    for name, previous in (
        ("verify_recovery_resources", "recovery"),
        ("verify_native_outputs_world", "native-outputs/world"),
        ("verify_utility_migration", "utility-migration"),
    ):
        target = folder / "regressions" / name
        target.mkdir(parents=True, exist_ok=True)
        source = Path(__file__).with_name(name + ".script")
        script = target / "input.script"
        script.write_text(source.read_text().replace("artifacts/skill_rebuild/" + previous,
                          target.relative_to(PROJECT).as_posix()))
        report = view.baseline.call("run", str(script), "120000")
        write_json(target / "result.json", report)
        count = report["checks"]
        summary = dict(name=name, passed=report["passed"], checks=count if isinstance(count, int) else len(count))
        reports.append(summary)
        write_json(folder / "regressions.json", reports)
        print(json.dumps(summary, ensure_ascii=False), flush=True)
        if not report["passed"]:
            raise RuntimeError(name + " 未通过")


if __name__ == "__main__":
    main()
