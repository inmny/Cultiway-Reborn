#!/usr/bin/env python3
"""复用已有双人世界，专项核对真实兵器架挡；不遍历身体与装备。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """共用现成准备和正式施放入口，记录本轮输入及实际结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", action="store_true", help="使用独立原版游戏")
    args = parser.parse_args()
    if args.native:
        install = PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway"
        os.environ["CULTIWAY_BASELINE_ROOT"] = str(install)
        os.environ["CULTIWAY_BASELINE_GAME"] = str(install / "game/worldbox")
        import baseline_scenarios as baseline
    else:
        import verify_view_switch
        baseline = verify_view_switch.baseline
    folder = PROJECT / "artifacts/skill_rebuild" / ("weapon-connections-native" if args.native else "weapon-connections")
    folder.mkdir(parents=True, exist_ok=True)
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for p in (PROJECT / "Source").rglob("*.cs")):
        raise RuntimeError("游戏仍在使用本轮修改之前的代码，请先退出这个独立游戏")
    helpers = Path(__file__).with_name("verify_body_footwork.script").read_text().split("var bridge =", 1)[0]
    helpers = helpers.replace("artifacts/skill_rebuild/body-footwork", str(folder.relative_to(PROJECT)))
    script = folder / "input.script"
    script.write_text(helpers + Path(__file__).with_suffix(".script").read_text())
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected))
    try:
        result = baseline.call("run", str(script), "180000")
        write_json(folder / "result.json", result)
        (folder / "error.json").unlink(missing_ok=True)
        print(json.dumps(result, ensure_ascii=False))
    except RuntimeError as error:
        (folder / "error.json").write_text(str(error))
        raise RuntimeError("兵器专项未完成，详见 " + str(folder / "error.json")) from None


if __name__ == "__main__":
    main()
