#!/usr/bin/env python3
"""在独立游戏捕获正式创法输入，或用同一份资料重放并比较结果。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shlex
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, version_stamp, write_json


def comparable(result):
    """复现会分配新编号，比较其余完整构造、拒绝原因及实际授予变化。"""
    definition = result.get("definition")
    if definition is not None:
        definition = {key: value for key, value in definition.items() if key != "Id"}
    return dict(outcome=result["outcome"], reason=result["reason"], definition=definition,
                known=result["knownAfter"] - result["knownBefore"],
                registered=result["directoryAfter"] - result["directoryBefore"])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    modes = parser.add_subparsers(dest="mode", required=True)
    capture = modes.add_parser("capture", help="从正式世界受理指定种子的请求")
    capture.add_argument("--world", type=Path, required=True, help="含 map.wbox 的正式存档目录")
    capture.add_argument("--actor", type=int, required=True, help="存档里实际人物编号")
    capture.add_argument("--seed", type=int, required=True, help="固定创法种子")
    capture.add_argument("--source", help="指定人物已掌握的原术编号")
    replay = modes.add_parser("replay", help="使用已捕获的完整资料和期待结果")
    replay.add_argument("file", type=Path, help="捕获输出的 replay.json")
    replay.add_argument("--world", type=Path, help="原档移到新路径时指定同内容目录")
    for mode in (capture, replay):
        mode.add_argument("--output", type=Path, help="新结果目录，缺省在本次独立运行内创建")
    args = parser.parse_args()
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请通过独立游戏的 dev.py exec 运行")

    recording = None
    if args.mode == "replay":
        recording = json.loads(args.file.read_text(encoding="utf-8"))
        if recording["schema"] != 1:
            parser.error("不支持此份创法复现资料版本")
    world = (args.world or Path(recording["world"]["path"])).resolve()
    map_file = world / "map.wbox"
    digest = hashlib.sha256(map_file.read_bytes()).hexdigest()
    if recording is not None and digest != recording["world"]["sha256"]:
        parser.error("原档指纹不一致，请选择捕获时的同一份地图")

    started_version, _ = version_stamp()
    connection = baseline.connect(False)
    if args.output is None:
        output = Path(tempfile.mkdtemp(prefix="creation-replay-", dir=os.environ["CULTIWAY_DEV_OUTPUT"]))
    else:
        output = args.output.resolve()
        output.mkdir(parents=True, exist_ok=False)
    request = output / "request.json"
    observed_path = output / "observed.json"
    selection = (dict(actor=args.actor, seed=args.seed, sourceId=args.source) if recording is None
                 else recording["input"])
    write_json(request, dict(mode=args.mode, world=str(world), input=selection, profile=connection["profile"],
                             observation=str(observed_path)))
    script = output / "request.script"
    source = Path(__file__).with_suffix(".script").read_text(encoding="utf-8")
    script.write_text(source.replace("// CREATION_REPLAY_REQUEST",
        "var request = JObject.Parse(File.ReadAllText(" + json.dumps(str(request)) + "));"), encoding="utf-8")
    baseline.call("run", str(script), "90000")
    observation = json.loads(observed_path.read_text(encoding="utf-8"))
    if hashlib.sha256(map_file.read_bytes()).hexdigest() != digest:
        raise RuntimeError("运行期间原档内容变化，不能用本轮作同档复现")
    if version_stamp()[0]["sha256"] != started_version["sha256"]:
        raise RuntimeError("运行期间代码或资源变化，不能用本轮作同版本复现")

    snapshot = observation.pop("input")
    result = observation["result"]
    observed = comparable(result)
    expected = observed if recording is None else recording["expected"]
    same_input = None if recording is None else snapshot == selection
    matches = None if recording is None else same_input and observed == expected
    saved = dict(schema=1, world=dict(path=str(world), sha256=digest), input=snapshot,
                 capturedVersion=started_version if recording is None else recording["capturedVersion"],
                 capturedRuntime=observation["runtime"] if recording is None else recording.get("capturedRuntime"),
                 expected=expected)
    write_json(output / "replay.json", saved)
    write_json(output / "result.json", dict(result, version=started_version, runtime=observation["runtime"],
                                           sameInput=same_input, matches=matches, replay=args.mode == "replay"))
    if result["definition"] is not None:
        write_json(output / "definition.json", result["definition"])
    script_name = str(Path(__file__).resolve().relative_to(PROJECT))
    dev = ["python3", ".agents/skills/develop/scripts/dev.py", "--root", os.environ["CULTIWAY_DEV_ROOT"],
           "--name", os.environ["CULTIWAY_DEV_ENV"].partition("-")[2], "exec", "--"]
    command = shlex.join([*dev, "python3", script_name, "replay", str(output / "replay.json")])
    comparison = ("已保存本次结果用于复现。" if matches is None else
                  "固定输入和结果均与捕获记录一致。" if matches else "与捕获记录有差异，详见 result.json。")
    lines = ["# 创法复现", "", f"人物 {snapshot['actor']}，种子 {snapshot['seed']}，规则 {snapshot['inspiration']['Rules']}。",
             f"运行提交 `{started_version['commit']}`，游戏版本 `{observation['runtime']['game']}`。", "",
             f"结果为{('成功取得构造' if result['outcome'] == 'created' else '拒绝')}。" + (result["reason"] or ""),
             comparison, "",
             "在同一独立游戏环境运行以下命令。", "", "```sh", command, "```"]
    if result["definition"] is not None:
        demo = shlex.join([*dev, "python3", "Scripts/SpellDemos/demo_spell.py", "--construction",
                           str(output / "definition.json"), "--cases", "base"])
        lines += ["", "成功构造可从独立演示入口试放。", "", "```sh", demo, "```"]
    (output / "复现说明.md").write_text("\n".join(lines) + "\n", encoding="utf-8")
    print(output, flush=True)
    if matches is False:
        raise SystemExit("固定输入的实际结果与已捕获结果不一致")


if __name__ == "__main__":
    main()
