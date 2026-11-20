#!/usr/bin/env python3
"""在同一实际狼形演示吻端喷吐、雷角刺和四足扑击。"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("attacks", "bounds", "save"), default="attacks")
    parser.add_argument("--record", choices=("native", "worldfall"))
    parser.add_argument("--part", choices=("breath", "horn", "pounce"))
    args = parser.parse_args()
    if args.section != "attacks" and (args.record or args.part):
        parser.error("录像和分招复验使用 attacks 场景")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix=f"beast-attacks-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    helpers = (scripts / "verify_body_organs.script").read_text().split("var editor =", 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-organs")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1")))
    helpers = helpers.replace('contact.AbilityId.StartsWith("test.body-organs.")',
                              'contact.AbilityId.StartsWith("Cultiway.Body.Beast") || contact.AbilityId == "Cultiway.FoxfireBreath"')
    helpers = helpers.replace("var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild",
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    camera = (scripts / "verify_wind_combat.script").read_text().split("object cameraSettings = null;", 1)[1]
    camera = "object cameraSettings = null;" + camera
    camera = camera.replace("    ConstructedAbilityService.CastStarted -= Started;\n", "")
    camera = camera.replace("    ConstructedAbilityService.CastEnded -= Ended;\n", "")
    camera_position = "    World.world.camera.transform.position = new Vector3(65.7f, 72f, 0);\n"
    if args.record == "worldfall":
        camera = camera.replace("x = 61; x <= 76", "x = 58; x <= 80").replace("y = 66; y <= 79", "y = 63; y <= 84")
        camera = camera.replace("tile.setTileTypes(TileLibrary.soil_low, null, true);", "tile.setTileTypes(tile.main_type, null, true);")
        camera = camera.replace("    World.world.camera.transform.position = new Vector3(67.7f, 72f, 0);\n", camera_position)
    else:
        start = camera.index("    // 复用原测试场")
        end = camera.index("    MoveCamera.instance.forceZoom", start)
        camera = camera[:start] + camera_position + camera[end:]
    camera = camera.replace('type.GetField("_godTip", F).SetValue(mod, .7f);', '''type.GetField("_godTip", F).SetValue(mod, .68f);
        type.GetField("_godTurn", F).SetValue(mod, 2.70f);
        type.GetField("_godTurnRate", F).SetValue(mod, 0f);
        int applied = (int)Get("_godGpuApplied");
        await Game.WaitUntil(() => (int)Get("_godGpuApplied") >= applied + 4, 600);''')
    camera = camera.replace('    object sampler = Get("_sampler");',
                            '    await Game.WaitUntil(() => Time.unscaledTime >= (float)Get("_godToastUntil"), 600);\n'
                            '    object sampler = Get("_sampler");')
    imports = "using Friflo.Engine.ECS;\nusing Cultiway.Core.SkillLibV3;\n"
    flags = (f'const string section = "{args.section}";\nconst string recordView = "{args.record or ""}";\n'
             f'const string only = "{args.part or ""}";')
    path = output / "input.script"
    path.write_text("\n".join([imports + helpers, flags, camera, (scripts / "verify_beast_attacks.script").read_text()]))
    try:
        result = baseline.call("run", str(path), "240000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"妖兽攻击复验位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.record:
        movies = []
        for case in result["cases"]:
            key = case["key"]
            encode(output, dict(key=key, name=case["name"], label="正式施放", variant=1,
                                trajectory="actual beast anatomy", modifiers=[], caption=case["caption"]), keep_frames=True)
            movies.append(f"file '{key}/demo.mp4'\n")
        (output / "montage.ffconcat").write_text("ffconcat version 1.0\n" + "".join(movies))
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i",
                        "montage.ffconcat", "-c", "copy", "-movflags", "+faststart", "all.mp4"], cwd=output, check=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("妖兽攻击未通过")


if __name__ == "__main__":
    main()
