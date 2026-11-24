#!/usr/bin/env python3
"""选择受改动影响的技能复验，逐项保留命令和结果。"""
import argparse
from datetime import datetime
import json
import os
from pathlib import Path
import subprocess
import sys

from common import FIXTURE_DIR, PROJECT, write_json

SPELLS = Path("Scripts/SpellDemos")
SAVE_TESTS = Path("Scripts/WorldSaveTests")
DESCRIPTIONS = {
    "build": "模组开发编译",
    "tools": "演示工具的快速检查",
    "runtime": "普通施放、失败、取消、再用与隔离",
    "bounds": "真实碰撞、未命中边界和反射",
    "edits": "改轨迹、正式支付、不合法构造及恢复",
    "parallel": "工作线程采样、真实交锋与换世界回收",
    "save-data": "独立运行保存资料及旧格式转换",
    "save": "游戏真实读写、旧单招迁移与读档后施放",
    "rain": "已学雨云的付款、实际成云和回收",
    "resources": "发布资源包与实际形体清单",
    "outline": "真实作用轮廓、原版与三维显示及关闭恢复",
    "visual-result": "同次施放的画面、有效区和实际扣血",
}
COMMANDS = {
    "build": ["dotnet", "build", "Cultiway.csproj", "-c", "Debug"],
    "tools": [sys.executable, "-m", "unittest", "discover", "-s", str(SPELLS), "-p", "test_*.py"],
    "save-data": ["dotnet", "run", "--project", str(SAVE_TESTS / "WorldSaveTests.csproj")],
    "runtime": [sys.executable, str(SPELLS / "verify_demo_isolation.py"), "--output", "{output}/result"],
    "edits": [sys.executable, str(SPELLS / "verify_construction_batch.py"), "--output", "{output}/result"],
    "parallel": [sys.executable, str(SPELLS / "verify_parallel_contact_batch.py"), "--output", "{output}"],
    "rain": [sys.executable, str(SPELLS / "record_rain_process.py"), "legacy", "--output", "{output}"],
    "resources": [sys.executable, str(SPELLS / "verify_form_combinations_909.py"), "--section", "catalog", "--output", "{output}"],
    "outline": [sys.executable, str(SPELLS / "verify_contact_accuracy.py"), "--section", "outline", "--output", "{output}"],
    "visual-result": [sys.executable, str(SPELLS / "verify_visual_boundary.py"), "--section", "circle", "--output", "{output}"],
}
SCRIPTS = {
    "bounds": (SPELLS / "verify_flight_contacts.script", 'Path.Combine(root,"' + FIXTURE_DIR + '")',
               'Path.Combine(root,"artifacts/skill_rebuild/flight-contact-checks.json")', "contacts.json"),
    "save": (SAVE_TESTS / "runtime/verify_skill_settings.script",
             f'Path.Combine(mod.GetDeclaration().FolderPath, "{FIXTURE_DIR}")',
             'Path.Combine(mod.GetDeclaration().FolderPath, "artifacts/skill_settings")', "."),
}
OFFLINE = {"build", "tools", "save-data"}


def run_game_script(name, output):
    """把既有验证的固定输入与输出指向该轮测试目录。"""
    import baseline_scenarios as baseline

    source, fixture, destination, result_path = SCRIPTS[name]
    script = (PROJECT / source).read_text(encoding="utf-8")
    if fixture not in script or destination not in script:
        raise RuntimeError(f"既有脚本的固定路径已变化，请检查 {source}")
    body = script.replace(fixture, json.dumps(str(PROJECT / FIXTURE_DIR))).replace(
        destination, json.dumps(str(output / result_path)))
    program = output / "input.script"
    program.write_text(body, encoding="utf-8")
    connection = baseline.connect(False)
    write_json(output / "connection.json", connection)
    result = baseline.call("run", str(program), "90000")
    write_json(output / "result.json", result)
    if not result.get("passed"):
        raise RuntimeError(f"实机复验未通过，见 {output / 'result.json'}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("checks", nargs="*", choices=tuple(DESCRIPTIONS), help="只列本次受影响的项")
    parser.add_argument("--plan", action="store_true", help="列出可选项，或只查看本次所选入口")
    parser.add_argument("--output", type=Path, help="本次新的结果目录")
    args = parser.parse_args()
    selected = list(dict.fromkeys(args.checks))
    if args.plan:
        for name in selected or DESCRIPTIONS:
            print(f"{name}\t{DESCRIPTIONS[name]}\t{COMMANDS.get(name) or str(SCRIPTS[name][0])}")
        return
    if not selected:
        parser.error("请选择受影响的项，先用 --plan 查看")
    if any(name not in OFFLINE for name in selected) and not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("实机复验请使用独立游戏的 dev.py exec")
    output = (args.output or PROJECT / "artifacts/skill_rebuild/regressions" /
              datetime.now().strftime("%Y%m%d-%H%M%S-%f")).resolve()
    output.mkdir(parents=True, exist_ok=False)
    write_json(output / "selection.json", {"selected": selected})
    for name in selected:
        step = output / name
        step.mkdir()
        print(f"{DESCRIPTIONS[name]} · {step}", flush=True)
        command = [value.format(output=step) for value in COMMANDS[name]] if name in COMMANDS else None
        write_json(step / "request.json", {"command": command, "script": str(SCRIPTS[name][0]) if command is None else None})
        try:
            if command is None:
                run_game_script(name, step)
            else:
                with (step / "run.log").open("w", encoding="utf-8") as log:
                    subprocess.run(command, cwd=PROJECT, stdout=log, stderr=subprocess.STDOUT, check=True)
        except (OSError, RuntimeError, subprocess.CalledProcessError) as error:
            write_json(step / "check.json", {"passed": False, "error": str(error)})
            parser.exit(1, f"复验未通过，见 {step}\n")
        write_json(step / "check.json", {"passed": True})
    print(f"通过 · {output}", flush=True)


if __name__ == "__main__":
    main()
