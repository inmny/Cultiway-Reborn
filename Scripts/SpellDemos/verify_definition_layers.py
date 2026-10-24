#!/usr/bin/env python3
"""验证构造来源、玩家选择和默认补全的固定优先次序。"""
import json
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view
import verify_output_bindings


def main():
    """在私有游戏验证合成和实际编辑，再回归已有编辑、创法及构造合同。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/definition-layers"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.05构造合成顺序与明确选择，实际编辑、支付及正式资料恢复"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    result = view.baseline.call("run", str(script), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("构造优先次序检查未通过：" + str(folder / "checks.json"))
    for name, original, destination in (
        ("verify_construction_editing", 'Path.Combine(root,"artifacts/skill_rebuild")', 'Path.Combine(root,"artifacts/skill_rebuild/definition-layers/editing")'),
        ("verify_derivation", 'artifacts/skill_rebuild/derivation', 'artifacts/skill_rebuild/definition-layers/derivation'),
    ):
        (folder / "editing").mkdir(exist_ok=True)
        source = Path(__file__).with_name(name + ".script").read_text()
        target = folder / (name + ".script")
        source = source.replace(original, destination)
        if name == "verify_derivation":
            source = source.replace('Path.Combine(folder,"legacy-v2")', 'Path.Combine(root,"artifacts/skill_rebuild/derivation/legacy-v2")')
            source = source.replace('Path.Combine(folder,"legacy-expected.json")', 'Path.Combine(root,"artifacts/skill_rebuild/derivation/legacy-expected.json")')
        target.write_text(source)
        report = view.baseline.call("run", str(target), "120000")
        print(json.dumps(dict(name=name, passed=report["passed"], checks=report["checks"] if isinstance(report["checks"], int) else len(report["checks"])), ensure_ascii=False), flush=True)
        if not report["passed"]:
            raise RuntimeError("来源或编辑回归未通过：" + name)
    verify_output_bindings.main()


if __name__ == "__main__":
    main()
