#!/usr/bin/env python3
"""逐阶段验证多资源构造、历史存档转换与真正重启；按 prepare、restart 的顺序运行。"""
import argparse
import json
import zipfile
from pathlib import Path
from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view

ROOT = PROJECT / "artifacts/skill_rebuild/multiresource"


def run(name, scope=None):
    target = ROOT / "runs" / name
    target.mkdir(parents=True, exist_ok=True)
    source = Path(__file__).with_name(name + ".script")
    script = source
    if scope:
        script = target / "input.script"
        script.write_text(source.read_text().replace("artifacts/skill_rebuild/" + scope,
                                                   target.relative_to(PROJECT).as_posix()))
    result = view.baseline.call("run", str(script), "180000")
    write_json(target / "result.json", result)
    count = result["checks"]
    print(json.dumps(dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count)), ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError(name + " 未通过")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("prepare", "restart"))
    args = parser.parse_args()
    ROOT.mkdir(parents=True, exist_ok=True)
    legacy = ROOT / "legacy"
    if args.phase == "prepare" and not legacy.exists():
        legacy.mkdir()
        with zipfile.ZipFile(PROJECT / "Scripts/WorldSaveTests/Fixtures/resource-channels-v1-materials-v4.zip") as archive:
            archive.extractall(legacy)
    private_installation("cultiway", root=view.ROOT / "environment", worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    version, _ = version_stamp()
    write_json(ROOT / (args.phase + "-metadata.json"), dict(version=version, connection=connection))
    if args.phase == "prepare":
        run("verify_multiresource_legacy")
        run("verify_cast_payments", "cast-payments")
        run("verify_recovery_resources", "recovery")
        run("verify_construction_resources")
        run("verify_multiresource")
        run("verify_multiresource_water")
    else:
        run("verify_construction_resources_restart")
        run("verify_multiresource_restart")
        run("verify_multiresource_water_restart")


if __name__ == "__main__":
    main()
