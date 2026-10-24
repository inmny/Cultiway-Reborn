#!/usr/bin/env python3
"""实测跨区材料身份、局部转换、历史升级及真实冷启动。"""
import json
import shutil
import subprocess
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view
import verify_world_material_inputs


def main():
    """先验证实际转换和历史档，再重启恢复本次第五版世界。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    folder = PROJECT / "artifacts/skill_rebuild/world-material-identity"
    folder.mkdir(parents=True, exist_ok=True)
    connection = view.baseline.connect(True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="3.04跨区材料身份与独立转换分支，世界材料第五版保存"))
    for suffix in ("", "_restart", "_motion", "_upgrade"):
        script = Path(__file__).with_name(Path(__file__).stem + suffix + ".script")
        shutil.copyfile(script, folder / ("input" + suffix + ".script"))
    result = view.baseline.call("run", str(Path(__file__).with_name(Path(__file__).stem + "_upgrade.script")), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("材料身份升级边界未通过：" + str(folder / "upgrade.json"))
    result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("材料身份检查未通过：" + str(folder / "checks.json"))
    verify_world_material_inputs.main()
    subprocess.run(["python3", str(Path(__file__).with_name("verify_view_switch.py")), "--quit"], check=True)
    restarted = view.baseline.connect(True)
    write_json(folder / "restart-metadata.json", dict(version=version, connection=restarted))
    result = view.baseline.call("run", str(Path(__file__).with_name(Path(__file__).stem + "_restart.script")), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("材料身份冷启动未通过：" + str(folder / "restart.json"))
    result = view.baseline.call("run", str(Path(__file__).with_name(Path(__file__).stem + "_motion.script")), "120000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("移动材料身份未通过：" + str(folder / "motion.json"))


if __name__ == "__main__":
    main()
