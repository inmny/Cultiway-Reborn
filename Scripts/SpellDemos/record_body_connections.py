#!/usr/bin/env python3
"""复用已通过的关系场景，录制徒手、持物、器官及飞行/游泳目标的连续过程。"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

from common import PROJECT, caption_font, write_json

CASES = {
    "throw": ("徒手抓投", ["--carrying", "--carry-cases", "throw"], "body-carrying", "cases-throw"),
    "throw-item": ("持剑目标抓投", ["--carrying", "--carry-cases", "throw-item"], "body-carrying", "cases-throw-item"),
    "palm": ("徒手抓持", ["--connections", "--connection-cases", "grab"], "body-connections", "cases-grab"),
    "sword": ("持剑接触", ["--held-connections", "--held-cases", "sword.edge"], "body-held-connections", "cases-sword.edge"),
    "organ": ("角部接触", ["--connection-parts", "--source-cases", "organ:yao.horn.thunder"], "body-connection-parts", "sources-organ.yao.horn.thunder"),
    "flying": ("飞行目标接触", ["--target-parts", "--target-cases", "bee.surface", "--target-motion", "reach"], "body-target-parts", "cases-bee.surface/motion-reach"),
    "swimming": ("游泳目标接触", ["--swimming-surfaces", "--swimming-cases", "human.moving"], "body-swimming-surfaces", "cases-human.moving"),
}


def encode(case, folder, output, native, label=None, milestone=None, skip_initial=0):
    """按真实世界时间差编码，不插入离线动作或用暂停凑出运动时长。"""
    data = json.loads((folder / "recording.json").read_text())
    frames = data["frames"][skip_initial:]
    gaps = [b["time"] - a["time"] for a, b in zip(frames, frames[1:])]
    if not gaps or any(gap <= 0 for gap in gaps):
        raise RuntimeError("缺少连续且递增的世界时间：" + str(folder))
    durations = gaps + [1 / 24]
    timeline = [0.0]
    for duration in durations:
        timeline.append(timeline[-1] + duration)
    lines = []
    for frame, duration in zip(frames, durations):
        lines += [f"file 'frames/{frame['frame']:05d}.png'", f"duration {duration:.9f}"]
    lines.append(f"file 'frames/{frames[-1]['frame']:05d}.png'")
    (folder / "frames.ffconcat").write_text("\n".join(["ffconcat version 1.0", *lines]) + "\n")
    view = "原版独立安装" if native else "Worldfall"
    name = label or CASES[case][0]
    title = f"{name} · {view} · 世界时间常速"
    (folder / "title.txt").write_text(title)
    filters = [] if native else ["crop=iw/2:ih/2:iw*0.05:ih*0.1" if case == "flying" else "crop=iw/2:ih/2"]
    filters.append("scale=960:540:flags=" + ("neighbor" if native else "bicubic"))
    font = caption_font()
    filters.append(f"drawtext=fontfile='{font}':textfile='title.txt':fontsize=21:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=14")
    stages = []
    for index, frame in enumerate(frames):
        if not stages or stages[-1]["name"] != frame["stage"]:
            stages.append(dict(name=frame["stage"], first=index, last=index))
        else:
            stages[-1]["last"] = index
    for index, stage in enumerate(stages):
        caption = f"stage-{index}.txt"
        prefix = milestone or ("4.24 连续抓投" if case.startswith("throw") else "4.23 接触关系验证")
        (folder / caption).write_text(prefix + " · " + stage["name"])
        start, end = timeline[stage["first"]], timeline[stage["last"] + 1]
        filters.append(f"drawtext=fontfile='{font}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=h-th-14:enable='between(t,{start:.6f},{end:.6f})'")
    movie = output / f"{case}.mp4"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-safe", "0", "-f", "concat", "-i", "frames.ffconcat",
                    "-vf", ",".join(filters), "-r", "24", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(movie)],
                   cwd=folder, check=True)
    evidence = dict(case=case, name=name, view=data["view"], movie=str(movie), recording=str(folder / "recording.json"),
                    definition=str(folder / "definition.json"), result=str(folder / "result.json"),
                    duration=timeline[-1], frames=len(frames), maxWorldGap=max(gaps), stages=stages,
                    trimmedInitialFrames=skip_initial, worldStart=frames[0]["time"], worldEnd=frames[-1]["time"],
                    events=data["events"], version=json.loads((folder / "metadata.json").read_text())["version"])
    write_json(output / f"{case}.json", evidence)
    print(movie, flush=True)
    return evidence


def main():
    """每次只跑一个原有场景，复用当前独立游戏；支持指定与仅编码。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", action="store_true", help="使用没有Worldfall的独立原版安装")
    parser.add_argument("--case", nargs="+", choices=list(CASES), help="指定原有接触或抓投场景；缺省仍录制原五段接触")
    parser.add_argument("--encode-only", action="store_true", help="只编码已有实际帧")
    args = parser.parse_args()
    mode = "native" if args.native else "worldfall"
    output = PROJECT / "artifacts/skill_rebuild/body-connection-films" / mode
    output.mkdir(parents=True, exist_ok=True)
    manifest = output / ("manifest" + ("-" + "-".join(args.case) if args.case else "") + ".json")
    write_json(manifest, dict(passed=False, status="running", view=mode))
    rows = []
    original_cases = [case for case in CASES if not case.startswith("throw")]
    for case in args.case or original_cases:
        _, flags, group, suffix = CASES[case]
        if case == "flying" and not args.native:
            # 三维沿R06已通过的短促接触；原版沿已通过的退远场景，各自保留真实构造。
            flags = flags[:-2]
            suffix = suffix.split("/motion-", 1)[0]
        if case == "swimming" and not args.native:
            flags = ["--swimming-surfaces", "--swimming-cases", "human.reach"]
            suffix = "cases-human.reach"
        folder = PROJECT / "artifacts/skill_rebuild" / (group + ("-native" if args.native else "")) / suffix / "film"
        if not args.encode_only:
            command = [sys.executable, str(Path(__file__).with_name("verify_body_footwork.py")), *flags, "--film-relations"]
            if case.startswith("throw"):
                command.append("--carry-save")
            if args.native:
                command.append("--native")
            log = output / f"{case}-run.log"
            with log.open("w") as stream:
                result = subprocess.run(command, cwd=PROJECT, stdout=stream, stderr=subprocess.STDOUT)
            if result.returncode:
                raise RuntimeError("场景未完成，见 " + str(log))
        result = json.loads((folder / "result.json").read_text())
        if not result["passed"]:
            raise RuntimeError("原场景未通过，不能作为验收录像：" + str(folder))
        rows.append(encode(case, folder, output, args.native))
    if not args.case:
        (output / "movies.ffconcat").write_text("ffconcat version 1.0\n" +
            "".join(f"file '{case}.mp4'\n" for case in original_cases))
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-safe", "0", "-f", "concat", "-i", "movies.ffconcat",
                        "-c", "copy", "-movflags", "+faststart", "all.mp4"], cwd=output, check=True)
        print(output / "all.mp4", flush=True)
    write_json(manifest, dict(passed=True, view=mode, cases=rows,
               timeBasis="游戏世界时间；功能录像不作为负载性能证明"))


if __name__ == "__main__":
    main()
