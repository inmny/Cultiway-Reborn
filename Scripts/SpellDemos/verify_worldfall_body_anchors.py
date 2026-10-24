#!/usr/bin/env python3
"""在私有Worldfall游戏中验证正式身体施放、肩腕连接和器物接管。"""
import argparse
import json
import shutil
import subprocess
from pathlib import Path

from common import PROJECT, caption_font, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """复用私有配置与连接，保存实际加载来源，再执行身体验证。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--worldfall", type=Path, default=Path("/data/Downloads/S3COND2_WORLDFALL"))
    parser.add_argument("--armour", action="store_true", help="验证衣甲在共同动作中的真实接合")
    parser.add_argument("--record", action="store_true", help="额外记录正式动作慢放近景")
    parser.add_argument("--quit", action="store_true", help="退出同一私有游戏")
    args = parser.parse_args()
    if args.quit:
        view.baseline.connect(False, validate_runtime=False)
        print(view.baseline.call("eval", "UnityEngine.Application.Quit(); return true;"))
        return
    private_installation("cultiway", root=view.ROOT / "environment", worldfall=args.worldfall.resolve())
    connected = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/body-anchors" / ("armour" if args.armour else "worldfall")
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected,
               worldfallSource=str(args.worldfall.resolve())))
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_BODY_ANCHOR_RECORD", "' + ("1" if args.record else "0") + '"); return true;')
    if args.record:
        shutil.rmtree(folder / "frames", ignore_errors=True)
    script = Path(__file__).with_name("verify_worldfall_armour.script") if args.armour else Path(__file__).with_suffix(".script")
    result = view.baseline.call("run", str(script), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    report = json.loads((folder / "checks.json").read_text())
    if args.record and report["passed"]:
        caption = folder / "caption.txt"
        caption.write_text("衣甲接合验证：皮甲 / 铁甲 / 精金甲；正式动作慢放" if args.armour else
                           "身体挂点验证：正式右掌 / 持剑 / 左掌；动作慢放；第一人称旁观", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                        "-vf", f"drawtext=fontfile='{caption_font()}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / ("armour-anchors.mp4" if args.armour else "body-anchors.mp4"))], check=True)
    if not report["passed"]:
        raise RuntimeError("身体挂点检查未通过：" + str(folder / "checks.json"))


if __name__ == "__main__":
    main()
