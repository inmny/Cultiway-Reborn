#!/usr/bin/env python3
"""在独立游戏内验证连续战斗、切换时回收和正式冷读。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, version_stamp, write_json


def file_hash(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def mod_snapshot(folder):
    return json.loads((folder.parent.parent / "inputs.json").read_text())["sha256"]


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--world", type=Path, help="复用平坦战场的存档目录")
    parser.add_argument("--other", type=Path, default=PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1")
    parser.add_argument("--seconds", type=int, default=300, help="连续运行的实际秒数")
    parser.add_argument("--cycles", type=int, default=4, help="暂停、视角往返和换世界的轮数")
    continuation = parser.add_mutually_exclusive_group()
    continuation.add_argument("--cold-run", type=Path, help="已正常退出的长期复验输出目录")
    continuation.add_argument("--transitions-from", type=Path, help="保留已完成的 300 秒样本，单独补测切换")
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请通过独立游戏的 dev.py exec 运行")
    if args.world is None and args.cold_run is None:
        parser.error("请指定平坦战场的存档目录")
    if args.seconds <= 0 or args.cycles <= 0:
        parser.error("持续时间及循环数应为正数")

    connection = baseline.connect(False)
    output = Path(tempfile.mkdtemp(prefix="runtime-stability-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    version, _ = version_stamp()
    mod_input = mod_snapshot(output)
    request = dict(profile=connection["profile"], connection=connection, output=str(output),
                   version=version, modSha256=mod_input, scenario="moving", seed=1312, sampleSeconds=args.seconds,
                   cycles=args.cycles, warmupSeconds=0, frameLimit=60, repeat=0)
    here = Path(__file__).resolve().parent
    if args.cold_run:
        previous = args.cold_run.resolve()
        if mod_snapshot(previous) != mod_input:
            raise RuntimeError("冷读游戏的模组内容与保存时不同")
        prior = json.loads((previous / "result.json").read_text())
        exit_report = json.loads((previous / "exit.json").read_text())
        archive = previous / "archive"
        if not prior["passed"] or exit_report["process"] != prior["process"] or baseline.running(prior["process"]):
            raise RuntimeError("冷读需要已正常退出的原测试进程")
        if any(exit_report[key] for key in ("nativeBufferBytes", "worldfallBufferBytes", "worldfallDraws")):
            raise RuntimeError("退出回调仍记录着战斗绘制资源")
        before = json.loads((previous / "archive-identity.json").read_text())
        if file_hash(archive / "map.wbox") != before["mapSha256"]:
            raise RuntimeError("原正式档已改变")
        request.update(worldPath=str(archive), archiveIdentity=before,
                       formerProcess=prior["process"])
        source = (here / "verify_runtime_stability_cold.script").read_text()
    else:
        world = args.world.resolve()
        other = args.other.resolve()
        request.update(worldPath=str(world), worldSha256=file_hash(world / "map.wbox"),
                       otherWorld=str(other), otherSha256=file_hash(other / "map.wbox"))
        if args.transitions_from:
            previous = args.transitions_from.resolve()
            if mod_snapshot(previous) != mod_input:
                raise RuntimeError("切换补测与原连续运行的模组内容不同")
            old = json.loads((previous / "request.json").read_text())
            soak = json.loads((previous / "soak.json").read_text())
            if soak["realSeconds"] < 300 or soak["soakWaves"] < 8 or soak["soakDamage"] <= 0:
                raise RuntimeError("接续需要完整长跑与实际结算的原记录")
            if any(old[key] != request[key] for key in ("worldSha256", "otherSha256")):
                raise RuntimeError("切换补测须使用相同的原世界和另一份存档")
            identity = json.loads((previous / "archive-identity.json").read_text())
            shutil.copytree(previous / "archive", output / "archive")
            identity["mapSha256"] = file_hash(output / "archive" / "map.wbox")
            if identity["mapSha256"] != file_hash(previous / "archive" / "map.wbox"):
                raise RuntimeError("原正式档复制校验失败")
            write_json(output / "archive-identity.json", identity)
            request.update(reusedSoak=str(previous), soakProcess=soak["process"], archiveIdentity=identity)
        prefix, marker, _ = (here / "benchmark_combat_load.script").read_text().partition("// COMBAT_MEASUREMENT_HELPER")
        if not marker:
            raise RuntimeError("原飞剑负载入口已改变")
        view = "Func<Task> beginView = null; Action endView = null; JObject observedView = null;\n"
        source = prefix + (here / "frame_measurement.script").read_text() + "\n" + view
        source += (here / "combat_worldfall_profile.script").read_text() + "\n"
        source += (here / "verify_runtime_stability.script").read_text()
    request_path = output / "request.json"
    write_json(request_path, request)
    script = output / "measurement.script"
    script.write_text(source.replace("__REQUEST_PATH__", json.dumps(str(request_path))))
    print(output, flush=True)
    seconds = 65 if args.cold_run else args.cycles * 55 + 100
    if not args.cold_run and not args.transitions_from:
        seconds += args.seconds
    result = baseline.call("run", str(script), str(seconds * 1000))
    write_json(output / "response.json", result)
    if not result["passed"]:
        raise RuntimeError(f"运行复验失败，见 {output}")
    if not args.cold_run:
        archive = output / "archive"
        identity = json.loads((output / "archive-identity.json").read_text())
        identity["mapSha256"] = file_hash(archive / "map.wbox")
        write_json(output / "archive-identity.json", identity)
        if file_hash(world / "map.wbox") != request["worldSha256"] or file_hash(other / "map.wbox") != request["otherSha256"]:
            raise RuntimeError("复用的原存档发生变化")


if __name__ == "__main__":
    main()
