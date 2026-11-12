#!/usr/bin/env python3
"""复用已有双人世界，专项核对真实兵器架挡；不遍历身体与装备。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, version_stamp, write_json


def main():
    """共用现成准备和正式施放入口，记录本轮输入及实际结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", action="store_true", help="使用独立原版游戏")
    parser.add_argument("--film", action="store_true", help="录制指定单场正常关系及恢复")
    selection = parser.add_mutually_exclusive_group()
    selection.add_argument("--case", nargs="+", choices=["normal", "miss", "replace", "deflect", "deflect-reach", "deflect-withdraw", "passive", "passive-busy", "passive-replace", "passive-world", "disarm", "disarm-cursed", "disarm-active", "disarm-save", "disarm-save-missing"], help="只核对本次受影响的场景")
    selection.add_argument("--editor", action="store_true", help="只验证拨压的实际编辑、替换保存及重开")
    selection.add_argument("--restart", action="store_true", help="只在新进程读回既有脱手存档并核对换世界清理")
    selection.add_argument("--disarm-editor", action="store_true", help="只验证脱手的实际编辑、保存及中英文显示")
    args = parser.parse_args()
    if args.film and (not args.case or len(args.case) != 1 or args.case[0] not in {"normal", "deflect", "passive", "disarm-active"}):
        parser.error("--film 需要单独指定 normal、deflect、passive 或 disarm-active")
    if args.native:
        install = PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway"
        os.environ["CULTIWAY_BASELINE_ROOT"] = str(install)
        os.environ["CULTIWAY_BASELINE_GAME"] = str(install / "game/worldbox")
        import baseline_scenarios as baseline
    else:
        import verify_view_switch
        baseline = verify_view_switch.baseline
    folder = PROJECT / "artifacts/skill_rebuild" / ("weapon-connections-native" if args.native else "weapon-connections")
    if args.restart:
        if not (folder / "cases-disarm-save/restart-expected.json").exists():
            raise RuntimeError("请先在对应视图运行 --case disarm-save，保留正式世界存档")
        folder /= "restart"
    if args.case:
        folder /= "cases-" + "-".join(args.case)
    elif args.editor or args.disarm_editor:
        folder /= "disarm-editor" if args.disarm_editor else "editor"
    if args.film:
        folder /= "film"
    folder.mkdir(parents=True, exist_ok=True)
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for p in (PROJECT / "Source").rglob("*.cs")):
        raise RuntimeError("游戏仍在使用本轮修改之前的代码，请先退出这个独立游戏")
    if args.film:
        # 同一入口重录时清掉旧帧和旧结果，准备失败也不能留下上次成功的现场。
        shutil.rmtree(folder / "frames", ignore_errors=True)
        for name in ("recording.json", "checks.json", "result.json", "error.json"):
            (folder / name).unlink(missing_ok=True)
    helpers = Path(__file__).with_name("verify_body_footwork.script").read_text().split("var bridge =", 1)[0]
    helpers = helpers.replace("artifacts/skill_rebuild/body-footwork", str(folder.relative_to(PROJECT)))
    script = folder / "input.script"
    fragment = Path(__file__).with_suffix(".script").read_text()
    worldfall = '\nvar worldfall = AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType("FirstPerson.WorldBoxMod", false)).FirstOrDefault(t => t != null);\n'
    fragment = fragment.replace("\ntry\n{", worldfall + "\ntry\n{", 1)
    if args.case:
        fragment = fragment.replace('new[] { "normal", "miss", "replace" }',
                                    "new[] { " + ", ".join(json.dumps(case) for case in args.case) + " }")
    if args.case and any(case.startswith("passive") for case in args.case):
        if not all(case.startswith("passive") for case in args.case):
            raise ValueError("普通持物专项请单独指定，避免混用准备流程")
        fragment = fragment.split("\ntry\n{", 1)[0]
        fragment += Path(__file__).with_name("verify_passive_weapons.script").read_text().replace(
            'new[] { "passive", "passive-busy", "passive-replace" }',
            "new[] { " + ", ".join(json.dumps(case) for case in args.case) + " }")
    if args.case and any(case.startswith("disarm") for case in args.case):
        if not all(case.startswith("disarm") for case in args.case):
            raise ValueError("脱手专项请单独指定，避免混用准备流程")
        fragment = fragment.split("\ntry\n{", 1)[0]
        fragment += Path(__file__).with_name("verify_weapon_disarm.script").read_text().replace(
            'new[] { "disarm", "disarm-cursed" }',
            "new[] { " + ", ".join(json.dumps(case) for case in args.case) + " }")
    if args.editor or args.disarm_editor:
        helpers = Path(__file__).with_name("verify_body_connection_editor_ui.script").read_text().split("\ntry\n{", 1)[0]
        helpers = helpers.replace("artifacts/skill_rebuild/body-carrying-editor/ui", str(folder.relative_to(PROJECT)))
        fragment = Path(__file__).with_name("verify_weapon_disarm_editor.script" if args.disarm_editor else "verify_weapon_connection_editor.script").read_text()
    if args.restart:
        helpers = helpers.split("Game.Resume();", 1)[0]
        fragment = Path(__file__).with_name("verify_weapon_disarm_restart.script").read_text()
    if args.film:
        # 固定现成人形的起势朝向，避免随机出生朝向改变普通持物表面。
        if not args.native:
            helpers = helpers.replace("    Place(a, x);\n    return a;", "    Place(a, x);\n    a.flip = false;\n    return a;", 1)
        if not args.native and args.case == ["passive"]:
            # 沿游戏已有的真实部位瞄准适配，不用固定手高猜普通持物的位置。
            fragment = fragment.replace("var definition = Definition(sample, true);",
                "var definition = Definition(sample, true, BodyActionKind.Deflect);\n        definition.Body.MatchDistance = true;", 1)
        film = Path(__file__).with_name("record_body_connections.script").read_text()
        helpers = helpers.replace("async Task Fire(",
            '/// <summary>保存真实接触点的三个坐标。</summary>\n/// <param name="point">实际空间点。</param>\n'
            'float[] P(Vector3 point) => new[] { point.x, point.y, point.z };\n' + film + "\nasync Task Fire(", 1)
        helpers = helpers.replace("async Task Advance(", "bool filmCapturing = false;\n\nasync Task Advance(", 1)
        helpers = helpers.replace("    Config.paused = false;\n    double until",
            "    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until", 1)
        helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
            'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
        helpers = helpers.replace("    if (aggro)\n", "    await StartRelationFilm(d, a, target);\n    if (aggro)\n", 1)
        fragment = fragment.replace("return new { passed = true,", "filmSceneCompleted = true;\n    return new { passed = true,", 1)
        # 场景暂停与三维取样共用同一世界，切换暂停前先让当前取样结束。
        fragment = fragment.replace("Config.paused =", "await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused =")
        fragment = fragment.replace("    ControllableUnit.clear(false);", "    ControllableUnit.clear(false);\n    await FinishRelationFilm();", 1)
    script.write_text(helpers + fragment)
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected,
                                            cases=["restart"] if args.restart else ["editor"] if args.editor or args.disarm_editor else args.case or ["normal", "miss", "replace"]))
    try:
        result = baseline.call("run", str(script), "180000")
        write_json(folder / "result.json", result)
        (folder / "error.json").unlink(missing_ok=True)
        print(json.dumps(result, ensure_ascii=False))
    except RuntimeError as error:
        (folder / "error.json").write_text(str(error))
        write_json(folder / "result.json", dict(passed=False, error=str(folder / "error.json")))
        raise RuntimeError("兵器专项未完成，详见 " + str(folder / "error.json")) from None


if __name__ == "__main__":
    main()
