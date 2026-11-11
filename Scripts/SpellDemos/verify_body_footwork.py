#!/usr/bin/env python3
"""复用独立原版或Worldfall安装，验证实际步法、落脚锚点和阻挡。"""
import argparse
import json
import os
import subprocess
import sys
from pathlib import Path
from common import PROJECT, version_stamp, write_json


def main():
    """不从下载目录重新安装，也不启用旧身体比例试稿。"""
    parser = argparse.ArgumentParser(description=__doc__)
    scenario = parser.add_mutually_exclusive_group()
    held_options = ["sword.tip", "sword.edge", "spear.tip", "spear.shaft", "warhammer.face.right", "warhammer.face.left", "shield.face"]
    parser.add_argument("--native", action="store_true", help="在没有Worldfall的独立安装中验证，可配合双方连接检查")
    scenario.add_argument("--obstacles", action="store_true", help="验证不依赖镜头的实际建筑模型阻挡")
    scenario.add_argument("--surfaces", action="store_true", help="验证模型顶棚、落地及动作结束后的承托")
    scenario.add_argument("--exertions", action="store_true", help="验证七种发力的姿态、实际受力或损耗")
    scenario.add_argument("--force-routing", action="store_true", help="验证真实接触传力、分散和有限卸力")
    scenario.add_argument("--carrying", action="store_true", help="核对抓持后的真实带动、环境截停与归还")
    parser.add_argument("--carry-cases", nargs="+", choices=["move", "rotate", "rotate-item", "blocked", "ground", "model", "throw", "throw-detached", "throw-external"], help="只核对指定带动场景")
    scenario.add_argument("--connections", action="store_true", help="验证手部对身体的真实抓持、抵住和解除")
    scenario.add_argument("--connection-parts", action="store_true", help="验证肘肩膝足头及器官的实际原地维持")
    scenario.add_argument("--target-parts", action="store_true", help="验证实际人形部位及动物表面；独立原版支持地面人形与器官")
    scenario.add_argument("--organ-registry", action="store_true", help="按当前器官、等级及身体登记遍历绑定与真实表面")
    scenario.add_argument("--body-registry", action="store_true", help="遍历Worldfall全部动物模型及具名部件的选择与真实接触")
    scenario.add_argument("--airborne-surfaces", action="store_true", help="核对蜂蝇蝶与真实龙的振翼、飞行和翼膜实际显示")
    scenario.add_argument("--natural-surfaces", action="store_true", help="核对真实自然攻击、受击与身体、持物表面的姿态交接")
    parser.add_argument("--natural-weapon", choices=["sword", "bow"], help="与--natural-surfaces配合，只检查指定的已有持物")
    scenario.add_argument("--swimming-surfaces", action="store_true", help="核对真实游泳身体、水面高度及进出水中后的关系解除")
    parser.add_argument("--swimming-cases", nargs="+", choices=["human.pose", "wolf.pose", "piranha.pose", "human.enter", "human.leave", "wolf.enter", "wolf.leave", "human.moving", "human.reach", "human.deck", "human.deck-source"], help="只核对指定游泳场景，结果另存")
    parser.add_argument("--organ-layers", nargs="+", help="与--organ-registry配合，只核对指定图层并另存结果")
    scenario.add_argument("--connection-lifecycle", action="store_true", help="验证真实出招冲突、死亡、形体变化、换世界及正式关系保存")
    scenario.add_argument("--held-connections", action="store_true", help="验证本方实际兵器的接触维持、握点与来源换装解除")
    scenario.add_argument("--acting-surfaces", action="store_true", help="核对出招手臂、持物、步法、头部及完整身体表面")
    scenario.add_argument("--moving-connections", action="store_true", help="验证移动中真实接触、共同位置、落空与归还移动")
    parser.add_argument("--acting-cases", nargs="+", choices=["right", "left", "sword", "walk", "head", "pair", "organ", "organ-mirror", "horn-evilspotter", "wing-fupeng", "wing-fierce", "shell", "tail1", "tail3", "tail", "tail-mirror"], help="只核对指定出招表面，结果另存")
    parser.add_argument("--moving-cases", nargs="+", choices=["target", "fast", "miss", "source"], help="只检查指定移动场景，结果另存")
    parser.add_argument("--held-cases", nargs="+", choices=held_options, help="只验证指定的本方兵器部位，结果另存")
    scenario.add_argument("--equipment-occlusion", action="store_true", help="核对原版身体和持物的真实前后遮挡")
    scenario.add_argument("--equipment", action="store_true", help="验证甲片、实际持物和装备改变后的关系解除")
    parser.add_argument("--armor-only", action="store_true", help="与--equipment配合，只核对甲片，结果另存")
    parser.add_argument("--equipment-items", nargs="+", choices=["sword", "spear", "hammer", "axe", "bow", "stick", "staff"], help="只核对指定持物，仍执行装备变化检查")
    scenario.add_argument("--weapons", action="store_true", help="验证真实兵器结构、握持、接触和正式恢复")
    scenario.add_argument("--slopes", action="store_true", help="验证实际坡面前进、下坡跃落和山墙")
    scenario.add_argument("--terrain", action="store_true", help="验证实际岸边、桥面与普通移动归还")
    scenario.add_argument("--placements", action="store_true", help="对照实际绘制验证自然物、特殊物件、无模型阻挡和船坞桥面")
    scenario.add_argument("--surface-save", action="store_true", help="验证屋面位置的正式存读档恢复")
    parser.add_argument("--film-relations", action="store_true", help="录制一个指定关系场景的实际过程，结果放在film子目录")
    parser.add_argument("--record", action="store_true", help="记录同一施放的原版与Worldfall对应帧")
    parser.add_argument("--regressions", action="store_true", help="附加原有身体动作回归，单独保存结果")
    parser.add_argument("--parallel-occupancy", action="store_true", help="与本方持物或部位关系配合，核对另一动作被拒绝及原控制归还")
    parser.add_argument("--source-cases", nargs="+", choices=["head", "foot.right", "organ:yao.horn.thunder"], help="与--connection-parts配合，仅运行指定的既有来源部位")
    parser.add_argument("--connection-cases", nargs="+", choices=["grab", "brace", "torso", "torsogap", "headgap", "envelope", "withdraw", "gap", "busy", "immovable", "external", "force", "resistance", "walk", "speed", "pause"], help="只运行指定的双方连接场景，结果另存")
    parser.add_argument("--target-cases", nargs="+", choices=["human.head", "human.torso", "human.arm.right.upper", "human.arm.right.lower", "human.arm.left.upper", "human.arm.left.lower", "human.leg.right.upper", "human.leg.right.lower", "human.leg.left.upper", "human.leg.left.lower", "human.hand.right", "human.hand.left", "human.foot.right", "human.foot.left", "wolf.head", "fox.surface", "turtle.surface", "bear.surface", "crocodile.part:tail", "penguin.surface", "snake.surface", "buffalo.surface", "rhino.surface", "bee.surface", "fly.surface", "butterfly.surface", "dragon.surface", "wolf.organ:yao.horn.thunder", "wolf.organ:yao.horn.evilspotter", "fox.organ:yao.crown.tails", "penguin.organ:yao.wing.fupeng", "penguin.organ:yao.wing.fierce", "turtle.organ:yao.shell.black", "wolf.organ:yao.eye.illusion", "wolf.organ:yao.feather.fire", "wolf.organ:yao.fur.moonlight", "wolf.organ:yao.scale.basic", "wolf.organ:yao.scale.fine"], help="只运行指定目标表面，保留公共裁分及缺失部位检查")
    parser.add_argument("--lifecycle-cases", nargs="+", choices=["occupied", "source-death", "target-death", "source-body", "target-body", "source-body-fade", "immovable-after", "terrain-fade", "save"], help="只验证指定的关系生命周期，结果另存")
    parser.add_argument("--organ-rank", type=int, choices=[1, 3, 6], help="与--target-parts配合，选择尾冠实际等级并另存结果")
    parser.add_argument("--target-transition", choices=["takeoff", "land", "dragon-state"], help="指定目标建立关系后切换实际飞行或龙动作状态，结果另存")
    parser.add_argument("--target-motion", choices=["moving", "reach"], help="核对实际悬浮身体的移动中建立或退远解除，结果另存")
    args = parser.parse_args()
    if (args.body_registry or args.airborne_surfaces) and args.native:
        parser.error("--body-registry 核对Worldfall自己的模型部件；原版器官使用 --organ-registry --native")
    if args.natural_weapon and not args.natural_surfaces:
        parser.error("--natural-weapon 与 --natural-surfaces 配合使用")
    if args.native and args.natural_weapon == "bow":
        parser.error("动态弓形专项需要Worldfall；原版自然动作使用 --natural-surfaces --native")
    if args.organ_layers and not args.organ_registry:
        parser.error("--organ-layers 与 --organ-registry 配合使用")
    if args.native and args.swimming_cases and any(case.startswith("human.deck") for case in args.swimming_cases):
        parser.error("模型桥面专项需要 Worldfall")
    if args.swimming_cases and not args.swimming_surfaces:
        parser.error("--swimming-cases 与 --swimming-surfaces 配合使用")
    if args.equipment_occlusion and not args.native:
        parser.error("--equipment-occlusion 需要 --native")
    if args.equipment_items and not args.equipment:
        parser.error("--equipment-items 与 --equipment 配合使用")
    if args.armor_only and (not args.equipment or args.equipment_items):
        parser.error("--armor-only 与 --equipment 配合，不能同时指定持物")
    if args.held_cases and not args.held_connections:
        parser.error("--held-cases 与 --held-connections 配合使用")
    if args.acting_cases and not args.acting_surfaces:
        parser.error("--acting-cases 与 --acting-surfaces 配合使用")
    if args.native and args.target_cases and any(".organ:" not in case and not case.startswith("human.") and case not in ["bee.surface", "fly.surface", "butterfly.surface", "dragon.surface"] for case in args.target_cases):
        parser.error("独立原版目前提供人形部位与器官目标专项；其他动物头身细分继续接入")
    if args.moving_cases and not args.moving_connections:
        parser.error("--moving-cases 与 --moving-connections 配合使用")
    if args.native and any((args.obstacles, args.surfaces, args.exertions, args.force_routing, args.weapons,
                            args.slopes, args.terrain, args.placements, args.surface_save)):
        parser.error("--native 当前支持身体与持物专项；所选地表或三维专项尚未接入")
    if args.parallel_occupancy and not (args.held_connections or args.connection_parts):
        parser.error("--parallel-occupancy 与 --held-connections 或 --connection-parts 配合")
    if args.source_cases and not args.connection_parts:
        parser.error("--source-cases 与 --connection-parts 配合")
    if args.connection_cases and not args.connections:
        parser.error("--connection-cases 与 --connections 配合使用")
    if args.target_cases and not args.target_parts:
        parser.error("--target-cases 与 --target-parts 配合使用")
    if args.target_transition and (not args.target_parts or not args.target_cases):
        parser.error("--target-transition 需要 --target-parts 和明确的 --target-cases")
    if args.target_motion and (not args.target_parts or not args.target_cases or args.target_transition):
        parser.error("--target-motion 需要明确的目标部位专项，不能同时指定状态切换")
    if args.target_transition == "dragon-state" and args.target_cases != ["dragon.surface"]:
        parser.error("龙动作切换需要 --target-cases dragon.surface")
    if args.organ_rank and (not args.target_parts or args.target_cases and "fox.organ:yao.crown.tails" not in args.target_cases):
        parser.error("--organ-rank 需要包含狐尾的 --target-parts 场景")
    if args.native and args.lifecycle_cases and "terrain-fade" in args.lifecycle_cases:
        parser.error("地表归还专项需要Worldfall的实际地表快照")
    if args.lifecycle_cases and not args.connection_lifecycle:
        parser.error("--lifecycle-cases 与 --connection-lifecycle 配合使用")
    if args.native and args.carry_cases and "model" in args.carry_cases:
        parser.error("实际房屋模型需要Worldfall，原版使用ground或blocked共用数值面检查")
    if args.carry_cases and not args.carrying:
        parser.error("--carry-cases 与 --carrying 配合使用")
    if args.film_relations:
        selectors = [(args.connections, args.connection_cases), (args.held_connections, args.held_cases),
                     (args.connection_parts, args.source_cases), (args.target_parts, args.target_cases),
                     (args.swimming_surfaces, args.swimming_cases)]
        if not any(enabled and cases and len(cases) == 1 for enabled, cases in selectors):
            parser.error("--film-relations 需要明确指定一个关系场景")
    if args.record and not (args.slopes or args.weapons or args.exertions):
        parser.error("--record 与 --slopes、--weapons 或 --exertions 配合，记录同次施放的双视角")
    if args.held_connections and (not args.held_cases or len(args.held_cases) > 1):
        # 各兵器独立运行，复用同一游戏；每次仍受原脚本时限约束，避免把绘制与全部接触塞进一次请求。
        name = "body-held-connections" + ("-native" if args.native else "")
        folder = PROJECT / "artifacts/skill_rebuild" / name
        if args.held_cases:
            folder /= "cases-" + "-".join(args.held_cases)
        if args.parallel_occupancy:
            folder /= "parallel-occupancy"
        folder.mkdir(parents=True, exist_ok=True)
        report = dict(passed=False, status="running", cases=[], checks=[])
        write_json(folder / "result.json", report)
        for case in args.held_cases or held_options:
            command = [sys.executable, str(Path(__file__).resolve()), "--held-connections", "--held-cases", case]
            if args.native:
                command.append("--native")
            if args.regressions:
                command.append("--regressions")
            if args.parallel_occupancy:
                command.append("--parallel-occupancy")
            child = subprocess.run(command)
            result_folder = PROJECT / "artifacts/skill_rebuild" / name / ("cases-" + case)
            if args.parallel_occupancy:
                result_folder /= "parallel-occupancy"
            result_path = result_folder / "result.json"
            result = json.loads(result_path.read_text()) if child.returncode == 0 else None
            if child.returncode or not result["passed"]:
                report.update(status="failed", failedCase=case, result=str(result_path))
                write_json(folder / "result.json", report)
                raise RuntimeError("兵器场景未通过：" + case + "，见 " + str(result_path))
            metadata = json.loads(result_path.with_name("metadata.json").read_text())
            report["cases"].append(dict(id=case, checks=len(result["checks"]), result=str(result_path), version=metadata["version"]))
            report["checks"].extend(result["checks"])
            write_json(folder / "result.json", report)
        report.update(passed=True, status="complete")
        write_json(folder / "checks.json", dict(checks=report["checks"]))
        write_json(folder / "metadata.json", dict(cases=report["cases"]))
        write_json(folder / "result.json", report)
        print(json.dumps(dict(passed=True, cases=len(report["cases"]), checks=len(report["checks"])), ensure_ascii=False), flush=True)
        return
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
    name = ("body-carrying" if args.carrying else "body-natural-surfaces" if args.natural_surfaces else "body-swimming-surfaces" if args.swimming_surfaces else "body-airborne-surfaces" if args.airborne_surfaces else "body-model-registry" if args.body_registry else "body-organ-registry" if args.organ_registry else "body-acting-surfaces" if args.acting_surfaces else "body-moving-connections" if args.moving_connections else "body-held-connections" if args.held_connections else "body-connection-lifecycle" if args.connection_lifecycle else "body-target-parts" if args.target_parts else "body-connection-parts" if args.connection_parts else "body-equipment-occlusion" if args.equipment_occlusion else "body-equipment" if args.equipment else "body-connections" if args.connections else "body-force-routing" if args.force_routing else "body-exertions" if args.exertions else "body-weapons" if args.weapons else "body-slopes" if args.slopes else "body-footwork-native" if args.native else "body-terrain" if args.terrain else
            "body-placements" if args.placements else "body-surface-save" if args.surface_save else
            "body-surfaces" if args.surfaces else "body-obstacles" if args.obstacles else "body-footwork")
    if args.native and (args.carrying or args.natural_surfaces or args.swimming_surfaces or args.organ_registry or args.connections or args.connection_parts or args.target_parts or args.connection_lifecycle or args.held_connections or args.moving_connections or args.acting_surfaces or args.equipment or args.equipment_occlusion):
        name += "-native"
    folder = PROJECT / "artifacts/skill_rebuild" / name
    if args.carry_cases:
        folder /= "cases-" + "-".join(args.carry_cases)
    if args.natural_weapon:
        folder /= "weapon-" + args.natural_weapon
    if args.swimming_cases:
        folder = folder / ("cases-" + "-".join(args.swimming_cases))
    if args.acting_cases:
        folder = folder / ("cases-" + "-".join(args.acting_cases))
    if args.moving_cases:
        folder = folder / ("cases-" + "-".join(args.moving_cases))
    if args.held_cases:
        folder = folder / ("cases-" + "-".join(args.held_cases))
    if args.lifecycle_cases:
        folder = folder / ("cases-" + "-".join(args.lifecycle_cases))
    if args.connection_cases:
        folder = folder / ("cases-" + "-".join(args.connection_cases))
    if args.equipment_items:
        folder = folder / ("items-" + "-".join(args.equipment_items))
    if args.armor_only:
        folder /= "armor"
    if args.target_cases:
        folder = folder / ("cases-" + "-".join(case.replace(":", ".") for case in args.target_cases))
    if args.target_transition:
        folder /= "transition-" + args.target_transition
    if args.target_motion:
        folder /= "motion-" + args.target_motion
    if args.organ_rank:
        folder /= "rank-" + str(args.organ_rank)
    if args.organ_layers:
        folder /= "layers-" + "-".join(args.organ_layers)
    if args.source_cases:
        folder /= "sources-" + "-".join(case.replace(":", ".") for case in args.source_cases)
    if args.parallel_occupancy:
        folder /= "parallel-occupancy"
    if args.film_relations:
        folder /= "film"
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
        if args.carrying or args.natural_surfaces or args.swimming_surfaces or args.airborne_surfaces or args.body_registry or args.organ_registry or args.obstacles or args.surfaces or args.surface_save or args.placements or args.terrain or args.slopes or args.weapons or args.exertions or args.force_routing or args.connections or args.equipment or args.equipment_occlusion or args.connection_parts or args.target_parts or args.connection_lifecycle or args.held_connections or args.moving_connections or args.acting_surfaces:
            helpers = script.read_text().split("// BODY_SCENARIOS_BEGIN：", 1)[0]
            helpers = helpers.replace("artifacts/skill_rebuild/body-footwork", str(folder.relative_to(PROJECT)))
            script = folder / "input.script"
            if args.carrying:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                placement = Path(__file__).with_name("verify_body_connection_placement.script").read_text()
                carrying = Path(__file__).with_name("verify_body_carrying.script").read_text()
                carrying = carrying.replace("// CARRY_EXTRA_HELPERS", Path(__file__).with_name("verify_body_rotation.script").read_text() + "\n" +
                    Path(__file__).with_name("verify_body_model_carrying.script").read_text() + "\n" +
                    Path(__file__).with_name("verify_body_released.script").read_text())
                if args.carry_cases:
                    values = ", ".join(json.dumps(case) for case in args.carry_cases)
                    carrying = carrying.replace("var selectedCarrying = Array.Empty<string>();", "var selectedCarrying = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] + placement +
                            carrying + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.natural_surfaces:
                fragment = Path(__file__).with_name("verify_natural_surfaces.script").read_text()
                if args.natural_weapon:
                    fragment = fragment.replace('const string naturalWeapon = "sword";', 'const string naturalWeapon = ' + json.dumps(args.natural_weapon) + ';')
            elif args.swimming_surfaces:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                placement = Path(__file__).with_name("verify_body_connection_placement.script").read_text()
                swimming = Path(__file__).with_name("verify_bridge_surfaces.script").read_text() + "\n" + Path(__file__).with_name("verify_swimming_surfaces.script").read_text()
                if args.swimming_cases:
                    values = ", ".join(json.dumps(case) for case in args.swimming_cases)
                    swimming = swimming.replace("var selectedSwimming = Array.Empty<string>();", "var selectedSwimming = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] + placement +
                            swimming + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.body_registry or args.airborne_surfaces:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                registry = Path(__file__).with_name("verify_airborne_surfaces.script" if args.airborne_surfaces else "verify_body_registry.script").read_text()
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            registry + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.organ_registry:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                registry = Path(__file__).with_name("verify_organ_registry.script").read_text()
                if args.organ_layers:
                    values = ", ".join(json.dumps(layer) for layer in args.organ_layers)
                    registry = registry.replace("var selectedRegistryLayers = Array.Empty<string>();", "var selectedRegistryLayers = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            registry + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.acting_surfaces:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                acting = Path(__file__).with_name("verify_acting_surfaces.script").read_text()
                if args.acting_cases:
                    values = ", ".join(json.dumps(case) for case in args.acting_cases)
                    acting = acting.replace("var selectedActing = Array.Empty<string>();", "var selectedActing = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            acting + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.moving_connections:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                moving = Path(__file__).with_name("verify_moving_connections.script").read_text()
                if args.moving_cases:
                    values = ", ".join(json.dumps(case) for case in args.moving_cases)
                    moving = moving.replace("var selectedMoving = Array.Empty<string>();", "var selectedMoving = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            moving + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.held_connections:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                placement = Path(__file__).with_name("verify_body_connection_placement.script").read_text()
                held = Path(__file__).with_name("verify_held_connections.script").read_text()
                if args.held_cases:
                    values = ", ".join(json.dumps(case) for case in args.held_cases)
                    held = held.replace("var selectedHeld = Array.Empty<string>();", "var selectedHeld = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            placement + held + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.connection_lifecycle:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                placement = Path(__file__).with_name("verify_body_connection_placement.script").read_text()
                lifecycle = Path(__file__).with_name("verify_body_connection_lifecycle.script").read_text()
                if args.lifecycle_cases:
                    values = ", ".join(json.dumps(case) for case in args.lifecycle_cases)
                    lifecycle = lifecycle.replace("var selectedLifecycles = Array.Empty<string>();", "var selectedLifecycles = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            placement + lifecycle +
                            "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.connection_parts or args.target_parts:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                parts = Path(__file__).with_name("verify_body_connection_parts.script").read_text()
                if args.source_cases:
                    values = ", ".join(json.dumps(case) for case in args.source_cases)
                    parts = parts.replace("var selectedSources = Array.Empty<string>();", "var selectedSources = new[] { " + values + " };")
                if args.target_parts:
                    parts = parts.replace("const bool targetParts = false;", "const bool targetParts = true;")
                if args.target_transition:
                    parts = parts.replace('const string targetTransition = "";', 'const string targetTransition = ' + json.dumps(args.target_transition) + ';')
                if args.target_motion:
                    parts = parts.replace('const string targetMotion = "";', 'const string targetMotion = ' + json.dumps(args.target_motion) + ';')
                if args.organ_rank:
                    parts = parts.replace("const int tailRank = 6;", "const int tailRank = " + str(args.organ_rank) + ";")
                if args.target_cases:
                    values = ", ".join(json.dumps(case) for case in args.target_cases)
                    parts = parts.replace("var selectedTargets = Array.Empty<string>();", "var selectedTargets = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            parts + "\n}\n" +
                            connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.equipment_occlusion:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                occlusion = Path(__file__).with_name("verify_native_equipment_occlusion.script").read_text()
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            occlusion + "\n}\n" + connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.equipment:
                connections = Path(__file__).with_name("verify_body_connections.script").read_text()
                placement = Path(__file__).with_name("verify_body_connection_placement.script").read_text()
                equipment = Path(__file__).with_name("verify_body_equipment.script").read_text()
                if args.armor_only:
                    equipment = equipment.replace("var armourOnly = false;", "var armourOnly = true;")
                    if args.native:
                        equipment = Path(__file__).with_name("verify_native_armor_pixels.script").read_text() + "\nawait VerifyNativeArmorPixels();\n" + equipment
                if args.equipment_items:
                    values = ", ".join(json.dumps(item) for item in args.equipment_items)
                    equipment = equipment.replace("var selectedItems = Array.Empty<string>();", "var selectedItems = new[] { " + values + " };")
                fragment = (connections.split("// BODY_CONNECTION_SCENARIOS_BEGIN", 1)[0] +
                            placement + equipment + "\n}\n" +
                            connections.split("// BODY_CONNECTION_CLEANUP_BEGIN", 1)[1])
            elif args.connections:
                fragment = Path(__file__).with_name("verify_body_connections.script").read_text()
                fragment = fragment.replace("    // BODY_CONNECTION_SCENARIOS_BEGIN", Path(__file__).with_name("verify_body_connection_placement.script").read_text() + "\n    // BODY_CONNECTION_SCENARIOS_BEGIN", 1)
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
            if args.parallel_occupancy:
                fragment = fragment.replace("const bool verifyParallelOccupancy = false;", "const bool verifyParallelOccupancy = true;")
            if args.film_relations:
                film = Path(__file__).with_name("record_body_connections.script").read_text()
                if args.target_parts:
                    film = film.replace('const string filmTargetSuffix = "";', "const string filmTargetSuffix = " + json.dumps(args.target_cases[0].replace(":", ".")) + ";")
                helpers = helpers.replace("async Task Fire(", film + "\nasync Task Fire(", 1)
                helpers = helpers.replace("async Task Advance(", "bool filmCapturing = false;\n\nasync Task Advance(", 1)
                helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                    'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
                helpers = helpers.replace("    if (aggro)\n", "    await StartRelationFilm(d, a, target);\n    if (aggro)\n", 1)
                helpers = helpers.replace("    if (!record) return;", """    if (filmRecording != null)
    {
        if (filmMod != null)
        {
            int beforeFilm = (int)Field(filmMod, "_godGpuApplied");
            await Game.WaitUntil(() => (int)Field(filmMod, "_godGpuApplied") >= beforeFilm + 3, 1800);
        }
        return;
    }
    if (!record) return;""", 1)
                fragment = fragment.replace("return new { passed = true,", "filmSceneCompleted = true;\nreturn new { passed = true,")
                fragment = fragment.replace('    Config.setWorldSpeed("x1", false);\n}',
                    '    Config.setWorldSpeed("x1", false);\n    await FinishRelationFilm();\n}')
            script.write_text(helpers + fragment)
        write_json(folder / "result.json", dict(passed=False, status="running"))
        result = baseline.call("run", str(script), "180000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    result["checks"] = json.loads((folder / "checks.json").read_text())["checks"]
    write_json(folder / "result.json", result)
    print(json.dumps(dict(passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise SystemExit(1)
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
