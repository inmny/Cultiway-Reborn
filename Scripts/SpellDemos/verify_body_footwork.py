#!/usr/bin/env python3
"""复用已准备的独立Worldfall安装，验证实际步法、落脚锚点和阻挡。"""
import argparse
import json
from pathlib import Path
import verify_view_switch as view
from common import PROJECT, version_stamp, write_json


def main():
    """不从下载目录重新安装，也不启用旧身体比例试稿。"""
    parser = argparse.ArgumentParser(description=__doc__)
    scenario = parser.add_mutually_exclusive_group()
    scenario.add_argument("--obstacles", action="store_true", help="验证不依赖镜头的实际建筑模型阻挡")
    scenario.add_argument("--surfaces", action="store_true", help="验证模型顶棚、落地及动作结束后的承托")
    scenario.add_argument("--surface-save", action="store_true", help="验证屋面位置的正式存读档恢复")
    parser.add_argument("--regressions", action="store_true", help="附加原有身体动作回归，单独保存结果")
    args = parser.parse_args()
    name = "body-surface-save" if args.surface_save else "body-surfaces" if args.surfaces else "body-obstacles" if args.obstacles else "body-footwork"
    folder = PROJECT / "artifacts/skill_rebuild" / name
    folder.mkdir(parents=True, exist_ok=True)
    connected = view.baseline.connect(True, validate_runtime=False)
    started = json.loads((view.baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for directory in ("Source", "Content", "GameResources")
           for p in (PROJECT / directory).rglob("*") if p.is_file()):
        raise RuntimeError("代码或资源晚于本进程，请先退出这一独立游戏再启动")
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    try:
        script = Path(__file__).with_suffix(".script")
        if args.obstacles or args.surfaces or args.surface_save:
            helpers = script.read_text().split("// BODY_SCENARIOS_BEGIN：", 1)[0]
            helpers = helpers.replace("artifacts/skill_rebuild/body-footwork", f"artifacts/skill_rebuild/{name}")
            script = folder / "input.script"
            if args.surface_save:
                setup = Path(__file__).with_name("verify_body_surfaces.script").read_text().split("// SURFACE_ROOF_READY", 1)[0]
                fragment = setup + Path(__file__).with_name("verify_body_surface_save.script").read_text()
            else:
                fragment = Path(__file__).with_name("verify_body_surfaces.script" if args.surfaces else "verify_body_obstacles.script").read_text()
            script.write_text(helpers + fragment)
        result = view.baseline.call("run", str(script), "180000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    result["checks"] = json.loads((folder / "checks.json").read_text())["checks"]
    write_json(folder / "result.json", result)
    print(json.dumps(dict(passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)
    if args.regressions:
        regression = folder / "regressions"
        regression.mkdir(parents=True, exist_ok=True)
        script = Path(__file__).with_name("verify_body_motion.script").read_text().replace(
            "artifacts/skill_rebuild/body-checks.json",
            f"{folder.relative_to(PROJECT).as_posix()}/regressions/body-checks.json")
        saved = regression / "input.script"
        saved.write_text(script)
        try:
            report = view.baseline.call("run", str(saved), "180000")
        except RuntimeError as error:
            write_json(regression / "result.json", dict(passed=False, error=str(error)))
            raise
        write_json(regression / "result.json", report)
        print(json.dumps(dict(regression=True, passed=report["passed"], checks=len(report["checks"])),
                         ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
