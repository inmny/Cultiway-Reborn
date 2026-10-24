#!/usr/bin/env python3
"""验证新增共用操作的登记、真实运行与正式构造恢复，不改玩家世界。"""
import json
from pathlib import Path
from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/operation-registration"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection))
    report = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "120000")
    print(json.dumps(report, ensure_ascii=False), flush=True)
    if not report["passed"]:
        raise RuntimeError("共用操作登记专项未通过")
    results = []
    for name, previous in (
        ("verify_output_bindings", "output-bindings"),
        ("verify_collection_bindings", "collection-bindings"),
        ("verify_operation_contracts", "operation-contracts"),
        ("verify_definition_contract", "definition-contract"),
        ("verify_native_outputs_world", "native-outputs/world"),
        ("verify_range_and_motion_hold", "range-motion-hold"),
    ):
        target = folder / "regressions" / name
        target.mkdir(parents=True, exist_ok=True)
        script = target / "input.script"
        source = Path(__file__).with_name(name + ".script")
        script.write_text(source.read_text().replace(
            "artifacts/skill_rebuild/" + previous,
            "artifacts/skill_rebuild/operation-registration/regressions/" + name))
        result = view.baseline.call("run", str(script), "120000")
        write_json(target / "result.json", result)
        count = result["checks"]
        summary = dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count))
        results.append(summary)
        write_json(folder / "regression-results.json", results)
        print(json.dumps(summary, ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError(name + " 未通过")


if __name__ == "__main__":
    main()
