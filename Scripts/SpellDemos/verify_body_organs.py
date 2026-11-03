#!/usr/bin/env python3
"""在已有独立原版游戏验证实际器官动作、接触、保存和编辑。"""
import argparse
import json
from pathlib import Path
import baseline_scenarios as baseline
from common import PROJECT, write_json, version_stamp


def run_report(script, report):
    """每次记录实际结果，失败不能沿用此前的通过文件。"""
    try:
        result = baseline.call("run", str(script), "180000")
    except RuntimeError as error:
        write_json(report, dict(passed=False, error=str(error)))
        raise
    write_json(report, result)
    return result


def main():
    """复用原版测试安装，不修改玩家配置或打开三维试稿。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--regressions", action="store_true", help="附加原身体及肘肩专项，结果单独保存")
    args = parser.parse_args()
    folder = PROJECT / "artifacts/skill_rebuild/body-organs"
    folder.mkdir(parents=True, exist_ok=True)
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for d in ("Source", "Content", "GameResources")
           for p in (PROJECT / d).rglob("*") if p.is_file()):
        raise RuntimeError("代码或资源晚于本进程，请先退出这一独立游戏再启动")
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    result = run_report(Path(__file__).with_suffix(".script"), folder / "result.json")
    print(json.dumps(dict(passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)
    if args.regressions:
        for name, source_folder in (("verify_body_motion", "body-checks.json"), ("verify_body_parts", "body-parts")):
            target = folder / "regressions" / name
            target.mkdir(parents=True, exist_ok=True)
            script = Path(__file__).with_name(name + ".script").read_text()
            if source_folder:
                script = script.replace("artifacts/skill_rebuild/" + source_folder,
                                        "artifacts/skill_rebuild/body-organs/regressions/" + name +
                                        ("/checks.json" if source_folder.endswith(".json") else ""))
            frozen = target / "input.script"
            frozen.write_text(script)
            report = run_report(frozen, target / "result.json")
            print(json.dumps(dict(name=name, passed=report["passed"], checks=len(report["checks"])),
                             ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
