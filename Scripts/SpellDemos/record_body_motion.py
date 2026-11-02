#!/usr/bin/env python3
"""录制原版及Worldfall正式身体动作，并按真实世界时间输出可核对的视频。"""
import argparse
import json
import subprocess
from pathlib import Path

from common import PROJECT, caption_font, version_stamp, write_json
import verify_view_switch as view


def encode(folder, data, mode):
    """逐帧采用实际时间差，保留原速及明确改为三倍历时的动作。"""
    frames = [row for row in data["frames"] if row["view"] == mode]
    cases = [row for row in data["cases"] if row["view"] == mode]
    if not frames or not cases:
        raise RuntimeError(f"缺少 {mode} 的录像帧，请先使用 --view {mode} 录制")
    output = folder / mode
    duration = [max(b["time"] - a["time"], 1 / 60) for a, b in zip(frames, frames[1:])] + [1 / 24]
    timeline = [0.0]
    for seconds in duration:
        timeline.append(timeline[-1] + seconds)
    lines = []
    for row, seconds in zip(frames, duration):
        lines += [f"file frames/{row['frame']:05d}.png", f"duration {seconds:.9f}"]
    lines.append(f"file frames/{frames[-1]['frame']:05d}.png")
    (output / "frames.txt").write_text("\n".join(lines) + "\n", encoding="utf-8")
    filters = (["crop=iw/2:ih/2:iw/4:ih/4"] if mode == "worldfall" else [])
    filters.append("scale=960:540:flags=neighbor")
    font = caption_font()
    for i, case in enumerate(cases):
        label = f"{'原版' if mode == 'native' else 'Worldfall'}近景 · {case['name']} · {'常速' if case['rate'] == 1 else '动作历时×3'} · 实际接触 {case['contacts']} 次"
        caption = output / f"caption-{i}.txt"
        caption.write_text(label, encoding="utf-8")
        start, end = timeline[case["first"]], timeline[case["last"] + 1]
        filters.append(f"drawtext=fontfile='{font}':textfile='{caption.name}':fontsize=24:fontcolor=white:box=1:boxcolor=black@0.7:x=18:y=16:enable='between(t,{start:.6f},{end:.6f})'")
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-f", "concat", "-safe", "0", "-i", "frames.txt",
                    "-vf", ",".join(filters), "-r", "30", "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", "body-motion.mp4"],
                   cwd=output, check=True)
    write_json(output / "timeline.json", dict(duration=timeline[-1], frames=len(frames), cases=cases,
               framing="Worldfall画面中央二分之一裁切" if mode == "worldfall" else "原版相机近景拍摄"))
    print(output / "body-motion.mp4", flush=True)


def main():
    """明确使用已经准备的独立游戏；素材缺失不会改动玩家安装。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--view", choices=["all", "native", "worldfall"], default="all")
    parser.add_argument("--encode-only", action="store_true", help="只将已有帧编码，不重跑游戏")
    args = parser.parse_args()
    folder = PROJECT / "artifacts/skill_rebuild/body-motion-film"
    folder.mkdir(parents=True, exist_ok=True)
    if not args.encode_only:
        connected = view.baseline.connect(True)
        version, _ = version_stamp()
        write_json(folder / "metadata.json", dict(version=version, connection=connected, view=args.view))
        view.baseline.call("eval", f'System.Environment.SetEnvironmentVariable("CULTIWAY_BODY_FILM_VIEW", "{args.view}"); return true;')
        result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
        write_json(folder / "result.json", result)
        print(json.dumps(result, ensure_ascii=False), flush=True)
    data = json.loads((folder / "recording.json").read_text())
    for mode in ("native", "worldfall"):
        if args.view in ("all", mode):
            encode(folder, data, mode)


if __name__ == "__main__":
    main()
