#!/usr/bin/env python3
"""在独立游戏中测量六种实际战斗负载，逐场保留输入和逐帧记录。"""
import argparse
from datetime import datetime, timezone
import hashlib
from itertools import product
import json
import os
from pathlib import Path
import subprocess
import sys
from threading import Event, Thread
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
    parser.add_argument("--frame-limit", type=int)
    parser.add_argument("--fps-check", action="store_true", help="关闭帧率上限，检查常用交战的 60 帧目标")
    parser.add_argument("--repeat", type=int, help="每个场景的独立次数")
    parser.add_argument("--seed", type=int, default=1312, help="首轮随机种子，复测逐轮加一")
    parser.add_argument("--profile", action="store_true", help="同时采集阶段时间、线程、分配和提交")
    parser.add_argument("--measure-allocations", action="store_true", help="另测 Linux 游戏的托管对象分配")
    parser.add_argument("--view", choices=("native", "worldfall"), default="native")
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请从 develop 的独立游戏入口执行")
    frame_limit = args.frame_limit if args.frame_limit is not None else (-1 if args.fps_check else 60)
    repeats = args.repeat if args.repeat is not None else (2 if args.fps_check else 1)
    if frame_limit < 1 and not (args.fps_check and frame_limit == -1):
        parser.error("帧率上限应为正数")
    if repeats < 1:
        parser.error("复测次数应为正数")
    fps_cases = ("ordinary", "groups")
    cases = tuple(dict.fromkeys(args.case)) if args.case and "all" not in args.case else (fps_cases if args.fps_check else CASES)
    if args.fps_check and (args.profile or frame_limit != -1 or any(case not in fps_cases for case in cases)):
        parser.error("常用帧率检查使用普通混战和四线交战，关闭帧率上限和逐阶段采集")
    if args.measure_allocations and (not args.profile or not sys.platform.startswith("linux")):
        parser.error("托管对象计数需要 Linux 和 --profile")
    if args.view == "worldfall" and not (args.profile or args.fps_check):
        parser.error("三维负载使用 --profile 或 --fps-check 核对实际视角和绘制")
    world = args.world.resolve()
    world_hash = hashlib.sha256((world / "map.wbox").read_bytes()).hexdigest()
    output = Path(os.environ["CULTIWAY_DEV_OUTPUT"]) / datetime.now(timezone.utc).strftime("combat-%Y%m%dT%H%M%S.%fZ")
    connection = baseline.connect(False)
    version, _ = version_stamp()
    source = (SCRIPTS / "benchmark_combat_load.script").read_text()
    if args.profile:
        source = "using Unity.Profiling;\n" + source.replace(
            "// COMBAT_FRAME_PROFILE", (SCRIPTS / "combat_frame_profile.script").read_text())
    if args.measure_allocations:
        output.mkdir()
        library = output / "libcultiway-allocations.so"
        subprocess.run(["cc", "-O2", "-shared", "-fPIC", str(SCRIPTS / "mono_allocation_counter.c"),
                        "-ldl", "-o", str(library)], check=True)
        bindings = (SCRIPTS / "combat_mono_allocations.script").read_text()
        source = source.replace("// MONO_ALLOCATION_COUNTER", bindings.replace(
            "__ALLOCATION_LIBRARY__", json.dumps(str(library))))
    if args.view == "worldfall":
        source = source.replace("// COMBAT_VIEW_SETUP", (SCRIPTS / "combat_worldfall_profile.script").read_text())
    source = source.replace("// COMBAT_MEASUREMENT_HELPER", (SCRIPTS / "frame_measurement.script").read_text())
    failed = []
    for case, repeat in product(cases, range(repeats)):
        label = f"{case}-{repeat + 1}" if repeats > 1 else case
        request = dict(scenario=case, repeat=repeat, worldPath=str(world), worldSha256=world_hash,
                       profile=connection["profile"], version=version, connection=connection,
                       frameLimit=frame_limit, warmupSeconds=0, sampleSeconds=65 if args.fps_check else 50,
                       seed=args.seed + repeat, definitionPath=str(POOL), captureProfile=args.profile,
                       view=args.view, measureAllocations=args.measure_allocations, fpsCheck=args.fps_check)
        if not measure_case(output, label, request, source):
            failed.append(label)
    if failed:
        raise RuntimeError(f"未通过的场景：{', '.join(failed)}")


def measure_case(output, label, request, source):
    fps_check = request["fpsCheck"]
    own_pid = request["connection"]["pid"]
    for attempt in range(1, 4 if fps_check else 2):
        if not wait_for_game_slot(own_pid):
            print(f"{label} 等待其他游戏退出后再测", flush=True)
            continue
        folder = output / (label if attempt == 1 else f"{label}-retry{attempt - 1}")
        folder.mkdir(parents=True)
        write_json(folder / "request.json", dict(request, output=str(folder)))
        if request["scenario"] == "domains":
            (folder / POOL.name).write_bytes(POOL.read_bytes())
        script = folder / "measurement.script"
        script.write_text(source.replace("__REQUEST_PATH__", json.dumps(str(folder / "request.json"))))
        response, host = capture_case(script, own_pid, fps_check)
        write_json(folder / "host.json", host)
        write_json(folder / "response.json", response)
        if host["otherGamePids"]:
            print(f"{label} 采样时出现其他游戏，保留记录。{folder}", flush=True)
            continue
        if not response["passed"]:
            print(f"{label} 未形成预定负载，见 {folder}", flush=True)
            return False
        print(f'{label}: {response["frames"]} 帧，p95 {response["p95Ms"]:.2f}ms，'
              f'世界内 {response["worldSeconds"]:.2f} 秒。{folder}', flush=True)
        return True
    return False


def capture_case(script, own_pid, fps_check):
    """帧率复核用实际计时窗口判断其他游戏的重叠。"""
    started = datetime.now(timezone.utc).isoformat()
    observations = []
    stop = Event()

    def observe():
        while not stop.wait(0.5):
            active = other_games(own_pid)
            if active:
                observations.append((time.time_ns() // 1_000_000, active))

    watcher = Thread(target=observe) if fps_check else None
    if watcher is not None:
        watcher.start()
    try:
        response = baseline.call("run", str(script), "105000" if fps_check else "85000")
    finally:
        if watcher is not None:
            stop.set()
            watcher.join()
    if fps_check and "wallStartUnixMs" in response:
        start, end = response["wallStartUnixMs"], response["wallEndUnixMs"]
        concurrent = sorted({pid for stamp, pids in observations if start <= stamp <= end for pid in pids})
    else:
        concurrent = other_games(own_pid)
    return response, dict(started=started, finished=datetime.now(timezone.utc).isoformat(),
                          otherGamePids=concurrent, observations=observations)


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
