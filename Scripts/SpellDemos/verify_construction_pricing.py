#!/usr/bin/env python3
"""验证实际构造需求、正式支付及版本化创法输入。"""
import json
import time
from pathlib import Path

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def cold_restart():
    """确认旧进程退出后再启动，恢复真实第六版世界和第二版生成输入。"""
    folder = PROJECT / "artifacts/skill_rebuild/construction-pricing"
    old = view.baseline.connect(True)
    view.baseline.call("run", str(Path(__file__).with_name("verify_construction_pricing_prepare_restart.script")), "120000")
    failure = None
    try:
        view.baseline.call("eval", "UnityEngine.Application.Quit(); return true;")
    except RuntimeError as error:
        failure = error
    until = time.monotonic() + 30
    while view.baseline.running(old["pid"]) and time.monotonic() < until:
        time.sleep(.1)
    if view.baseline.running(old["pid"]):
        raise RuntimeError("原独立游戏尚未退出，不启动第二份") from failure
    new = view.baseline.connect(True)
    write_json(folder / "restart-metadata.json", dict(before=old, after=new))
    result = view.baseline.call("run", str(Path(__file__).with_name("verify_construction_pricing_restart.script")), "120000")
    print(json.dumps(dict(name="pricing_cold_restart", passed=result["passed"], checks=result["checks"]), ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("需求基准的冷启动验证未通过")


def main():
    """共用独立游戏检查计算、明确选择和旧创法请求。"""
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/construction-pricing"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection))
    for name in ("verify_construction_pricing", "verify_definition_layers", "verify_magnitude_steps", "verify_derivation", "verify_operation_contracts"):
        result = view.baseline.call("run", str(Path(__file__).with_name(name + ".script")), "120000")
        count = result["checks"]
        print(json.dumps(dict(name=name, passed=result["passed"], checks=count if isinstance(count, int) else len(count)), ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError("构造需求计算或相关回归未通过：" + name)
    cold_restart()


if __name__ == "__main__":
    main()
