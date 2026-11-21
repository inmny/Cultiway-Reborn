#!/usr/bin/env python3
"""复用旧测试世界，在实际雷击、水与岩体上核对 9.04。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, version_stamp, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--section", choices=("materials", "seams"), required=True)
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        raise RuntimeError("请使用 develop 的 dev.py exec 运行")
    import baseline_scenarios as baseline

    connected = baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f"visual-cleanup-{args.section}-",
                                   dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    directory = Path(__file__).parent
    read = lambda name: (directory / name).read_text()
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    camera = read("verify_appearance_camera.script") + read("verify_visual_cleanup_camera.script")
    if args.section == "seams":
        helper = read("verify_optical_layers.script").split("try\n{", 1)[0]
        helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/optical-layers")', json.dumps(str(folder)))
        camera = ""
        body = read("verify_visual_cleanup_seams.script")
    else:
        helper = ("using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Utils;\n"
                  "using Cultiway.Core.SkillLibV3.Blueprints;\nusing Friflo.Engine.ECS;\n")
        helper += read("verify_body_footwork.script").split("var bindingType =", 1)[0]
        helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
        helper = helper.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
        body = read("verify_form_visibility.script")
        start = body.index("var samples=new[]{")
        end = body.index("};", start) + 2
        samples = [line.rstrip(",") for line in body[start:end].splitlines()
                   if any(key in line for key in ('"water_curtain"', '"earth_ridge_0"', '"lightning_spear"'))]
        body = body[:start] + "var samples=new[]{\n" + ",\n".join(samples) + "\n};" + body[end:]
        body = body.replace('(Name:"near",Zoom:5f),(Name:"mid",Zoom:16f),(Name:"far",Zoom:22f)',
                            '(Name:"near",Zoom:3f),(Name:"far",Zoom:22f)')
        body = body.replace('sizes[0]<sizes[1]&&sizes[1]<sizes[2],sample.Form+"三个实际镜头距离",sizes',
                            'sizes[0]<sizes[1],sample.Form+"实际近远镜头",sizes')
        body = body.replace('        if(sample.Arrival)\n        {\n            await Advance(.35f);', '''        await FrameCleanupSide(sample.Form + "-side", center, 5f, -.25f);
        if (sample.Form != "lightning_spear")
            await FrameCleanupSide(sample.Form + "-reverse", center, 5f, .25f);
        if(sample.Arrival)
        {
            await Advance(.35f);''')
    script = folder / "input.script"
    script.write_text(helper + '\n' + camera + '\n' + body)
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected, section=args.section))
    print(str(folder), flush=True)
    try:
        result = baseline.call("run", str(script), "210000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    write_json(folder / "result.json", result)
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not result.get("passed"):
        raise RuntimeError(f"检查未通过，现场位于 {folder}")


if __name__ == "__main__":
    main()
