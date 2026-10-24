#!/usr/bin/env python3
"""核对真正批量提交、独立实例参数、逐像素对照及两种显示。"""
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
    parser.add_argument("--record", action="store_true", help="录制原版和三维中的多实例独立动作")
    parser.add_argument("--check-exit", action="store_true", help="验证资源重载、换世界，最后退出本脚本的独立游戏检查释放")
    args = parser.parse_args()
    private_installation("cultiway", root=view.ROOT / "environment",
                         worldfall=Path("/data/Downloads/S3COND2_WORLDFALL"))
    connection = view.baseline.connect(True)
    folder = PROJECT / "artifacts/skill_rebuild/form-batch"
    folder.mkdir(parents=True, exist_ok=True)
    version, _ = version_stamp()
    write_json(folder / "metadata.json", dict(version=version, connection=connection,
               scope="共用批量提交及独立实例参数；完整规模性能仍按13验收"))
    script = Path(__file__).with_suffix(".script")
    shutil.copyfile(script, folder / "input.script")
    if args.record:
        shutil.rmtree(folder / "frames", ignore_errors=True)
    view.baseline.call("eval", 'System.Environment.SetEnvironmentVariable("CULTIWAY_BATCH_RECORD", "' + ("1" if args.record else "0") + '"); return true;')
    result = view.baseline.call("run", str(script), "180000")
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not json.loads((folder / "checks.json").read_text())["passed"]:
        raise RuntimeError("批量绘制检查未通过：" + str(folder / "checks.json"))
    if args.record:
        caption = folder / "caption.txt"
        caption.write_text("批量绘制：巨神 / 水鲸 / 根藤；独立动作与染色；原版 / 三维；技术验证，非自然实战", encoding="utf-8")
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                        "-vf", f"drawtext=fontfile='{caption_font()}':textfile='{caption}':fontsize=20:fontcolor=white:box=1:boxcolor=black@0.65:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "form-batch.mp4")], check=True)

    if args.check_exit:
        lifetime = script.with_name("verify_form_batch_lifetime.script")
        (folder / "exit-checks.json").unlink(missing_ok=True)
        shutil.copyfile(lifetime, folder / "input-lifetime.script")
        print(json.dumps(view.baseline.call("run", str(lifetime), "180000"), ensure_ascii=False), flush=True)
        subprocess.run(["python3", str(Path(__file__).with_name("verify_view_switch.py")), "--quit"], check=True)
        exit_report = json.loads((folder / "exit-checks.json").read_text())
        if not exit_report["passed"] or exit_report["process"] != connection["pid"]:
            raise RuntimeError("退出后仍持有绘制缓冲：" + str(exit_report))
        print(json.dumps(exit_report, ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
