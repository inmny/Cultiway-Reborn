#!/usr/bin/env python3
"""验证发布形体的语义、连接、生成输入、变化声明及原有骨架和分件。"""
import json
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """复用独立游戏，核对真实发布资料，再回归原有网格与动作。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connected = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/form-structure"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected))
    result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    source = Path(__file__).with_name("verify_form_rig.script").read_text()
    source = source.replace("artifacts/skill_rebuild/form-rig-checks.json", "artifacts/skill_rebuild/form-structure/rig-checks.json")
    source = source.replace("artifacts/skill_rebuild/swimming-whale.json", "artifacts/skill_rebuild/form-structure/swimming-whale.json")
    script = folder / "input-rig.script"
    script.write_text(source)
    rig = view.baseline.call("run", str(script), "180000")
    print(json.dumps(dict(passed=rig["passed"], rigChecks=len(rig["checks"])), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
