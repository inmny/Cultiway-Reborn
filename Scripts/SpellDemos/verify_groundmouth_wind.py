#!/usr/bin/env python3
"""验证地口行风的受力范围、原物出口和两种实机视角。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("behavior", "variant", "film"), default="behavior")
    parser.add_argument("--view", choices=("native", "worldfall"), default="native")
    parser.add_argument("--city", action="store_true", help="复用实际城区边界，录制城内到城外的路径")
    args = parser.parse_args()
    if args.city and (args.section != "film" or args.view != "native"):
        parser.error("--city 只用于 --section film --view native")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix=f"groundmouth-wind-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    helpers = (scripts / "verify_body_footwork.script").read_text().split("var bindingType =", 1)[0]
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Demo.GroundmouthWind")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')

    shared = (scripts / "verify_wind_combat.script").read_text()
    inputs = json.dumps(str(PROJECT / "Doc/技能系统全面重构/构造输入"))
    shared = shared.replace('const string inputRoot = "";', f"const string inputRoot = {inputs};")
    shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.view}";')
    shared = shared.replace("Place(source, 64); Place(victim, targetX);", "Place(source, 64, 76); Place(victim, targetX, 76);")
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.Wind.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")')
    start = shared.index("    // 复用原测试场")
    stop = shared.index("    MoveCamera.instance.forceZoom", start)
    shared = shared[:start] + "    World.world.camera.transform.position = new Vector3(84f, 76f, 0);\n" + shared[stop:]
    angle = '''type.GetField("_godTip", F).SetValue(mod, .37f);
        type.GetField("_godTurn", F).SetValue(mod, -.44f);
        type.GetField("_godTurnRate", F).SetValue(mod, 0f);
        int applied = (int)Get("_godGpuApplied");
        await Game.WaitUntil(() => (int)Get("_godGpuApplied") >= applied + 4, 600);'''
    shared = shared.replace('type.GetField("_godTip", F).SetValue(mod, .7f);', angle)
    shared = shared.replace('    object sampler = Get("_sampler");',
                            '    await Game.WaitUntil(() => Time.unscaledTime >= (float)Get("_godToastUntil"), 600);\n'
                            '    object sampler = Get("_sampler");')

    shared = shared.replace("        Tooltip.blockTooltips(.25f);",
                            "        WorldTip.hideNow();\n        Tooltip.blockTooltips(.25f);")

    wind = (scripts / "verify_wind_combat_carry.script").read_text()
    loose = wind[wind.index("var groundType"):wind.index("\ntry\n")]
    loose = loose.replace("    await Game.NextFrame();",
                          "    for (int i = 0; i < 6 && service.GetMethod(\"BodySelectionSurface\", F)"
                          ".Invoke(null, new object[] { victim }) == null; i++)\n"
                          "    { victim.checkAnimationContainer(); await Advance(.065f); }\n"
                          "    await Game.NextFrame();", 1)
    flags = f'const string section = "{args.section}";\nbool cityFilm = {str(args.city).lower()};\n'
    path = output / "input.script"
    path.write_text("\n".join([helpers, shared, loose, flags, (scripts / "verify_groundmouth_wind.script").read_text()]))
    try:
        result = baseline.call("run", str(path), "125000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"地口行风复验位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.section == "film":
        encode(output.parent, dict(key=output.name, name="地口行风", label="城区实录" if args.city else "正式施放", variant=1,
                                  trajectory="moving channel", modifiers=[],
                                  caption="双侧地口、实物卷送、窄口抛出与自然卸压"), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("地口行风未通过")


if __name__ == "__main__":
    main()
