#!/usr/bin/env python3
"""复验法宝器口的原物接入、随动、抛出和控制归还。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("release", "interrupt", "editor", "save"), default="release")
    parser.add_argument("--record", choices=("native", "worldfall"))
    args = parser.parse_args()
    if args.record and args.section != "release":
        parser.error("连续录像使用完整的释放流程")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")
    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix=f"weapon-chamber-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))

    helpers = (scripts / "verify_body_footwork.script").read_text().split("var bindingType =", 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1")))
    helpers = helpers.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Demo.WeaponChamber")')
    helpers = helpers.replace("var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild",
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')

    shared = (scripts / "verify_wind_combat.script").read_text()
    shared = shared.replace('const string inputRoot = "";',
                            "const string inputRoot = " + json.dumps(str(PROJECT / "Doc/技能系统全面重构/构造输入")) + ";")
    shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.record or ""}";')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.Wind.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")')
    start = shared.index("    // 复用原测试场")
    end = shared.index("    MoveCamera.instance.forceZoom", start)
    shared = shared[:start] + "    World.world.camera.transform.position = new Vector3(70f, 72f, 0);\n" + shared[end:]
    shared = shared.replace('type.GetField("_godTip", F).SetValue(mod, .7f);', '''type.GetField("_godTip", F).SetValue(mod, .41f);
        type.GetField("_godTurn", F).SetValue(mod, -.42f);
        type.GetField("_godTurnRate", F).SetValue(mod, 0f);
        int applied = (int)Get("_godGpuApplied");
        await Game.WaitUntil(() => (int)Get("_godGpuApplied") >= applied + 4, 600);''')
    shared = shared.replace('    object sampler = Get("_sampler");',
                            '    await Game.WaitUntil(() => Time.unscaledTime >= (float)Get("_godToastUntil"), 600);\n'
                            '    object sampler = Get("_sampler");')

    wind = (scripts / "verify_wind_combat_carry.script").read_text()
    loose = "var droppedBy = Make(70);\n" + wind[wind.index("var groundType"):wind.index("\ntry\n")].replace("victim", "droppedBy")
    loose = loose.replace("    await Game.NextFrame();",
                          '    for (int i = 0; i < 6 && service.GetMethod("BodySelectionSurface", F)'
                          '.Invoke(null, new object[] { droppedBy }) == null; i++)\n'
                          '    { droppedBy.checkAnimationContainer(); await Advance(.065f); }\n'
                          '    await Game.NextFrame();', 1)
    five = (scripts / "verify_five_phase_district.script").read_text()
    carrier = five[five.index("AbilityCast Send("):five.index("\ntry\n")]
    carrier = carrier.replace("AbilityCast Send(", "void Send(")
    carrier = carrier.replace("Cultiway.Demo.FivePhaseDistrict.Probe.", "Cultiway.Demo.WeaponChamber.Probe.")
    carrier = carrier.replace('Name = "五相承载飞剑"', 'Name = "鸣铗承载飞剑"')
    carrier = carrier.replace("    return seen.Last().Value;\n", "")

    imports = "using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\nusing Cultiway.Content.Extensions;\n"
    if args.section == "editor":
        imports += "using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n"
        editor = (scripts / "verify_five_phases_editor.script").read_text().split("\ntry\n", 1)[0]
        editor = editor.replace('Spell("五相周流")', 'Spell("鸣铗纳锋")')
        editor = editor.replace('Row("ActivationTrigger").transform', 'Row("WeaponChamberDepth").transform')
        editor = editor.replace('Shot("handoff-"', 'Shot("weapon-chamber-"')
        scenario = [editor, (scripts / "verify_weapon_chamber_editor.script").read_text()]
    else:
        scenario = [loose, f'const string section = "{args.section}";',
                    "Vector3 PositionOf(Matrix4x4 pose) => pose.GetColumn(3);", carrier,
                    (scripts / "verify_weapon_chamber.script").read_text()]
    path = output / "input.script"
    path.write_text("\n".join([imports + helpers, shared, *scenario]))
    try:
        result = baseline.call("run", str(path), "145000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"法宝器口复验位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.record:
        encode(output.parent, dict(key=output.name, name="鸣铗纳锋", label="正式施放", variant=1,
                                  trajectory="chamber load and release", modifiers=[],
                                  caption="真器口接入原剑、夹页暂存、再开口抛出"), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("法宝器口未通过")


if __name__ == "__main__":
    main()
