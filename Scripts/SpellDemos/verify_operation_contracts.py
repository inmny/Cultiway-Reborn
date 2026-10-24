#!/usr/bin/env python3
"""在私有游戏验证操作输入输出约定及正式首版资料兼容。"""
import json
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """复用当前私有游戏，保存专项和正式编号回归的独立证据。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/operation-contracts"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.03操作约定、全部当前构造编译及共同执行数值；输出绑定按3.04继续实现"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    result = view.baseline.call("run", str(script), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("操作约定专项未通过：" + str(folder / "checks.json"))
    source = Path(__file__).with_name("verify_definition_contract.script").read_text()
    regression = folder / "definition-input.script"
    regression.write_text(source.replace("artifacts/skill_rebuild/definition-contract", "artifacts/skill_rebuild/operation-contracts/definition-regression"))
    legacy = view.baseline.call("run", str(regression), "120000")
    print(json.dumps(dict(legacyPassed=legacy["passed"], legacyChecks=len(legacy["checks"])), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
