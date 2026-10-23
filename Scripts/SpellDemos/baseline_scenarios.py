#!/usr/bin/env python3
"""在独立配置中准备、重载四种固定基线场景，使用现有调试客户端。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import subprocess
import time

from common import CLIENT, PROJECT, version_stamp, write_json

ROOT = Path(os.environ.get("CULTIWAY_BASELINE_ROOT", PROJECT / "artifacts/skill_rebuild/baseline")).resolve()
PROFILE = ROOT / "profile"
TEMP = ROOT / "temp"
ENV = dict(os.environ, XDG_CONFIG_HOME=str(PROFILE), TMPDIR=str(TEMP),
           CULTIWAY_BASELINE_ROOT=str(ROOT))
CONFIG = Path(__file__).with_name("baseline_scenarios.json")
SCRIPT = Path(__file__).with_name("baseline_scenarios.script")
GAME = Path(os.environ.get("CULTIWAY_BASELINE_GAME", PROJECT.parents[1] / "worldbox"))
REQUIRE_CULTIWAY = os.environ.get("CULTIWAY_BASELINE_REQUIRE_CULTIWAY", "1") == "1"


def running(pid):
    """进程退出或成为僵尸即为终态，避免只凭旧记录判断仍在运行。"""
    try:
        state = (Path("/proc") / str(pid) / "stat").read_text()
    except FileNotFoundError:
        return False
    return state.rsplit(")", 1)[1].split()[0] != "Z"


def call(*args):
    """调试连接与游戏使用相同临时目录，避免串入其他游戏会话。"""
    result = subprocess.run(["dotnet", str(CLIENT), *args], cwd=PROJECT,
                            env=ENV, capture_output=True, text=True)
    if result.returncode:
        raise RuntimeError(result.stderr or result.stdout)
    data = json.loads(result.stdout)
    if isinstance(data, dict) and data.get("Success") is False:
        raise RuntimeError(json.dumps(data, ensure_ascii=False, indent=2))
    return data.get("Result", data) if isinstance(data, dict) else data


def connect(launch, validate_runtime=True):
    """只连接本配置；就绪超时保留原进程，下一次继续观察同一会话。"""
    ROOT.mkdir(parents=True, exist_ok=True)
    PROFILE.mkdir(exist_ok=True)
    TEMP.mkdir(exist_ok=True)
    session = TEMP / "DebugToolbox/session.json"
    state_path = ROOT / "process.json"
    live_pid = None
    launched_process = None
    if state_path.exists():
        state = json.loads(state_path.read_text())
        process = Path("/proc") / str(state["pid"])
        if running(state["pid"]):
            environment = (process / "environ").read_bytes().split(b"\0")
            if ("XDG_CONFIG_HOME=" + str(PROFILE)).encode() not in environment:
                raise RuntimeError("记录的进程已被其他程序使用，不能连接或关闭它")
            live_pid = state["pid"]
    if live_pid is None:
        if not launch:
            raise RuntimeError("本配置没有运行中的游戏，请添加 --launch")
        # 原版允许明文配置；只写本测试目录，显式启用NML所需实验模式。
        options = json.loads(CONFIG.read_text())["playerOptions"]
        data = PROFILE / "unity3d/mkarpenko/WorldBox/worldboxData"
        data.parent.mkdir(parents=True, exist_ok=True)
        write_json(data, {"tutorialFinished": True, "list": options})
        started = time.time()
        with (ROOT / "game-console.log").open("w") as log:
            # 本机通过Doorstop加载BepInEx/NML；裸启动游戏不会加载模组。
            game_environment = dict(
                ENV, DOORSTOP_ENABLED="1", DOORSTOP_IGNORE_DISABLED_ENV="0",
                DOORSTOP_TARGET_ASSEMBLY=str(GAME.parent / "BepInEx/core/BepInEx.Preloader.dll"),
                LD_PRELOAD=str(GAME.parent / "libdoorstop.so"))
            process = subprocess.Popen(
                [str(GAME), "--class=ai-owned",
                 "-screen-width", "1280", "-screen-height", "720", "-screen-fullscreen", "0",
                 "-logFile", str(ROOT / "game.log")],
                cwd=GAME.parent, env=game_environment, stdout=log, stderr=subprocess.STDOUT,
                start_new_session=True)
        live_pid = process.pid
        launched_process = process
        write_json(state_path, {"pid": live_pid, "started": started, "profile": str(PROFILE)})
    deadline = time.monotonic() + 240
    mod_ready = ('AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetType("Cultiway.ModClass", false) != null)'
                 if REQUIRE_CULTIWAY else 'true')
    ready_code = ('Game.Resume(); return new { pid = System.Diagnostics.Process.GetCurrentProcess().Id, '
                  'profile = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME"), '
                  'data = UnityEngine.Application.persistentDataPath, '
                  'ready = !SmoothLoader.isLoading() && ' + mod_ready + ' };')
    while time.monotonic() < deadline:
        if launched_process is not None and launched_process.poll() is not None:
            raise RuntimeError("本配置游戏已经退出，请查看 baseline/game.log")
        if not running(live_pid):
            raise RuntimeError("本配置游戏已经退出，请查看 baseline/game.log")
        if session.exists():
            try:
                call("hello")
                state = call("eval", ready_code)
            except RuntimeError as error:
                # 启动中调试服务可能先就绪；保留最后错误，超时后原样报告。
                last_error = str(error)
            else:
                if state["pid"] != live_pid or state["profile"] != str(PROFILE):
                    raise RuntimeError("调试会话不属于本测试进程，停止加载世界")
                if not Path(state["data"]).is_relative_to(PROFILE):
                    raise RuntimeError("游戏实际存档目录未隔离，停止加载世界")
                if state["ready"]:
                    _, modified = version_stamp()
                    started = json.loads(state_path.read_text())["started"]
                    if validate_runtime and modified > started:
                        raise RuntimeError("代码或资源晚于本游戏启动；请先quit，再用--launch重新启动")
                    return state
        time.sleep(2)
    raise RuntimeError(f"游戏 {live_pid} 尚未就绪；保留进程和日志，请重试连接。" +
                       locals().get("last_error", ""))


def main():
    """命令边界选择固定场景，保存来源版本及每轮实际结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("action", choices=["list", "prepare", "run", "quit"])
    parser.add_argument("--scenario", default="all")
    parser.add_argument("--launch", action="store_true")
    parser.add_argument("--repeat", type=int, default=1)
    args = parser.parse_args()
    config = json.loads(CONFIG.read_text())
    choices = {s["id"]: s for s in config["scenarios"]}
    if args.scenario != "all" and args.scenario not in choices:
        parser.error("场景可选：all、" + "、".join(choices))
    if not 1 <= args.repeat <= 10:
        parser.error("重复次数须为1至10")
    if args.action == "list":
        print(json.dumps(config, ensure_ascii=False, indent=2))
        return
    connected = connect(args.launch, validate_runtime=args.action != "quit")
    if args.action == "quit":
        try:
            call("eval", "UnityEngine.Application.Quit(); return true;")
        except RuntimeError:
            # Unity退出可能先关闭调试通道；只有确认同一进程已退出才算成功。
            for _ in range(20):
                if not running(connected["pid"]):
                    break
                time.sleep(.1)
            else:
                raise
        print("已请求退出本配置游戏 " + str(connected["pid"]))
        return
    selected = list(choices) if args.scenario == "all" else [args.scenario]
    version, _ = version_stamp()
    if args.action == "prepare":
        base_metadata = ROOT / "base/baseline-source.json"
        if not base_metadata.exists() or json.loads(base_metadata.read_text())["version"]["sha256"] != version["sha256"]:
            write_json(ROOT / "request.json", {"action": "base", "scenario": "none",
                                               "profile": str(PROFILE), "version": version})
            call("run", str(SCRIPT), "120000")
            print("干净基底世界已通过原版生成与正式保存入口建立", flush=True)
    inputs = {}
    fixture = ROOT / "base"
    if not (fixture / "map.wbox").exists():
        raise RuntimeError("基底世界尚未准备，请先执行prepare")
    for path in sorted(fixture.iterdir()):
        if path.is_file():
            inputs[str(path.relative_to(PROJECT))] = hashlib.sha256(path.read_bytes()).hexdigest()
    definition = PROJECT / "Doc/技能系统全面重构/构造输入/掌击改追踪飞掌.json"
    inputs[str(definition.relative_to(PROJECT))] = hashlib.sha256(definition.read_bytes()).hexdigest()
    stamp = time.strftime("%Y%m%d-%H%M%S")
    output = ROOT / "runs" / stamp
    output.mkdir(parents=True, exist_ok=True)
    write_json(output / "metadata.json", {"version": version, "connection": connected,
                                           "config": config, "inputs": inputs, "action": args.action})
    for repeat in range(args.repeat):
        for scenario in selected:
            request = {"action": args.action, "scenario": scenario, "repeat": repeat,
                       "profile": str(PROFILE), "output": str(output), "version": version, "inputs": inputs}
            write_json(ROOT / "request.json", request)
            try:
                result = call("run", str(SCRIPT), "120000")
            except RuntimeError as error:
                write_json(output / f"{scenario}-{repeat}-failed.json",
                           {"passed": False, "request": request, "error": str(error)})
                raise
            write_json(output / f"{scenario}-{repeat}.json", result)
            if not result["passed"]:
                raise RuntimeError("场景验证未通过：" + scenario)
            print(f"{args.action} {scenario} 第{repeat + 1}轮：通过", flush=True)
    print("结果：" + str(output), flush=True)


if __name__ == "__main__":
    main()
