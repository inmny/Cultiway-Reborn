#!/usr/bin/env python3
"""验证明壳承阳的真实蓄力、裂口、命中和恢复。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("behavior", "miss", "editor", "film"), default="behavior")
    parser.add_argument("--view", choices=("native", "worldfall"), default="native")
    parser.add_argument("--city", action="store_true", help="在城域现场复用本招的正式施放")
    args = parser.parse_args()
    if args.city and (args.section != "film" or args.view != "native"):
        parser.error("--city 只用于原版的主术录像")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix=f"solar-reservoir-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    helpers = (scripts / "verify_body_footwork.script").read_text().split("var bindingType =", 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Demo.SolarReservoir")')
    helpers = helpers.replace("var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild",
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    shared = (scripts / "verify_wind_combat.script").read_text()
    inputs = json.dumps(str(PROJECT / "Doc/技能系统全面重构/构造输入"))
    shared = shared.replace('const string inputRoot = "";', f"const string inputRoot = {inputs};")
    shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.view}";')
    shared = shared.replace('if (fact.SourceId != source.id) return;',
                            'if (fact.SourceId != source.id && fact.SourceId != victim.id) return;')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.Wind.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")')
    start = shared.index("    // 复用原测试场")
    stop = shared.index("    MoveCamera.instance.forceZoom", start)
    shared = shared[:start] + "    World.world.camera.transform.position = new Vector3(73f, 72f, 0);\n" + shared[stop:]
    angle = '''type.GetField("_godTip", F).SetValue(mod, .45f);
        type.GetField("_godTurn", F).SetValue(mod, -.34f);
        type.GetField("_godTurnRate", F).SetValue(mod, 0f);
        int applied = (int)Get("_godGpuApplied");
        await Game.WaitUntil(() => (int)Get("_godGpuApplied") >= applied + 4, 600);'''
    shared = shared.replace('type.GetField("_godTip", F).SetValue(mod, .7f);', angle)
    shared = shared.replace('    object sampler = Get("_sampler");',
                            '    await Game.WaitUntil(() => Time.unscaledTime >= (float)Get("_godToastUntil"), 600);\n'
                            '    object sampler = Get("_sampler");')
    shared = shared.replace("        Tooltip.blockTooltips(.25f);",
                            "        WorldTip.hideNow();\n        Tooltip.blockTooltips(.25f);")
    flags = f'const string section = "{args.section}";\n'
    path = output / "input.script"
    if args.section == "editor":
        helpers = "using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n" + helpers
        editor = (scripts / "verify_five_phases_editor.script").read_text().split("\ntry\n", 1)[0]
        editor = editor.replace('Spell("五相周流")', 'Spell("明壳承阳")')
        editor = editor.replace('Click("Option1")', 'Click("Option2")')
        editor = editor.replace('Draft.Root.Children[0]', 'Draft.Root.Children[1]')
        editor = editor.replace('Row("ActivationTrigger").transform', 'Row("InheritAbsorbedForce").transform')
        editor = editor.replace('Shot("handoff-"', 'Shot("solar-reservoir-"')
        scenario = editor + "\n" + (scripts / "verify_solar_reservoir_editor.script").read_text()
    else:
        scenario = (scripts / "verify_solar_reservoir.script").read_text()
    path.write_text("\n".join([helpers, shared, flags, f"bool cityFilm = {str(args.city).lower()};",
                              (scripts / "city_film_scene.script").read_text(), scenario]))

    try:
        result = baseline.call("run", str(path), "125000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"明壳承阳复验位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.section == "film":
        encode(output.parent, dict(key=output.name, name="明壳承阳", label="城区实录" if args.city else "正式施放", variant=1,
                                  trajectory="shell and rupture", modifiers=[],
                                  caption="种核长壳、两股来势蓄入、沿实际裂口放出并护生"), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("明壳承阳未通过")


if __name__ == "__main__":
    main()
