#!/usr/bin/env python3
"""核对刚性器物的实际内腔、镜面、握点、构造与分件，可记录两种显示。"""
import argparse
import json
import shutil
import subprocess
from pathlib import Path

from common import PROJECT, caption_font, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """复用私有游戏及发布资源，把技术检查与录制保存在独立目录。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", action="store_true", help="录制同一组器物在原版和三维中的转面近景")
    args = parser.parse_args()
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/rigid-forms"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="刚性资源、实际构造及两种显示；不是最终法宝战斗验收"))
    if args.record:
        shutil.rmtree(folder / "frames", ignore_errors=True)
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_RIGID_RECORD", "' + ("1" if args.record else "0") + '"); return true;')
    result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not json.loads((folder / "checks.json").read_text())["passed"]:
        raise RuntimeError("刚性器物验证未通过：" + str(folder / "checks.json"))
    if args.record:
        caption = folder / "caption.txt"
        caption.write_text("器物资源技术演示：剑 / 铜镜 / 敞口灵炉；原版 / 三维；非战斗验收", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                        "-vf", f"drawtext=fontfile='{caption_font()}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "rigid-forms.mp4")], check=True)


if __name__ == "__main__":
    main()
