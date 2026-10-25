#!/usr/bin/env python3
"""验证真实序列受理与直接受影响的构造、功能法术回归。"""
import json
from pathlib import Path
from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    private_installation("cultiway", root=view.ROOT / "environment", worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connected = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/sequence-admission"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected))
    reports = []
    for name, scope in (("verify_sequence_admission", None), ("verify_cast_payments", "cast-payments"),
                        ("verify_multiresource", "multiresource"), ("verify_utility_migration", "utility-migration")):
        source = Path(__file__).with_name(name + ".script")
        target = folder / "regressions" / name
        target.mkdir(parents=True, exist_ok=True)
        script = source
        if scope:
            script = target / "input.script"
            script.write_text(source.read_text().replace("artifacts/skill_rebuild/" + scope, target.relative_to(PROJECT).as_posix()))
        result = view.baseline.call("run", str(script), "180000")
        write_json(target / "result.json", result)
        count = result["checks"]
        summary = dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count))
        reports.append(summary)
        write_json(folder / "regressions.json", reports)
        print(json.dumps(summary, ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError(name + " 未通过")


if __name__ == "__main__":
    main()
