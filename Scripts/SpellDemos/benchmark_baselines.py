#!/usr/bin/env python3
"""顺序测量原版和模组基线；私有加载器目录隔离模组启用状态。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time

from common import PROJECT, version_stamp, write_json

ROOT = PROJECT / "artifacts/skill_rebuild/performance-baseline"
SCRIPTS = Path(__file__).resolve().parent


def private_installation(mode, root=ROOT, worldfall=None):
    """只共享不写入的游戏文件；加载器缓存、启用记录和日志均为私有副本。"""
    source = PROJECT.parents[1]
    game = root / mode / "game"
    game.mkdir(parents=True, exist_ok=True)
    for name in ("worldbox", "UnityPlayer.so", "libdoorstop.so"):
        target = game / name
        if not target.exists():
            os.link(source / name, target)
    data = game / "worldbox_Data"
    if not data.exists():
        shutil.copytree(source / "worldbox_Data", data, copy_function=os.link,
                        ignore=lambda directory, names: ["mods"] if Path(directory).name == "StreamingAssets" else [])
    native_mods = data / "StreamingAssets/mods"
    native_mods.mkdir(exist_ok=True)
    for path in (source / "worldbox_Data/StreamingAssets/mods").glob("NeoModLoader.*"):
        target = native_mods / path.name
        if target.exists() and target.stat().st_nlink > 1:
            target.unlink()
        if not target.exists():
            shutil.copy2(path, target)
    # 复制会被加载器重写的资料，不与玩家安装目录共用硬链接。
    for name in ("Assemblies", "Assembly-CSharp-Publicized.dll", "commit"):
        original = source / "worldbox_Data/StreamingAssets/mods/NML" / name
        target = native_mods / "NML" / name
        if not target.exists():
            target.parent.mkdir(parents=True, exist_ok=True)
            if original.is_dir():
                shutil.copytree(original, target)
            else:
                shutil.copy2(original, target)
    for name in ("core", "config"):
        target = game / "BepInEx" / name
        if not target.exists():
            shutil.copytree(source / "BepInEx" / name, target)
    mods = game / "Mods"
    mods.mkdir(exist_ok=True)
    for name in (["DebugToolbox", "Cultiway"] if mode == "cultiway" else ["DebugToolbox"]):
        target = mods / name
        if not target.exists():
            target.symlink_to(PROJECT.parent / name, target_is_directory=True)
    if worldfall is not None:
        target = mods / "Worldfall"
        target.mkdir(exist_ok=True)
        # 原包译文辅助源码会在程序集回调里重入安装补丁。独立验证直接使用原DLL的NML入口。
        # DLL放顶层才能被加载器识别为已编译模组；不修改或分发下载目录中的任何文件。
        for source_path, name in ((worldfall / "mod.json", "mod.json"),
                                  (worldfall / "icon.png", "icon.png"),
                                  (worldfall / "Assemblies/Worldfall.dll", "Worldfall.dll")):
            shutil.copy2(source_path, target / name)
    actual = sorted(p.name for p in mods.iterdir())
    expected = sorted(["DebugToolbox", "Cultiway"] if mode == "cultiway" else ["DebugToolbox"])
    if worldfall is not None:
        expected = sorted([*expected, "Worldfall"])
    if actual != expected:
        raise RuntimeError(f"私有模组目录含额外条目：{actual}")
    return game / "worldbox"


def worker(args):
    """一个进程只测一种加载环境；各轮重载同一正式世界。"""
    import baseline_scenarios as baseline
    state = baseline.connect(True, validate_runtime=args.worker == "cultiway")
    output = ROOT / "runs" / args.stamp
    output.mkdir(parents=True, exist_ok=True)
    try:
        if args.worker == "cultiway":
            subprocess.run([sys.executable, str(SCRIPTS / "baseline_scenarios.py"), "prepare"],
                           cwd=PROJECT, env=baseline.ENV, check=True)
        for repeat in range(args.repeat):
            scenes = ("none", "ordinary", "apex", "process") if args.worker == "cultiway" else ("none",)
            for scene in scenes:
                version, _ = version_stamp()
                prepared = ROOT / "cultiway/worlds" / scene / "baseline.json"
                metadata = json.loads(prepared.read_text())
                request = dict(action="measure", scenario=scene, repeat=repeat,
                               profile=str(baseline.PROFILE), version=version, inputs=metadata["inputs"],
                               output=str(output), configPath=str(SCRIPTS / "baseline_scenarios.json"),
                               worldPath=str(ROOT / "cultiway/worlds/none"),
                               frameLimit=-1, warmupSeconds=args.warmup, sampleSeconds=args.seconds,
                               resultPath=str(output / f"{args.worker}-{scene}-{repeat}.json"))
                write_json(baseline.ROOT / "request.json", request)
                source = (SCRIPTS / ("baseline_scenarios.script" if args.worker == "cultiway" else "native_baseline.script")).read_text()
                helper = (SCRIPTS / "frame_measurement.script").read_text()
                if args.worker == "cultiway":
                    source = source.replace('// BASELINE_MEASUREMENT_HELPER\nasync Task<JObject> Measure(Func<int[]> counts) => throw new InvalidOperationException("请使用benchmark_baselines.py执行计时");', helper)
                else:
                    source = source.replace("// BASELINE_MEASUREMENT_HELPER", helper)
                variable = "measured" if args.worker == "cultiway" else "result"
                source = source.replace("return " + variable + ";", 'File.WriteAllText((string)request["resultPath"], ' + variable + '.ToString()); return new { passed = true };')
                script = output / (args.worker + "-measurement.script")
                script.write_text(source)
                baseline.call("run", str(script), "180000")
                result = json.loads(Path(request["resultPath"]).read_text())
                # 游戏写原始数值，避免调试通道将复杂对象转成句柄。
                if not isinstance(result, dict) or "passed" not in result:
                    raise RuntimeError(f"采样返回格式错误：{result}")
                result["mode"] = args.worker
                result["request"] = request
                result["connection"] = state
                write_json(output / f"{args.worker}-{scene}-{repeat}.json", result)
                print(f'{args.worker}/{scene}/{repeat}: {result["meanMs"]:.2f}ms, '
                      f'p95 {result["p95Ms"]:.2f}ms, 世界速度 {result["actualWorldSpeed"]:.3f}', flush=True)
    finally:
        subprocess.run([sys.executable, str(SCRIPTS / "baseline_scenarios.py"), "quit"],
                       cwd=PROJECT, env=baseline.ENV, check=True)
        deadline = time.monotonic() + 15
        while baseline.running(state["pid"]) and time.monotonic() < deadline:
            time.sleep(.2)
        if baseline.running(state["pid"]):
            raise RuntimeError("本测试游戏尚未退出，不能并行启动下一个环境")


def summarize(output, repeats):
    """核对场景实际负载和原版起点，汇总保留逐轮数据，失败不认领基线。"""
    records = [json.loads(path.read_text()) for path in sorted(output.glob("*-*-*.json"))]
    checks = []

    def check(value, message):
        if not value:
            raise RuntimeError(message)
        checks.append(message)

    check(len(records) == 5 * repeats, "五种条件的全部重复结果齐全")
    version, _ = version_stamp()
    groups = {}
    for record in records:
        key = record["mode"] + "/" + record["scenario"]
        groups.setdefault(key, []).append(record)
        label = key + "/" + str(record["repeat"])
        check(record["passed"] and record["request"]["version"]["sha256"] == version["sha256"], label + "来源内容一致")
        check(record["resolution"] == [1280, 720] and record["frameLimit"] == -1 and record["vsync"] == 0,
              label + "实际画面及采样设置符合输入")
        check(record["frames"] == len(record["frameMilliseconds"]) and record["realSeconds"] >= record["request"]["sampleSeconds"]
              and record["worldSeconds"] > 0 and len(record["samples"]) >= 10, label + "记录完整且世界实际推进")
        check(all(len(sample["counts"]) == 5 for sample in record["samples"]), label + "五类主体及内存记录齐全")
        if record["mode"] == "native":
            check(record["cultiwayLoaded"] is False, label + "没有加载Cultiway")
        elif record["scenario"] == "ordinary":
            check(max(sample["counts"][1] for sample in record["samples"]) > 0, label + "旧执行主体实际活动")
        elif record["scenario"] == "apex":
            check(max(sample["counts"][2] for sample in record["samples"]) > 0, label + "新构造实际活动")
        elif record["scenario"] == "process":
            check(max(sample["counts"][4] for sample in record["samples"]) > 0, label + "启动施放结束后雨云实际持续")
        else:
            check(all(sample["counts"][1:4] == [0, 0, 0] for sample in record["samples"]), label + "无技能场景没有活动技能主体")
    columns = ["name", "id", "asset", "x", "y", "health", "maximumHealth", "damage", "alive"]
    reference = groups["cultiway/none"][0]
    expected = [{key: actor[key] for key in columns} for actor in reference["initial"]]
    for record in groups["native/none"] + groups["cultiway/none"]:
        actual = [{key: actor[key] for key in columns} for actor in record["initial"]]
        check(actual == expected and record["camera"] == reference["camera"],
              record["mode"] + "/" + str(record["repeat"]) + "原版角色起点、数值及镜头一致")
    summary = {}
    for key, items in groups.items():
        samples = [sample for item in items for sample in item["samples"]]
        summary[key] = dict(repeats=len(items), frames=sum(item["frames"] for item in items),
                            meanMs=[item["meanMs"] for item in items], p95Ms=[item["p95Ms"] for item in items],
                            p99Ms=[item["p99Ms"] for item in items], actualWorldSpeed=[item["actualWorldSpeed"] for item in items],
                            sampledPeakCounts=[max(s["counts"][i] for s in samples) for i in range(5)],
                            workingSetMiB=[min(s["workingSetBytes"] for s in samples)/2**20, max(s["workingSetBytes"] for s in samples)/2**20],
                            managedMiB=[min(s["managedBytes"] for s in samples)/2**20, max(s["managedBytes"] for s in samples)/2**20])
    source = PROJECT.parents[1]
    binaries = {name: hashlib.sha256((source / name).read_bytes()).hexdigest() for name in
                ["worldbox", "UnityPlayer.so", "worldbox_Data/Managed/Assembly-CSharp.dll",
                 "worldbox_Data/StreamingAssets/mods/NeoModLoader.dll"]}
    report = dict(passed=True, checks=checks, version=version, binaries=binaries,
                  device=reference["device"], countColumns=["actors", "legacySkills", "constructedCasts", "visualInstances", "rainClouds"],
                  summary=summary)
    write_json(output / "verification.json", report)
    print(f"基线核对：{len(checks)}项通过", flush=True)


def main():
    """建立两份隔离安装并串行取样，原始逐帧记录与版本一并保留。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--repeat", type=int, default=2)
    parser.add_argument("--seconds", type=float, default=20)
    parser.add_argument("--warmup", type=float, default=4)
    parser.add_argument("--worker", choices=["cultiway", "native"], help=argparse.SUPPRESS)
    parser.add_argument("--stamp", help=argparse.SUPPRESS)
    args = parser.parse_args()
    if not 1 <= args.repeat <= 10 or args.seconds < 5 or args.warmup < 1:
        parser.error("重复1至10次，采样至少5秒，预热至少1秒")
    if args.worker:
        worker(args)
        return
    live = subprocess.run(["pgrep", "-a", "-x", "worldbox"], capture_output=True, text=True)
    if live.returncode == 0:
        raise RuntimeError("请先结束已有WorldBox，避免多进程争用GPU：" + live.stdout.strip())
    stamp = time.strftime("%Y%m%d-%H%M%S")
    for mode in ("cultiway", "native"):
        game = private_installation(mode)
        environment = dict(os.environ, CULTIWAY_BASELINE_ROOT=str(ROOT / mode),
                           CULTIWAY_BASELINE_GAME=str(game),
                           CULTIWAY_BASELINE_REQUIRE_CULTIWAY="1" if mode == "cultiway" else "0")
        subprocess.run([sys.executable, str(Path(__file__).resolve()), "--worker", mode, "--stamp", stamp,
                        "--repeat", str(args.repeat), "--seconds", str(args.seconds), "--warmup", str(args.warmup)],
                       cwd=PROJECT, env=environment, check=True)
    summarize(ROOT / "runs" / stamp, args.repeat)
    print("原始结果：" + str(ROOT / "runs" / stamp), flush=True)


if __name__ == "__main__":
    main()
