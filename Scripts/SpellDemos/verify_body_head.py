#!/usr/bin/env python3
"""在独立游戏验证头部动作，再复用四族实绘夹具检查真实骨架。"""
import argparse
import json
import tempfile
from pathlib import Path

from common import PROJECT, version_stamp, write_json
import verify_view_switch as view


def main():
    """实际作用及显示分别记录，已有测试安装直接复用。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--views-only", action="store_true", help="只复测实际绘制，不重复作用与保存专项")
    args = parser.parse_args()
    folder = PROJECT / "artifacts/skill_rebuild/body-head"
    folder.mkdir(parents=True, exist_ok=True)
    connected = view.baseline.connect(True)
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    if not args.views_only:
        result = view.baseline.call("run", str(Path(__file__).with_suffix(".script")), "180000")
        write_json(folder / "result.json", result)
        print(json.dumps(dict(name="body-head", passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)
        if not result["passed"]:
            raise RuntimeError("头部实际作用专项未通过")
    template = Path(__file__).with_name("verify_worldfall_body_fits.script").read_text()
    marker = "const bool UseHeadMotion = false;"
    if template.count(marker) != 1:
        raise RuntimeError("实绘夹具的头部入口标记不符")
    with tempfile.TemporaryDirectory(prefix="cultiway-body-head-") as temporary:
        script = Path(temporary) / "worldfall.script"
        script.write_text(template.replace(marker, "const bool UseHeadMotion = true;"))
        result = view.baseline.call("run", str(script), "180000")
    write_json(folder / "worldfall-result.json", result)
    print(json.dumps(dict(name="worldfall-head", passed=result["passed"], checks=result["checks"]), ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("头部实际骨架绘制未通过")


if __name__ == "__main__":
    main()
