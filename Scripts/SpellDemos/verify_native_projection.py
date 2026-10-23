#!/usr/bin/env python3
"""在隔离的原版显示环境中验证形体深度、纹理及透明重叠。"""
import json
import os
from pathlib import Path
import subprocess
import shutil

from common import PROJECT, version_stamp, write_json

# 与性能入口共用私有游戏安装和配置，避免加载玩家启用的其他模组。
os.environ["CULTIWAY_BASELINE_ROOT"] = str(PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway")
os.environ["CULTIWAY_BASELINE_GAME"] = str(PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway/game/worldbox")
import baseline_scenarios as baseline
from benchmark_baselines import private_installation


def main():
    """先确认实际加载版本，运行真实绘制检查，再保存来源和结果。"""
    private_installation("cultiway")
    connected = baseline.connect(True)
    if not (baseline.ROOT / "worlds/none/map.wbox").exists():
        subprocess.run(["python3", str(Path(__file__).with_name("baseline_scenarios.py")), "prepare", "--scenario", "none"],
                       cwd=PROJECT, env=baseline.ENV, check=True)
    version, _ = version_stamp()
    folder = Path(os.environ.get("CULTIWAY_PROJECTION_OUTPUT", PROJECT / "artifacts/skill_rebuild/native-projection")).resolve()
    folder.mkdir(parents=True, exist_ok=True)
    write_json(folder / "metadata.json", dict(version=version, connection=connected))
    shutil.rmtree(folder / "frames", ignore_errors=True)
    shutil.rmtree(folder / "scene-frames", ignore_errors=True)
    result = baseline.call("run", str(Path(__file__).with_suffix(".script")), "120000")
    report = json.loads((folder / "checks.json").read_text())
    print(json.dumps(result, ensure_ascii=False), flush=True)
    if not report["passed"]:
        raise RuntimeError("原版投影验证未通过，请查看 " + str(folder / "checks.json"))
    font = subprocess.run(["fc-match", "-f", "%{file}", "sans:lang=zh"], capture_output=True, text=True, check=True).stdout
    caption = "原版投影技术验证：实体与半透明剑 / 木纹 / 水体连续动作"
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "frames/%05d.png"),
                    "-vf", f"drawtext=fontfile='{font}':text='{caption}':fontsize=22:fontcolor=white:box=1:boxcolor=black@0.6:x=16:y=16",
                    "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "native-projection.mp4")], check=True)
    subprocess.run(["ffmpeg", "-y", "-v", "error", "-framerate", "12", "-i", str(folder / "scene-frames/%05d.png"),
                    "-vf", f"drawtext=fontfile='{font}':text='原版人物与树木：前后交错 / 退出 / 重新进入':fontsize=22:fontcolor=white:box=1:boxcolor=black@0.6:x=16:y=16",
                    "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(folder / "native-scene.mp4")], check=True)
    print("检查通过：" + str(folder / "checks.json"), flush=True)


if __name__ == "__main__":
    main()
