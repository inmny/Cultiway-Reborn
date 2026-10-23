#!/usr/bin/env python3
"""在独立Worldfall配置中检查真实视角切换、位置、高度、遮挡和近裁剪。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation

ROOT = PROJECT / "artifacts/skill_rebuild/view-switch"
PROFILE = ROOT / "environment/cultiway"
os.environ["CULTIWAY_BASELINE_ROOT"] = str(PROFILE)
os.environ["CULTIWAY_BASELINE_GAME"] = str(PROFILE / "game/worldbox")
import baseline_scenarios as baseline


def main():
    """明确外部来源，启动私有游戏，再执行同一场景中的真实显示检查。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--worldfall", type=Path, default=Path("/data/Downloads/S3COND2_WORLDFALL"))
    parser.add_argument("--quit", action="store_true", help="退出本脚本的独立游戏")
    args = parser.parse_args()
    if args.quit:
        subprocess.run(["python3", str(Path(__file__).with_name("baseline_scenarios.py")), "quit"],
                       cwd=PROJECT, env=baseline.ENV, check=True)
        return
    if not (PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway/worlds/none/map.wbox").is_file():
        raise RuntimeError("缺少固定世界，请先运行 Scripts/SpellDemos/verify_native_projection.py，再退出其独立游戏")
    game = private_installation("cultiway", root=ROOT / "environment", worldfall=args.worldfall.resolve())
    connected = baseline.connect(True)
    folder = ROOT / "views"
    folder.mkdir(parents=True, exist_ok=True)
    shutil.rmtree(folder / "frames", ignore_errors=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected,
               game=str(game), worldfallSource=str(args.worldfall.resolve()), presentationFps=12))
    result = baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    report = json.loads((folder / "checks.json").read_text())
    font = subprocess.run(["fc-match", "-f", "%{file}", "sans:lang=zh"], capture_output=True, text=True, check=True).stdout
    caption = folder / "caption.txt"
    caption.write_text("视角切换技术检查：原版 / Worldfall / 第一人称；同一发布形体", encoding="utf-8")
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                    "-vf", f"drawtext=fontfile='{font}':textfile='{caption}':fontsize=22:fontcolor=white:box=1:boxcolor=black@0.6:x=16:y=16",
                    "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "view-switch.mp4")], check=True)
    if not report["passed"]:
        raise RuntimeError("视角检查未通过：" + str(folder / "checks.json"))
    print("检查与录像：" + str(folder), flush=True)


if __name__ == "__main__":
    main()
