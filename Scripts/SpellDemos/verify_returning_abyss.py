#!/usr/bin/env python3
"""观察归墟开口的实际散件、交锋和两种视角。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("materials", "cancel", "combat", "save", "cold", "film"), default="materials")
    parser.add_argument("--view", choices=("native", "worldfall"), default="native")
    parser.add_argument("--saved", type=Path)
    args = parser.parse_args()
    if args.section == "cold" and args.saved is None:
        parser.error("cold 需要 --saved 指向 save 的输出目录")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f"returning-abyss-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    scripts = Path(__file__).parent
    helpers = (scripts / "verify_body_footwork.script").read_text().split("var bindingType =", 1)[0]
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.ReturningAbyss")')
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
        shared = shared.replace("float targetX = 69", "float targetX = 69.5f").replace("Place(victim, targetX)", "Place(victim, targetX, 72.5f)")
        shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                                'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.ReturningAbyss.Mana") ?? '
                                'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.ReturningAbyss.Mana")')
        shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.view}";')
        shared = shared.replace("67.7f, 72f", "69.5f, 72.5f").replace(
            "        Tooltip.blockTooltips(.25f);", "        WorldTip.hideNow();\n        Tooltip.blockTooltips(.25f);")
        script = helpers + "\n" + shared + "\n" + (scripts / "verify_returning_abyss_scene.script").read_text()
    phase = "materials" if args.section in ("materials", "cancel") else "combat" if args.section == "film" else args.section
    if phase == "materials":
        script += f'\nconst bool interrupt = {str(args.section == "cancel").lower()};\n'
    if phase == "combat":
        script += f'\nconst bool record = {str(args.section == "film").lower()};\n'
    if phase in ("save", "cold"):
        script += "\n" + (scripts / "verify_returning_abyss_archive.script").read_text()
    path = output / "input.script"
    path.write_text(script + "\n" + (scripts / f"verify_returning_abyss_{phase}.script").read_text())
    try:
        result = baseline.call("run", str(path), "125000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"归墟开口复验结果位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.section == "film":
        encode(output.parent, dict(key=output.name, name="归墟开口", label="正式施放", variant=1,
                                   trajectory="vortex", modifiers=[],
                                   caption="外缘交锋、原件拉伸、喉口消解及余料退场"), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("归墟开口未通过")


if __name__ == "__main__":
    main()
