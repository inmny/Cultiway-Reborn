#!/usr/bin/env python3
"""在独立游戏中测量六种实际战斗负载，逐场保留输入和逐帧记录。"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import time

import baseline_scenarios as baseline
from common import PROJECT, version_stamp, write_json


CASES = ("ordinary", "solo", "duel", "groups", "domains", "moving")
SCRIPTS = Path(__file__).resolve().parent
POOL = PROJECT / "Doc/技能系统全面重构/构造输入/蚀甲毒潭.json"


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--world", type=Path, required=True, help="现有平坦世界的存档目录")
    parser.add_argument("--case", choices=(*CASES, "all"), action="append",
                        help="指定一个场景，可重复提供，默认运行全部")
    parser.add_argument("--frame-limit", type=int, default=60)
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请从 develop 的独立游戏入口执行")
    if args.frame_limit < 1:
        parser.error("帧率上限应为正数")
    world = args.world.resolve()
    world_hash = hashlib.sha256((world / "map.wbox").read_bytes()).hexdigest()
    output = Path(os.environ["CULTIWAY_DEV_OUTPUT"]) / datetime.now(timezone.utc).strftime("combat-%Y%m%dT%H%M%S.%fZ")
    connection = baseline.connect(False)
    version, _ = version_stamp()
    source = (SCRIPTS / "benchmark_combat_load.script").read_text()
    source = source.replace("// COMBAT_MEASUREMENT_HELPER", (SCRIPTS / "frame_measurement.script").read_text())
    cases = CASES if not args.case or "all" in args.case else dict.fromkeys(args.case)
    failed = []
    for case in cases:
        if not wait_for_game_slot(connection["pid"]):
            failed.append(case)
            print(f"{case} 等待其他游戏退出后再测", flush=True)
            continue
        folder = output / case
        folder.mkdir(parents=True)
        request = dict(scenario=case, repeat=0, worldPath=str(world), worldSha256=world_hash,
                       profile=connection["profile"], version=version, connection=connection,
                       frameLimit=args.frame_limit, warmupSeconds=0, sampleSeconds=50,
                       seed=1312, definitionPath=str(POOL), output=str(folder))
        write_json(folder / "request.json", request)
        if case == "domains":
            (folder / POOL.name).write_bytes(POOL.read_bytes())
        script = folder / "measurement.script"
        script.write_text(source.replace("__REQUEST_PATH__", json.dumps(str(folder / "request.json"))))
        started = datetime.now(timezone.utc).isoformat()
        response = baseline.call("run", str(script), "85000")
        concurrent = other_games(connection["pid"])
        write_json(folder / "host.json", dict(started=started, finished=datetime.now(timezone.utc).isoformat(),
                                             otherGamePids=concurrent))
        write_json(folder / "response.json", response)
        if concurrent:
            failed.append(case)
            print(f"{case} 采样时出现其他游戏，保留记录。{folder}", flush=True)
            continue
        if not response["passed"]:
            failed.append(case)
            print(f"{case} 未形成预定负载，见 {folder}", flush=True)
            continue
        print(f'{case}: {response["frames"]} 帧，p95 {response["p95Ms"]:.2f}ms，'
              f'世界内 {response["worldSeconds"]:.2f} 秒。{folder}', flush=True)
    if failed:
        raise RuntimeError(f"未通过的场景：{', '.join(failed)}")


def other_games(own_pid):
    return sorted(int(pid.name) for pid in Path("/proc").iterdir()
                  if pid.name.isdecimal() and int(pid.name) != own_pid and is_worldbox(pid))


def wait_for_game_slot(own_pid):
    """给其他独立游戏的短用例留出结束时间。"""
    deadline = time.monotonic() + 55
    while other_games(own_pid):
        if time.monotonic() >= deadline:
            return False
        time.sleep(0.5)
    return True


def is_worldbox(pid):
    try:
        return (pid / "comm").read_text().strip() == "worldbox" and baseline.running(int(pid.name))
    except (FileNotFoundError, ProcessLookupError, PermissionError):
        return False


if __name__ == "__main__":
    main()
