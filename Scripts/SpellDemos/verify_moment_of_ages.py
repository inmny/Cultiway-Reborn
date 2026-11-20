#!/usr/bin/env python3
"""复验刹那千秋的原人物、原地物和各轮真实结果。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("mechanics", "interrupt", "save", "cold", "film"), default="mechanics")
    parser.add_argument("--view", choices=("native", "worldfall"), default="native")
    parser.add_argument("--saved", type=Path)
    args = parser.parse_args()
    if args.section == "cold" and args.saved is None:
        parser.error("cold 需要 --saved 指向 save 的输出目录")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f"moment-of-ages-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    scripts = Path(__file__).parent
    helpers = (scripts / "verify_body_footwork.script").read_text().split("var bindingType =", 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.MomentOfAges")')
    helpers = helpers.replace("var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild",
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              "Cultiway.Core.Libraries.StatusEffectAsset.StartBuild")
    if args.section == "cold":
        script = helpers.split("Game.Resume();", 1)[0]
        script += f"string saved = {json.dumps(str(args.saved.resolve()))};\n"
    else:
        shared = (scripts / "verify_wind_combat.script").read_text()
        inputs = json.dumps(str(PROJECT / "Doc/技能系统全面重构/构造输入"))
        shared = shared.replace('const string inputRoot = "";', f"const string inputRoot = {inputs};")
        shared = shared.replace("float targetX = 69", "float targetX = 69.5f")
        shared = shared.replace("Place(victim, targetX)", "Place(victim, targetX, 72.5f)")
        shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                                'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.MomentOfAges.Mana") ?? '
                                'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.MomentOfAges.Mana")')
        shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.view}";')
        start, end = shared.index("    // 复用原测试场"), shared.index("    World.world.camera")
        shared = shared[:start] + shared[end:]
        shared = shared.replace("67.7f, 72f", "69.8f, 72.5f").replace(
            "        Tooltip.blockTooltips(.25f);", "        WorldTip.hideNow();\n        Tooltip.blockTooltips(.25f);")
        script = helpers + "\n" + shared + "\n" + (scripts / "verify_moment_of_ages_scene.script").read_text()
    phase = "mechanics" if args.section in ("mechanics", "interrupt") else args.section
    if phase == "mechanics":
        script += f'\nconst bool partly = {str(args.section == "interrupt").lower()};\n'
    if phase in ("save", "cold"):
        script += "\n" + (scripts / "verify_moment_of_ages_archive.script").read_text()
    path = output / "input.script"
    path.write_text(script + "\n" + (scripts / f"verify_moment_of_ages_{phase}.script").read_text())
    try:
        result = baseline.call("run", str(path), "125000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"刹那千秋复验结果位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.section == "film":
        encode(output.parent, dict(key=output.name, name="刹那千秋", label="正式施放", variant=1,
                                   trajectory="periods", modifiers=[],
                                   caption="真实护持、原甲剥落、人物老化、两轮枯荣和石屋风化"), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("刹那千秋未通过")


if __name__ == "__main__":
    main()
