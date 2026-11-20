#!/usr/bin/env python3
"""复验同一张符箓正式消耗、真实接触留印和有限传递。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("relay", "miss"), default="relay")
    parser.add_argument("--record", choices=("native", "worldfall"))
    parser.add_argument("--save", action="store_true", help="保存真实消耗之后的一份世界")
    args = parser.parse_args()
    if args.record and args.section != "relay":
        parser.error("连续录像使用完整的传递流程")
    if not os.getenv("CULTIWAY_DEV_ACTIVE"):
        raise SystemExit("请通过 dev.py exec 运行")

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix=f"seal-relay-{args.section}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    script = (scripts / "verify_body_footwork.script").read_text().split("var bindingType =", 1)[0]
    script = script.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    script = script.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                            json.dumps(str(PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1")))
    script = script.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.SealRelay")')
    script = script.replace("var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild",
                            'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    visual = (scripts / "verify_wind_combat.script").read_text()
    visual = visual[visual.index("object cameraSettings = null;"):]
    visual = visual.replace("    ConstructedAbilityService.CastStarted -= Started;\n", "")
    start = visual.index("    // 复用原测试场")
    end = visual.index("    MoveCamera.instance.forceZoom", start)
    visual = visual[:start] + "    World.world.camera.transform.position = new Vector3(70f, 74f, 0);\n" + visual[end:]
    visual = visual.replace('type.GetField("_godTip", F).SetValue(mod, .7f);', '''type.GetField("_godTip", F).SetValue(mod, .48f);
        type.GetField("_godTurn", F).SetValue(mod, 2.82f);
        type.GetField("_godTurnRate", F).SetValue(mod, 0f);
        int applied = (int)Get("_godGpuApplied");
        await Game.WaitUntil(() => (int)Get("_godGpuApplied") >= applied + 4, 600);''')
    visual = visual.replace('    object sampler = Get("_sampler");',
                            '    await Game.WaitUntil(() => Time.unscaledTime >= (float)Get("_godToastUntil"), 600);\n'
                            '    object sampler = Get("_sampler");')
    imports = ("using Friflo.Engine.ECS;\nusing Cultiway.Utils;\nusing Cultiway.Core.Components;\n"
               "using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n"
               "using Cultiway.Core.SkillLibV3.Utils;\n")
    path = output / "input.script"
    path.write_text("\n".join([imports + script, f'const string section = "{args.section}";',
                               f'const string recordView = "{args.record or ""}";\nconst bool save = {str(args.save).lower()};',
                               visual, (scripts / "verify_seal_relay.script").read_text()]))
    try:
        result = baseline.call("run", str(path), "145000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"连印走符复验位于 {output}") from error
    write_json(output / "result.json", result)
    if result.get("passed") and args.record:
        encode(output.parent, dict(key=output.name, name="连印走符", label="符箓正式受理", variant=1,
                                  trajectory="actual contact relay", modifiers=[],
                                  caption="折纸接触、三次朱印及余力递减"), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise SystemExit("连印走符未通过")


if __name__ == "__main__":
    main()
