#!/usr/bin/env python3
"""用现成地图复验 9.05 的施放与命中、9.06 的粒子来源与寿命。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, version_stamp, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("weapon", "steam", "surface", "terrain"), required=True)
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        raise RuntimeError("请使用 develop 的 dev.py exec 运行")
    import baseline_scenarios as baseline

    connected = baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f"material-feedback-{args.section}-",
                                   dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    directory = Path(__file__).parent
    read = lambda name: (directory / name).read_text()
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    if args.section == "steam":
        helper = read("verify_cast_cleanup.script").split("void Reset()", 1)[0]
        helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/cast-cleanup")', json.dumps(str(folder)))
        helper = helper.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
        helper = helper.replace('JsonConvert.SerializeObject(new { checks }, Formatting.Indented));',
                                'JsonConvert.SerializeObject(new { checks }, Formatting.Indented));\n'
                                '    if (!ok) throw new InvalidOperationException(name);')
    else:
        helper = read("verify_source_sessions.script").split("var mutations =", 1)[0]
        helper = ("using Cultiway.Core.SkillLibV3.Utils;\nusing System.Collections;\n" + helper)
        helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/source-sessions")', json.dumps(str(folder)))
        helper = helper.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
        helper = helper.replace('    if (!pass) throw', '    File.WriteAllText(Path.Combine(folder, "checks.json"), JsonConvert.SerializeObject(checks));\n    if (!pass) throw')
        helper = helper.replace('await Boundary();\nvar stats', '''await Boundary();
for (int x = 61; x <= 75; x++)
for (int y = 69; y <= 75; y++)
{
    var tile = World.world.GetTileSimple(x, y);
    tile.Height = 140;
    tile.setTileTypes(TileLibrary.soil_low, TopTileLibrary.grass_low);
}
var stats''')
    camera = read("verify_appearance_camera.script") + read("verify_visual_cleanup_camera.script")
    script = folder / "input.script"
    tail = read(f"verify_material_feedback_{args.section}.script")
    tail = tail.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    script.write_text(helper + '\n' + camera + '\n' + tail)
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected, section=args.section))
    print(str(folder), flush=True)
    try:
        result = baseline.call("run", str(script), "190000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    write_json(folder / "result.json", result)
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise RuntimeError(f"检查未通过，现场位于 {folder}")


if __name__ == "__main__":
    main()
