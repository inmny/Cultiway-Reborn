#!/usr/bin/env python3
"""在私有游戏验证操作输出真实驱动后续执行及既有构造回归。"""
import json
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """记录标量输出绑定专项，不把待接入集合或世界输出计为完成。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/output-bindings"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.04首批标量部位、结构、过程、关系及本地结果；集合及世界结果继续接入"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    result = view.baseline.call("run", str(script), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("输出绑定专项未通过：" + str(folder / "checks.json"))
    for name, original, destination in (
        ("verify_operation_contracts", "artifacts/skill_rebuild/operation-contracts", "artifacts/skill_rebuild/output-bindings/operation-regression"),
        ("verify_definition_contract", "artifacts/skill_rebuild/definition-contract", "artifacts/skill_rebuild/output-bindings/definition-regression"),
    ):
        source = Path(__file__).with_name(name + ".script").read_text()
        target = folder / (name + ".script")
        target.write_text(source.replace(original, destination))
        report = view.baseline.call("run", str(target), "120000")
        print(json.dumps(dict(name=name, passed=report["passed"], checks=report["checks"] if isinstance(report["checks"], int) else len(report["checks"])), ensure_ascii=False), flush=True)
        if not report["passed"]:
            raise RuntimeError("相关回归未通过：" + name)


if __name__ == "__main__":
    main()
