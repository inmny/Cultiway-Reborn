#!/usr/bin/env python3
"""在独立游戏验证身体构造资料，再复用真实三维骨架夹具核对声明落脚点。"""
import json
import tempfile
from pathlib import Path

from common import PROJECT, version_stamp, write_json
import verify_view_switch as view


def main():
    """复用已准备的独立游戏，临时绘制脚本始终从同一夹具生成。"""
    folder = PROJECT / "artifacts/skill_rebuild/body-construction"
    folder.mkdir(parents=True, exist_ok=True)
    connection = view.baseline.connect(True)
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connection))
    source = Path(__file__).with_name("verify_body_construction.script")
    result = view.baseline.call("run", str(source), "180000")
    write_json(folder / "result.json", result)
    print(json.dumps(dict(name="construction", passed=result["passed"], checks=len(result["checks"])), ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("身体构造专项未通过")
    template = Path(__file__).with_name("verify_worldfall_body_fits.script").read_text()
    marker = "const bool UseConstructedSupport = false;"
    if template.count(marker) != 1:
        raise RuntimeError("绘制夹具的构造入口标记不符")
    with tempfile.TemporaryDirectory(prefix="cultiway-body-construction-") as work:
        script = Path(work) / "worldfall.script"
        script.write_text(template.replace(marker, "const bool UseConstructedSupport = true;"))
        result = view.baseline.call("run", str(script), "180000")
    write_json(folder / "worldfall-result.json", result)
    print(json.dumps(dict(name="worldfall-construction", passed=result["passed"], checks=result["checks"]), ensure_ascii=False), flush=True)
    if not result["passed"]:
        raise RuntimeError("构造落脚点的真实三维绘制未通过")


if __name__ == "__main__":
    main()
