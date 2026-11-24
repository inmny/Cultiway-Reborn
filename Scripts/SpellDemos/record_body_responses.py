#!/usr/bin/env python3
"""复用现成身体和正式出招，集中录制4.26的四类结果过程。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode

CASES = {
    "contact": ("命中后连续收势", "responses.contact"),
    "blocked": ("被实际兵器架挡后回收", "responses.blocked"),
    "force": ("真实受击、翻滚与起身", "responses.force.guard"),
    "status": ("实际停顿中断与恢复", "responses.status"),
}


def main():
    """只使用一个既有人形及铁剑，帧、事实和正式输入一起交付。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", action="store_true", help="使用独立原版游戏")
    parser.add_argument("--case", nargs="+", choices=list(CASES), help="只录制指定结果")
    parser.add_argument("--encode-only", action="store_true", help="只编码已有连续帧")
    parser.add_argument("--surface-feedback", action="store_true", help="4.34：关闭装饰，仅录掌击和铁剑的实际动作与局部表面")
    args = parser.parse_args()
    cases = args.case or (["contact", "blocked"] if args.surface_feedback else list(CASES))
    if args.surface_feedback and any(case not in {"contact", "blocked"} for case in cases):
        parser.error("局部表面影片只复用contact、blocked")
    mode = "native" if args.native else "worldfall"
    output = PROJECT / ("artifacts/skill_rebuild/body-surface-films" if args.surface_feedback else "artifacts/skill_rebuild/body-response-films") / mode
    output.mkdir(parents=True, exist_ok=True)
    manifest = output / ("manifest" + ("-" + "-".join(args.case) if args.case else "") + ".json")
    write_json(manifest, dict(passed=False, status="running", view=mode))
    rows = []
    if not args.encode_only:
        if args.native:
            install = PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway"
            os.environ["CULTIWAY_BASELINE_ROOT"] = str(install)
            os.environ["CULTIWAY_BASELINE_GAME"] = str(install / "game/worldbox")
            import baseline_scenarios as baseline
        else:
            import verify_view_switch
            baseline = verify_view_switch.baseline
        connected = baseline.connect(True, validate_runtime=False)
        started = json.loads((baseline.ROOT / "process.json").read_text())["started"]
        if any(p.stat().st_mtime > started for p in (PROJECT / "Source").rglob("*.cs")):
            raise RuntimeError("游戏尚未加载本轮代码，请先退出这一独立进程")
        version = version_stamp()[0]
    for case in cases:
        folder = output / case
        folder.mkdir(exist_ok=True)
        label, suffix = CASES[case]
        if not args.encode_only:
            shutil.rmtree(folder / "frames", ignore_errors=True)
            for name in ("recording.json", "result.json", "checks.json", "error.json"):
                (folder / name).unlink(missing_ok=True)
            helpers = Path(__file__).with_name("verify_body_footwork.script").read_text().split("var bridge =", 1)[0]
            helpers = helpers.replace("artifacts/skill_rebuild/body-footwork", str(folder.relative_to(PROJECT)))
            helpers = helpers.replace("async Task Advance(", "bool filmCapturing = false;\nasync Task Advance(", 1)
            helpers = helpers.replace("    Config.paused = false;\n    double until", "    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until", 1)
            helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,', 'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
            film = Path(__file__).with_name("record_body_connections.script").read_text()
            film = film.replace('const string filmTargetSuffix = "";', 'const string filmTargetSuffix = "' + suffix + '";')
            film = film.replace("bool filmResponses = false;", "bool filmResponses = true;")
            if args.surface_feedback:
                raw = Path(__file__).with_name("record_surface_feedback.script").read_text()
                film = raw + "\n" + film
                film = film.replace("        filmCapturing = true;", "        decorations.Hide();\n        filmCapturing = true;", 1)
                film = film.replace("    RenderTexture previous = RenderTexture.active;", "    decorations.Hide();\n    RenderTexture previous = RenderTexture.active;", 1)
                film = film.replace("        sourceControl = BodyControlState.IsBusy(filmSource.id),", "        feedback = FeedbackFacts(filmSource.id),\n        sourceControl = BodyControlState.IsBusy(filmSource.id),", 1)
                film = film.replace("        if (filmRead != null) UnityEngine.Object.Destroy(filmRead);", "        decorations.Restore();\n        if (filmRead != null) UnityEngine.Object.Destroy(filmRead);", 1)
            helpers = helpers.replace("async Task Fire(", "/// <summary>保留实际空间点的三个坐标。</summary>\n/// <param name=\"p\">实际空间点。</param>\nfloat[] P(Vector3 p) => new[] { p.x, p.y, p.z };\n" + film + "\nasync Task Fire(", 1)
            helpers = helpers.replace("    if (aggro)\n", "    await StartRelationFilm(d, a, target);\n    if (aggro)\n", 1)
            shared = Path(__file__).with_name("verify_weapon_connections.script").read_text().split("\ntry\n{", 1)[0]
            shared = shared.replace("test.body-footwork.weapon-connection.", "test.body-footwork.")
            script = folder / "input.script"
            fragment = Path(__file__).with_suffix(".script").read_text().replace('const string sample = "contact";', 'const string sample = "' + case + '";')
            if args.surface_feedback:
                # 无伤触碰保留实际姿态、接触和结果，避免原版伤害图遮住待观察表面。
                fragment = fragment.replace("        await Fire(definition, source, target);", "        definition.Body.Impact ??= new ConstructionImpact(); definition.Body.Impact.DamageScale = 0f;\n        await Fire(definition, source, target);", 1)
                fragment = fragment.replace("        Position(source, target, guard, attack);", "        attack.Body.Impact.DamageScale = 0f;\n        Position(source, target, guard, attack);", 1)
            program = helpers + shared + fragment
            if args.surface_feedback:
                program += "\n" + Path(__file__).with_name("capture_decorations.script").read_text()
            script.write_text(program)
            write_json(folder / "metadata.json", dict(version=version, connection=connected, case=case, surfaceFeedback=args.surface_feedback))
            try:
                result = baseline.call("run", str(script), "180000")
            except RuntimeError as error:
                (folder / "error.json").write_text(str(error))
                write_json(folder / "result.json", dict(passed=False))
                raise RuntimeError("结果录像未完成，见 " + str(folder / "error.json")) from None
            write_json(folder / "result.json", result)
        if not json.loads((folder / "result.json").read_text())["passed"]:
            raise RuntimeError("原场景未通过，不能作为交付录像：" + str(folder))
        # 沿现有兵器录像的约定去掉三维施放前技术初帧，原始帧仍全部保留。
        # 两种模式都按实际世界时间编码，沿已有近景裁切显示双方动作。
        rows.append(encode(case, folder, output, args.native, label,
            "4.34 无粒子动作与局部材质" if args.surface_feedback else "4.26 结果过程",
            0 if args.native or args.surface_feedback else 1, crop_worldfall=True))
    if not args.case:
        (output / "movies.ffconcat").write_text("ffconcat version 1.0\n" + "".join(f"file '{case}.mp4'\n" for case in cases))
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-safe", "0", "-f", "concat", "-i", "movies.ffconcat", "-c", "copy", "-movflags", "+faststart", "all.mp4"], cwd=output, check=True)
    write_json(manifest, dict(passed=True, view=mode, cases=rows, timeBasis="实际世界时间；功能录像不作为性能或最终美术证明"))


if __name__ == "__main__":
    main()
