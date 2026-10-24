#!/usr/bin/env python3
"""核对根、藤、浪面和流路的逐段成形、真实作用及两种显示。"""
import argparse
import json
import shutil
import subprocess
from pathlib import Path

from common import PROJECT, caption_font, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view


def main():
    """在独立游戏中读取真正资源，保存数值检查及可选的共同动作录像。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", action="store_true", help="录制原版和三维中的根、藤、浪面与流路")
    args = parser.parse_args()
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/form-formation"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="逐段成形、共同作用与保存；不是自然实战、完整植物或世界水流验收"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    if args.record:
        shutil.rmtree(folder / "frames", ignore_errors=True)
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_FORMATION_RECORD", "' + ("1" if args.record else "0") + '"); return true;')
    result = view.baseline.call("run", str(script), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not json.loads((folder / "checks.json").read_text())["passed"]:
        raise RuntimeError("逐段成形检查未通过：" + str(folder / "checks.json"))
    if args.record:
        caption = folder / "caption.txt"
        caption.write_text("逐段成形：分枝根 / 灵藤 / 浪面 / 流路；原版 / 三维；资源验证，非实战验收", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                        "-vf", f"drawtext=fontfile='{caption_font()}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "form-formation.mp4")], check=True)


if __name__ == "__main__":
    main()
