#!/usr/bin/env python3
"""复用独立原版或Worldfall安装，验证实际步法、落脚锚点和阻挡。"""
import argparse
import json
import os
import subprocess
from pathlib import Path
from common import PROJECT, version_stamp, write_json


def main():
    """不从下载目录重新安装，也不启用旧身体比例试稿。"""
    parser = argparse.ArgumentParser(description=__doc__)
    scenario = parser.add_mutually_exclusive_group()
    parser.add_argument("--native", action="store_true", help="在没有Worldfall的独立安装中验证，可配合双方连接检查")
    scenario.add_argument("--obstacles", action="store_true", help="验证不依赖镜头的实际建筑模型阻挡")
    scenario.add_argument("--surfaces", action="store_true", help="验证模型顶棚、落地及动作结束后的承托")
    scenario.add_argument("--exertions", action="store_true", help="验证七种发力的姿态、实际受力或损耗")
    scenario.add_argument("--force-routing", action="store_true", help="验证真实接触传力、分散和有限卸力")
    scenario.add_argument("--connections", action="store_true", help="验证手部对身体的真实抓持、抵住和解除")
    scenario.add_argument("--equipment", action="store_true", help="验证甲片、实际持物和装备改变后的关系解除")
    parser.add_argument("--equipment-items", nargs="+", choices=["sword", "spear", "hammer", "axe", "bow", "stick", "staff"], help="只核对指定持物，仍执行装备变化检查")
    scenario.add_argument("--weapons", action="store_true", help="验证真实兵器结构、握持、接触和正式恢复")
    scenario.add_argument("--slopes", action="store_true", help="验证实际坡面前进、下坡跃落和山墙")
    scenario.add_argument("--terrain", action="store_true", help="验证实际岸边、桥面与普通移动归还")
    scenario.add_argument("--placements", action="store_true", help="对照实际绘制验证自然物、特殊物件、无模型阻挡和船坞桥面")
    scenario.add_argument("--surface-save", action="store_true", help="验证屋面位置的正式存读档恢复")
    parser.add_argument("--record", action="store_true", help="记录同一施放的原版与Worldfall对应帧")
    parser.add_argument("--regressions", action="store_true", help="附加原有身体动作回归，单独保存结果")
    parser.add_argument("--connection-cases", nargs="+", choices=["grab", "brace", "torso", "torsogap", "headgap", "envelope", "withdraw", "gap", "busy", "immovable", "external", "force", "resistance", "walk", "speed", "pause"], help="只运行指定的双方连接场景，结果另存")
    args = parser.parse_args()
    if args.equipment_items and not args.equipment:
        parser.error("--equipment-items 与 --equipment 配合使用")
    if args.native and any((args.obstacles, args.surfaces, args.exertions, args.force_routing, args.weapons, args.equipment,
                            args.slopes, args.terrain, args.placements, args.surface_save)):
        parser.error("--native 当前支持基础步法或 --connections")
    if args.connection_cases and not args.connections:
        parser.error("--connection-cases 与 --connections 配合使用")
    if args.record and not (args.slopes or args.weapons or args.exertions):
        parser.error("--record 与 --slopes、--weapons 或 --exertions 配合，记录同次施放的双视角")
    if args.native:
        from benchmark_baselines import private_installation
        native_root = PROJECT / "artifacts/skill_rebuild/performance-baseline"
        game = private_installation("cultiway", root=native_root)
        os.environ["CULTIWAY_BASELINE_ROOT"] = str(native_root / "cultiway")
        os.environ["CULTIWAY_BASELINE_GAME"] = str(game)
        import baseline_scenarios as baseline
    else:
        import verify_view_switch as view
        baseline = view.baseline
    name = ("body-equipment" if args.equipment else "body-connections" if args.connections else "body-force-routing" if args.force_routing else "body-exertions" if args.exertions else "body-weapons" if args.weapons else "body-slopes" if args.slopes else "body-footwork-native" if args.native else "body-terrain" if args.terrain else
            "body-placements" if args.placements else "body-surface-save" if args.surface_save else
            "body-surfaces" if args.surfaces else "body-obstacles" if args.obstacles else "body-footwork")
    if args.native and args.connections:
        name += "-native"
    folder = PROJECT / "artifacts/skill_rebuild" / name
    if args.connection_cases:
        folder = folder / ("cases-" + "-".join(args.connection_cases))
    if args.equipment_items:
        folder = folder / ("items-" + "-".join(args.equipment_items))
    folder.mkdir(parents=True, exist_ok=True)
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for directory in ("Source", "Content", "GameResources")
           for p in (PROJECT / directory).rglob("*") if p.is_file()):
        raise RuntimeError("代码或资源晚于本进程，请先退出这一独立游戏再启动")
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    try:
        script = Path(__file__).with_suffix(".script")
        if args.native:
            text = script.read_text().replace("artifacts/skill_rebuild/body-footwork", str(folder.relative_to(PROJECT)))
            text = text.replace('await Game.WaitUntil(() => (bool)bridge.GetProperty("IsInstalled", F).GetValue(null), 1800);',
                'Check(!(bool)bridge.GetProperty("IsInstalled", F).GetValue(null) && !AppDomain.CurrentDomain.GetAssemblies()'
                '.Any(a => a.GetType("FirstPerson.WorldBoxMod") != null), "独立原版环境没有加载Worldfall或安装三维身体提供者");')
            script = folder / "input.script"
            script.write_text(text)
        if args.obstacles or args.surfaces or args.surface_save or args.placements or args.terrain or args.slopes or args.weapons or args.exertions or args.force_routing or args.connections or args.equipment:
            helpers = script.read_text().split("// BODY_SCENARIOS_BEGIN：", 1)[0]
            helpers = helpers.replace("artifacts/skill_rebuild/body-footwork", str(folder.relative_to(PROJECT)))
            script = folder / "input.script"
            if args.equipment:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                equipment = Path(__file__).with_name("verify_body_equipment.script").read_text()
                if args.equipment_items:
                    values = ", ".join(json.dumps(item) for item in args.equipment_items)
                    equipment = equipment.replace("var selectedItems = Array.Empty<string>();", "var selectedItems = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            equipment + "\n}\n" +
                            connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.connections:
                fragment = Path(__file__).with_name("verify_body_connections.script").read_text()
                if args.connection_cases:
                    values = ", ".join(json.dumps(case) for case in args.connection_cases)
                    fragment = fragment.replace("var selectedCases = Array.Empty<string>();", "var selectedCases = new[] { " + values + " };")
            elif args.force_routing:
                fragment = Path(__file__).with_name("verify_body_force_routing.script").read_text()
            elif args.slopes or args.weapons or args.exertions:
                fragment = Path(__file__).with_name("verify_body_exertions.script" if args.exertions else "verify_body_weapons.script" if args.weapons else "verify_body_slopes.script").read_text()
                if args.record:
                    fragment = fragment.replace("const bool record = false;", "const bool record = true;")
            elif args.terrain:
                fragment = Path(__file__).with_name("verify_body_terrain.script").read_text()
            elif args.placements:
                fragment = Path(__file__).with_name("verify_body_placements.script").read_text()
            elif args.surface_save:
                setup = Path(__file__).with_name("verify_body_surfaces.script").read_text().split("// SURFACE_ROOF_READY", 1)[0]
                fragment = setup + Path(__file__).with_name("verify_body_surface_save.script").read_text()
            else:
                fragment = Path(__file__).with_name("verify_body_surfaces.script" if args.surfaces else "verify_body_obstacles.script").read_text()
            script.write_text(helpers + fragment)
        write_json(folder / "result.json", dict(passed=False, status="running"))
        result = baseline.call("run", str(script), "180000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    result["checks"] = json.loads((folder / "checks.json").read_text())["checks"]
    write_json(folder / "result.json", result)
    print(json.dumps(dict(passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)
    if args.record:
        frames = json.loads((folder / "movie-frames.json").read_text())
        font = subprocess.run(["fc-match", "-f", "%{file}", "sans:lang=zh"],
                              capture_output=True, text=True, check=True).stdout
        for name in dict.fromkeys(frame["name"] for frame in frames):
            rows = [frame for frame in frames if frame["name"] == name]
            movie = folder / "movies" / name
            for mode in ("native", "worldfall"):
                lines = ["ffconcat version 1.0"]
                for i, frame in enumerate(rows):
                    seconds = rows[i + 1]["world"] - frame["world"] if i + 1 < len(rows) else .1
                    lines.extend([f"file '{mode}/{frame['index']:05d}.png'", f"duration {seconds:.9f}"])
                lines.append(f"file '{mode}/{rows[-1]['index']:05d}.png'")
                (movie / f"{mode}.ffconcat").write_text("\n".join(lines) + "\n")
            filters = (f"[0:v]scale=960:540,drawtext=fontfile='{font}':text='原版视角':fontsize=24:"
                       "fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16[a];"
                       f"[1:v]scale=960:540,drawtext=fontfile='{font}':text='Worldfall':fontsize=24:"
                       "fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16[b];[a][b]hstack=inputs=2[v]")
            output = movie / "comparison.mp4"
            subprocess.run(["ffmpeg", "-y", "-v", "error", "-safe", "0", "-f", "concat", "-i", str(movie / "native.ffconcat"),
                            "-safe", "0", "-f", "concat", "-i", str(movie / "worldfall.ffconcat"), "-filter_complex", filters,
                            "-map", "[v]", "-r", "24", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(output)], check=True)
            print("双视角录像：" + str(output), flush=True)
    if args.regressions:
        regression = folder / "regressions"
        regression.mkdir(parents=True, exist_ok=True)
        script = Path(__file__).with_name("verify_body_motion.script").read_text().replace(
            "artifacts/skill_rebuild/body-checks.json",
            f"{folder.relative_to(PROJECT).as_posix()}/regressions/body-checks.json")
        saved = regression / "input.script"
        saved.write_text(script)
        try:
            report = baseline.call("run", str(saved), "180000")
        except RuntimeError as error:
            write_json(regression / "result.json", dict(passed=False, error=str(error)))
            raise
        write_json(regression / "result.json", report)
        print(json.dumps(dict(regression=True, passed=report["passed"], checks=len(report["checks"])),
                         ensure_ascii=False), flush=True)

        if args.exertions or args.force_routing:
            impulse_folder = regression / "actor-impulses"
            impulse_folder.mkdir(exist_ok=True)
            saved = impulse_folder / "input.script"
            saved.write_text(Path(__file__).with_name("verify_actor_impulses.script").read_text().replace(
                "artifacts/skill_rebuild/actor-reactions", str(impulse_folder.relative_to(PROJECT))))
            report = baseline.call("run", str(saved), "180000")
            write_json(impulse_folder / "result.json", report)
            print(json.dumps(dict(actor_impulses=True, passed=report["passed"], checks=len(report["checks"]))), flush=True)


if __name__ == "__main__":
    main()
