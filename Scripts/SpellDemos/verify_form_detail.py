#!/usr/bin/env python3
"""核对近中远网格、原始通道及实际视点的显示选择。"""
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
    parser.add_argument("--record", action="store_true", help="录制原版和三维中的近中远主体")
    args = parser.parse_args()
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
