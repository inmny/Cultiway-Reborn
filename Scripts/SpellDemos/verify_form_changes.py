#!/usr/bin/env python3
"""核对六类变化资料、真实裂片及缺失资源提示。"""
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
    parser.add_argument("--record", action="store_true", help="录制原版和三维中的聚形、开裂、碎解、冻结及流散")
    args = parser.parse_args()
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/form-changes"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="六类共用变化资料及真实片体，非固定招式或自然战斗验收"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    if args.record:
        shutil.rmtree(folder / "frames", ignore_errors=True)
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_CHANGES_RECORD", "' + ("1" if args.record else "0") + '"); return true;')
    result = view.baseline.call("run", str(script), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not json.loads((folder / "checks.json").read_text())["passed"]:
        raise RuntimeError("变化资料检查未通过：" + str(folder / "checks.json"))
    if args.record:
        caption = folder / "caption.txt"
        caption.write_text("变化资源：聚形碎解 / 开裂冻结 / 材料流散；原版 / 三维；验证构造，非自然实战", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                        "-vf", f"drawtext=fontfile='{caption_font()}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "form-changes.mp4")], check=True)


if __name__ == "__main__":
    main()
