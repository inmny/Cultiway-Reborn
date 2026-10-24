#!/usr/bin/env python3
"""在独立游戏中核对器官逐帧出口、正式付费释放与延迟中的身体变化。"""
import json
import argparse
import subprocess
from pathlib import Path

from common import PROJECT, caption_font, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """复用独立配置与正式游戏连接，记录本次加载来源并运行检查。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--spatial", action="store_true", help="核对实际三维身体与雷角资源接合")
    parser.add_argument("--record", action="store_true", help="为三维接合额外记录技术近景")
    parser.add_argument("--output", type=Path, help="另存本次证据，避免覆盖历史验证")
    args = parser.parse_args()
    if args.record and not args.spatial:
        parser.error("--record需要与--spatial一起使用")
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connected = view.baseline.connect(True)
    folder = args.output.resolve() if args.output else PROJECT / "artifacts/skill_rebuild/body-anchors" / ("worldfall-organs" if args.spatial else "organ-outlets")
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connected,
               scope="三维器官表面接合与共同出口" if args.spatial else "原版实际器官帧及正式施法出口；不代表三维器官已经接合"))
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_ORGAN_ANCHOR_RECORD", "' + ("1" if args.record else "0") + '"); return true;')
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_ORGAN_ANCHOR_OUTPUT", ' + json.dumps(str(folder)) + '); return true;')
    script = Path(__file__).with_name("verify_worldfall_organs.script") if args.spatial else Path(__file__).with_suffix(".script")
    result = view.baseline.call("run", str(script), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    report = json.loads((folder / "checks.json").read_text())
    if not report["passed"]:
        raise RuntimeError("器官出口检查未通过：" + str(folder / "checks.json"))
    if args.record:
        caption = folder / "caption.txt"
        caption.write_text("雷角挂点技术近景；实际表面与共同出口；比例仍须完善", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                        "-vf", f"drawtext=fontfile='{caption_font()}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "organ-anchors.mp4")], check=True)


if __name__ == "__main__":
    main()
