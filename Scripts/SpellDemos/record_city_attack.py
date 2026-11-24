#!/usr/bin/env python3
"""从正式编辑的城域改造实录原版交锋。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import encode, write_json
from verify_weapon_flight_archive import FIXTURE, PROJECT, SCRIPTS, world_helpers

CASES = {
    "metal": ("剑冢开天", 2.4),
    "earth": ("山河叩首", 2.2),
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--case", choices=CASES, required=True)
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请用独立原版游戏的 dev.py exec 录制")

    import baseline_scenarios as baseline

    name, size = CASES[args.case]
    output = Path(tempfile.mkdtemp(prefix=f"city-attack-{args.case}-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    print(output, flush=True)
    draft = PROJECT / "Doc/技能系统全面重构/构造输入" / (name + ".json")
    (output / "source-definition.json").write_bytes(draft.read_bytes())
    helpers = world_helpers(output, PROJECT / FIXTURE)
    helpers = helpers.replace("ConstructedAbilityService.CastEnded += Ended;", "")
    helpers = helpers.replace("ConstructedAbilityService.ContactConfirmed += Hit;", "")
    camera = (SCRIPTS / "verify_wind_combat.script").read_text()
    camera = camera[camera.index("void HideCameraOverlays()"):]
    input_script = output / "input.script"
    input_script.write_text(
        "using Cultiway.Const;\nusing Cultiway.Core.Components;\n" + helpers
        + f'\nconst string cityCase = "{args.case}";\nconst float resize = {size}f;\n'
        + camera + (SCRIPTS / "city_film_scene.script").read_text()
        + Path(__file__).with_suffix(".script").read_text(), encoding="utf-8")
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call("run", str(input_script), "145000")
    except RuntimeError as error:
        write_json(output / "result.json", dict(passed=False, error=str(error)))
        raise
    write_json(output / "result.json", result)
    encode(output.parent, dict(key=output.name, name=name, title=result["title"], label="城区实录",
                               variant=1, trajectory="", modifiers=[]), keep_frames=True)


if __name__ == "__main__":
    main()
