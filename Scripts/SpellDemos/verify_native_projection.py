#!/usr/bin/env python3
"""在隔离的原版显示环境中验证形体深度、纹理及透明重叠。"""
import json
import argparse
import os
from pathlib import Path
import subprocess
import shutil

from common import PROJECT, version_stamp, write_json, caption_font

# 与性能入口共用私有游戏安装和配置，避免加载玩家启用的其他模组。
os.environ["CULTIWAY_BASELINE_ROOT"] = str(PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway")
os.environ["CULTIWAY_BASELINE_GAME"] = str(PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway/game/worldbox")
import baseline_scenarios as baseline
from benchmark_baselines import private_installation


def main():
    """先确认实际加载版本，运行真实绘制检查，再保存来源和结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--body-anchors", action="store_true", help="检查原版身体挂点、腕点、器物握点及坡度下的刚性投影")
    args = parser.parse_args()
    private_installation("cultiway")
    connected = baseline.connect(True)
    if not (baseline.ROOT / "worlds/none/map.wbox").exists():
        subprocess.run(["python3", str(Path(__file__).with_name("baseline_scenarios.py")), "prepare", "--scenario", "none"],
                       cwd=PROJECT, env=baseline.ENV, check=True)
    version, _ = version_stamp()
    output_variable = "CULTIWAY_ANCHOR_OUTPUT" if args.body_anchors else "CULTIWAY_PROJECTION_OUTPUT"
    default_folder = "body-anchors/native" if args.body_anchors else "native-projection"
    folder = Path(os.environ.get(output_variable, PROJECT / "artifacts/skill_rebuild" / default_folder)).resolve()
    folder.mkdir(parents=True, exist_ok=True)
    write_json(folder / "metadata.json", dict(version=version, connection=connected))
    shutil.rmtree(folder / "frames", ignore_errors=True)
    shutil.rmtree(folder / "scene-frames", ignore_errors=True)
    # 已运行的私有游戏也按本次请求切换目录，不能继续沿用启动时的环境变量。
    baseline.call("eval", "System.Environment.SetEnvironmentVariable(" + json.dumps(output_variable) + "," +
                  json.dumps(str(folder), ensure_ascii=False) + "); return true;")
    script = Path(__file__).with_name("verify_body_anchors.script") if args.body_anchors else Path(__file__).with_suffix(".script")
    result = baseline.call("run", str(script), "120000")
    report = json.loads((folder / "checks.json").read_text())
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not report["passed"]:
        raise RuntimeError("原版投影验证未通过，请查看 " + str(folder / "checks.json"))
    font = caption_font()
    caption = "原版身体挂点技术验证：四向转身 / 跳动 / 共同刚性基点" if args.body_anchors else "原版投影技术验证：实体与半透明剑 / 木纹 / 水体连续动作"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                    "-vf", f"drawtext=fontfile='{font}':text='{caption}':fontsize=22:fontcolor=white:box=1:boxcolor=black@0.6:x=16:y=16",
                    "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "native-projection.mp4")], check=True)
    if not args.body_anchors:
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "scene-frames/%05d.png"),
                    "-vf", f"drawtext=fontfile='{font}':text='原版人物与树木：前后交错 / 退出 / 重新进入':fontsize=22:fontcolor=white:box=1:boxcolor=black@0.6:x=16:y=16",
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "native-scene.mp4")], check=True)
    print("检查通过：" + str(folder / "checks.json"), flush=True)


if __name__ == "__main__":
    main()
