#!/usr/bin/env python3
"""验证五相换城的真实承力、余量及原法宝承载。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("district", "carrier", "reordered", "editor"), default="district")
    parser.add_argument("--record", choices=("native", "worldfall"))
    args = parser.parse_args()
    if args.record and args.section == "editor":
        parser.error("编辑窗口输出截图")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix=f"five-phase-district-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    helpers = (scripts / "verify_body_footwork.script").read_text().split("var bindingType =", 1)[0]
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Demo.FivePhaseDistrict")')
    helpers = helpers.replace("var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild",
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    shared = (scripts / "verify_wind_combat.script").read_text()
    inputs = json.dumps(str(PROJECT / "Doc/技能系统全面重构/构造输入"))
    shared = shared.replace('const string inputRoot = "";', f"const string inputRoot = {inputs};")
    shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.record or ""}";')
    shared = shared.replace('if (fact.SourceId != source.id) return;',
                            'if (fact.SourceId != source.id && fact.SourceId != victim.id) return;')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.Wind.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")')
    start = shared.index("    // 复用原测试场")
    stop = shared.index("    MoveCamera.instance.forceZoom", start)
    shared = shared[:start] + "    World.world.camera.transform.position = new Vector3(70f, 72f, 0);\n" + shared[stop:]
    angle = '''type.GetField("_godTip", F).SetValue(mod, .39f);
        type.GetField("_godTurn", F).SetValue(mod, -.32f);
        type.GetField("_godTurnRate", F).SetValue(mod, 0f);
        int applied = (int)Get("_godGpuApplied");
        await Game.WaitUntil(() => (int)Get("_godGpuApplied") >= applied + 4, 600);'''
    shared = shared.replace('type.GetField("_godTip", F).SetValue(mod, .7f);', angle)
    shared = shared.replace('    object sampler = Get("_sampler");',
                            '    await Game.WaitUntil(() => Time.unscaledTime >= (float)Get("_godToastUntil"), 600);\n'
                            '    object sampler = Get("_sampler");')
    shared = shared.replace("        Tooltip.blockTooltips(.25f);",
                            "        WorldTip.hideNow();\n        Tooltip.blockTooltips(.25f);")
    imports = "using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\nusing Cultiway.Content.Extensions;\n"
    if args.section == "editor":
        imports += "using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n"
        editor = (scripts / "verify_five_phases_editor.script").read_text().split("\ntry\n", 1)[0]
        editor = editor.replace('Spell("五相周流")', 'Spell("五相换城")')
        editor = editor.replace('Click("Option1")', 'Click("Option2")')
        editor = editor.replace('Draft.Root.Children[0].Activation', 'Draft.Root.Children[0].Children[0].Activation')
        editor = editor.replace('Row("ActivationTrigger").transform', 'Row("KeepSourcePosition").transform')
        editor = editor.replace('Shot("handoff-"', 'Shot("district-handoff-"')
        scenario = editor + "\n" + (scripts / "verify_five_phase_district_editor.script").read_text()
    else:
        scenario = (scripts / "verify_five_phase_district.script").read_text()
    path = output / "input.script"
    path.write_text("\n".join([imports + helpers, shared, f'const string section = "{args.section}";', scenario]))
    try:
        result = baseline.call("run", str(path), "145000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"五相换城复验位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.record:
        encode(output.parent, dict(key=output.name, name=result["name"], label="正式施放", variant=1,
                                  trajectory="district material handoff", modifiers=[],
                                  caption=result["caption"]), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("五相换城未通过")


if __name__ == "__main__":
    main()
