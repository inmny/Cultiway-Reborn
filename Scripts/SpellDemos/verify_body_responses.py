#!/usr/bin/env python3
"""复用现成世界、人物及铁剑，验证真实结果驱动身体收势。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """沿已有准备和正式施放入口，只检查本次结果恢复及归还。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", action="store_true", help="使用独立原版安装")
    parser.add_argument("--case", nargs="+", choices=["contact", "blocked", "miss"], help="只检查指定结果")
    parser.add_argument("--pictures", action="store_true", help="记录结果开始、恢复中途和归还后的实际原版画面")
    args = parser.parse_args()
    if args.native:
        install = PROJECT / "artifacts/skill_rebuild/performance-baseline/cultiway"
        os.environ["CULTIWAY_BASELINE_ROOT"] = str(install)
        os.environ["CULTIWAY_BASELINE_GAME"] = str(install / "game/worldbox")
        import baseline_scenarios as baseline
    else:
        import verify_view_switch
        baseline = verify_view_switch.baseline
    folder = PROJECT / "artifacts/skill_rebuild" / ("body-responses-native" if args.native else "body-responses")
    if args.case:
        folder /= "cases-" + "-".join(args.case)
    folder.mkdir(parents=True, exist_ok=True)
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / "process.json").read_text())["started"]
    if any(p.stat().st_mtime > started for p in (PROJECT / "Source").rglob("*.cs")):
        raise RuntimeError("游戏尚未加载本轮代码，请先退出这个独立游戏")
    helpers = Path(__file__).with_name("verify_body_footwork.script").read_text().split("var bridge =", 1)[0]
    helpers = helpers.replace("artifacts/skill_rebuild/body-footwork", str(folder.relative_to(PROJECT)))
    shared = Path(__file__).with_name("verify_weapon_connections.script").read_text().split("\ntry\n{", 1)[0]
    shared = shared.replace("test.body-footwork.weapon-connection.", "test.body-footwork.")
    fragment = Path(__file__).with_suffix(".script").read_text()
    fragment = "bool pictures = " + str(args.pictures).lower() + ";\n" + fragment
    if args.case:
        fragment = fragment.replace('new[] { "contact", "blocked", "miss" }',
            "new[] { " + ", ".join(json.dumps(case) for case in args.case) + " }")
    script = folder / "input.script"
    script.write_text(helpers + shared + fragment)
    write_json(folder / "metadata.json", dict(version=version_stamp()[0], connection=connected,
        cases=args.case or ["contact", "blocked", "miss"], pictures=args.pictures))
    try:
        result = baseline.call("run", str(script), "180000")
        write_json(folder / "result.json", result)
        (folder / "error.json").unlink(missing_ok=True)
        print(json.dumps(result, ensure_ascii=False))
    except RuntimeError as error:
        (folder / "error.json").write_text(str(error))
        write_json(folder / "result.json", dict(passed=False, error=str(folder / "error.json")))
        raise RuntimeError("结果恢复专项未完成，详见 " + str(folder / "error.json")) from None


if __name__ == "__main__":
    main()
