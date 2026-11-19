#!/usr/bin/env python3
"""核对近中远网格、原始通道及实际视点的显示选择。"""
import argparse
import json
import os
import tempfile
import shutil
import subprocess
from pathlib import Path

from common import PROJECT, caption_font, version_stamp, write_json
from benchmark_baselines import private_installation


def appearance(section):
    """沿用正式已学入口，在同一场景按三个真实距离拍现有形体。"""
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        raise RuntimeError("主体外观检查请通过 develop 的 dev.py exec 运行")
    import baseline_scenarios as baseline
    connected = baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix="form-visibility-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    world = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    directory = Path(__file__).parent
    helper_end = "var bridge =" if section in ("held-sword", "released-sword") else "var bindingType ="
    helpers = (directory / "verify_body_footwork.script").read_text().split(helper_end, 1)[0]
    helpers = helpers.replace('"artifacts/skill_rebuild/body-footwork"', json.dumps(str(folder)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(world)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    camera = (directory / "verify_appearance_camera.script").read_text()
    body_name = {"meteor-motion": "verify_meteor_motion.script", "palm-motion": "verify_palm_motion.script",
                 "palm-motion-miss": "verify_palm_motion.script", "held-sword": "verify_held_sword.script",
                 "released-sword": "verify_released_sword.script"}
    body = (directory / body_name.get(section, "verify_form_visibility.script")).read_text()
    if section.startswith("palm-motion"):
        camera = camera.replace("    ControllableUnit.clear(false);\n", "")
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    if section == "palm-motion-miss":
        body = body.replace("const bool missOnly = false;", "const bool missOnly = true;")
    if section == "meteor":
        body = body.replace("const bool onlyMeteor = false;", "const bool onlyMeteor = true;")
    imports = ("using UnityEngine.UI;\nusing Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n"
               "using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Blueprints;\nusing Friflo.Engine.ECS;\n")
    script = folder / "input.script"
    script.write_text(imports + helpers + camera + body)
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    try:
        result = baseline.call("run", str(script), "210000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise
    write_json(folder / "result.json", result)
    if not result.get("passed"):
        raise RuntimeError("既有形体外观检查未通过：" + str(folder))
    print(json.dumps(dict(result, output=str(folder)), ensure_ascii=False))


def main():
    """在独立游戏中读取真正资源，保存数值检查及可选的共同动作录像。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", action="store_true", help="录制原版和三维中的近中远主体")
    parser.add_argument("--appearance", nargs="?", const="all", choices=("all", "meteor", "meteor-motion", "palm-motion", "palm-motion-miss", "held-sword", "released-sword"), help="既有形体视距、炎陨落地、掌击收势或持剑")
    args = parser.parse_args()
    if args.appearance:
        appearance(args.appearance)
        return
    import verify_view_switch as view
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/form-detail"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="远近网格与实际选择；批量提交及完整规模性能仍在2.14后续和13验证"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    if args.record:
        shutil.rmtree(folder / "frames", ignore_errors=True)
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_DETAIL_RECORD", "' + ("1" if args.record else "0") + '"); return true;')
    result = view.baseline.call("run", str(script), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not json.loads((folder / "checks.json").read_text())["passed"]:
        raise RuntimeError("显示细节检查未通过：" + str(folder / "checks.json"))
    if args.record:
        caption = folder / "caption.txt"
        caption.write_text("远近显示：巨神 / 水鲸；原版 / 三维；左近、中中、右远；几何验证，非自然实战", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                        "-vf", f"drawtext=fontfile='{caption_font()}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "form-detail.mp4")], check=True)


if __name__ == "__main__":
    main()
