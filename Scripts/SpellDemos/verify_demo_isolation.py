#!/usr/bin/env python3
"""在同一个独立原版游戏检查普通录像、取消恢复和存档隔离。"""
import argparse
import json
import os
from pathlib import Path
import signal
import subprocess
import sys
import time

from common import CONTROL, FIXTURE_DIR, PROJECT, file_sha256, input_fingerprint, rpc, write_json


def restored_state(folder, expected_error=None):
    deadline = time.monotonic() + 65
    status = {}
    while time.monotonic() < deadline:
        path = folder / "status.json"
        if path.is_file():
            status = json.loads(path.read_text())
            if status.get("done"):
                break
        time.sleep(.25)
    if not status.get("done") or not status["fixtureLoaded"] or not status["sceneRestored"]:
        raise RuntimeError(f"测试夹具没有恢复：{folder}")
    error = status["error"]
    if (expected_error is None and error is not None) or (expected_error and expected_error not in (error or "")):
        raise RuntimeError(f"演示结束原因不同：{folder}")
    state = rpc("eval", """
        var runner = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.MonoBehaviour>()
            .Single(value => value != null && value.GetType().Name == "CultiwaySpellDemoRunner");
        var busy = runner.GetType().GetField("busy",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        return new { busy = (bool)busy.GetValue(runner), paused = Config.paused, hidden = Config.ui_main_hidden,
            controlled = ControllableUnit.getControllableUnit() != null,
            visuals = Cultiway.Core.Visuals.Forms.FormVisualScene.Instances.Count,
            actors = World.world.units.getSimpleList().Select(actor => actor.id).OrderBy(id => id).ToArray(),
            demoActors = World.world.units.getSimpleList().Count(actor => actor.data.name.StartsWith("法术演示·")) };""")
    if state["busy"] or not state["paused"] or state["controlled"] or state["visuals"] or state["demoActors"]:
        raise RuntimeError(f"退出后仍存在临时演示状态：{state}")
    return dict(status=status, state=state)


def fireball(output, name, *options, duration="1.5"):
    return [sys.executable, str(Path(__file__).with_name("demo_spell.py")), "Cultiway.Fireball",
            "--cases", "base", "--duration", duration, "--fps", "8", "--output", str(output / name), *options]


def run(output, name, expected_exit, *options, env=None):
    with (output / f"{name}.log").open("w") as log:
        result = subprocess.run(fireball(output, name, *options), stdout=log, stderr=subprocess.STDOUT,
                                env=env, timeout=80)
    if result.returncode != expected_exit:
        raise RuntimeError(f"演示退出结果不同，见 {log.name}")
    return Path(log.name).read_text()


def cancel_recording(output):
    folder = output / "cancel"
    with (output / "cancel.log").open("w") as log:
        process = subprocess.Popen(fireball(output, "cancel", duration="20"), stdout=log, stderr=subprocess.STDOUT)
        deadline = time.monotonic() + 50
        while process.poll() is None and time.monotonic() < deadline:
            if next(folder.glob("*/frames/00000.png"), None):
                break
            time.sleep(.1)
        process.send_signal(signal.SIGINT)
        if process.wait(timeout=25) != 130 or not next(folder.glob("*/frames/00000.png"), None):
            raise RuntimeError("没有在实际录制期间成功取消")
    return restored_state(folder, "已取消演示")


def saves(profile):
    fixture = PROJECT / FIXTURE_DIR
    files = dict(worlds={}, testBackups={})
    for path in sorted(profile.rglob("*.wbox")):
        relative = str(path.relative_to(profile))
        group = "testBackups" if "/Cultiway/Backups/worlds/" in relative else "worlds"
        files[group][relative] = file_sha256(path)
    return dict(fixture=input_fingerprint(PROJECT, (FIXTURE_DIR,))[0],
                fixtureMap=file_sha256(fixture / "map.wbox"), **files)


def check_saves(before, after):
    if before["fixture"] != after["fixture"] or before["worlds"] != after["worlds"]:
        raise RuntimeError("测试夹具或保存的世界发生了变化")
    for name, digest in before["testBackups"].items():
        if after["testBackups"].get(name) != digest:
            raise RuntimeError(f"测试配置中已有的备份发生了变化：{name}")
    for name in after["testBackups"].keys() - before["testBackups"].keys():
        if after["testBackups"][name] != before["fixtureMap"]:
            raise RuntimeError(f"测试配置产生了其他世界的备份：{name}")


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--files-only", action="store_true", help="只补核对一次普通录制前后的存档文件")
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请使用独立原版游戏的 dev.py exec")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    profile = Path(os.environ["CULTIWAY_DEV_PROFILE"])

    before = saves(profile)
    write_json(output / "saves-before.json", before)
    hidden = rpc("eval", "return Config.ui_main_hidden;")
    run(output, "normal", 0)
    normal = restored_state(output / "normal")
    write_json(output / "normal-check.json", normal)
    if args.files_only:
        after = saves(profile)
        check_saves(before, after)
        write_json(output / "delivery.json", dict(passed=True, normal=normal, savedBefore=before, savedAfter=after))
        print(output / "delivery.json", flush=True)
        return
    request = file_sha256(CONTROL / "request.json")
    other_profile = dict(os.environ, CULTIWAY_DEV_PROFILE=str(output / "another-profile"))
    refused = run(output, "wrong-profile", 1, env=other_profile)
    listed = run(output, "readonly", 0, "--list", "--filter", "Fireball", env=other_profile)
    if ("不是本工具的独立测试配置" not in refused or "Cultiway.Fireball" not in listed or
            request != file_sha256(CONTROL / "request.json")):
        raise RuntimeError("目录查询或拒绝录制修改了已有请求")
    run(output, "failed", 1, "--require-source", "content.core_formation")
    failed = restored_state(output / "failed", "没有来自指定入口的正式授予")
    write_json(output / "failed-check.json", failed)
    cancelled = cancel_recording(output)
    write_json(output / "cancel-check.json", cancelled)
    run(output, "again", 0)
    again = restored_state(output / "again")
    after = saves(profile)
    write_json(output / "again-check.json", again)
    write_json(output / "saves-after.json", after)
    check_saves(before, after)
    states = [result["state"] for result in (normal, failed, cancelled, again)]
    if any(state["hidden"] != hidden or state["actors"] != states[0]["actors"] for state in states):
        raise RuntimeError("恢复后的界面或世界人物不一致")
    write_json(output / "delivery.json", dict(passed=True, normal=normal, failed=failed, cancelled=cancelled,
               again=again, savedBefore=before, savedAfter=after, requestUnchangedOnRefusal=True))
    print(output / "delivery.json", flush=True)


if __name__ == "__main__":
    main()
