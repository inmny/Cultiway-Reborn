#!/usr/bin/env python3
"""复用既有铁剑双人场景，录制4.25的真实接触、脱手及恢复。"""
import argparse
import json
import subprocess
import sys
from pathlib import Path

from common import PROJECT, write_json
from record_body_connections import encode

CASES = {
    "brace": ("兵器架挡", "normal"),
    "deflect": ("主动兵器拨压", "deflect"),
    "passive": ("普通持物拨压", "passive"),
    "disarm": ("真实脱手与原物品拾取", "disarm-active"),
}


def main():
    """指定一段或依次录制四段，实际帧按世界时间编码并汇总索引。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--native", action="store_true", help="使用独立原版游戏")
    parser.add_argument("--case", nargs="+", choices=list(CASES), help="只录制指定行为")
    parser.add_argument("--encode-only", action="store_true", help="只编码已有实际帧")
    args = parser.parse_args()
    mode = "native" if args.native else "worldfall"
    output = PROJECT / "artifacts/skill_rebuild/weapon-connection-films" / mode
    output.mkdir(parents=True, exist_ok=True)
    manifest = output / ("manifest" + ("-" + "-".join(args.case) if args.case else "") + ".json")
    write_json(manifest, dict(passed=False, status="running", view=mode))
    rows = []
    for case in args.case or CASES:
        label, sample = CASES[case]
        folder = PROJECT / "artifacts/skill_rebuild" / ("weapon-connections-native" if args.native else "weapon-connections") / ("cases-" + sample) / "film"
        if not args.encode_only:
            command = [sys.executable, str(Path(__file__).with_name("verify_weapon_connections.py")), "--case", sample, "--film"]
            if args.native:
                command.append("--native")
            log = output / f"{case}-run.log"
            with log.open("w") as stream:
                result = subprocess.run(command, cwd=PROJECT, stdout=stream, stderr=subprocess.STDOUT)
            if result.returncode:
                raise RuntimeError("场景未完成，见 " + str(log))
        if not json.loads((folder / "result.json").read_text())["passed"]:
            raise RuntimeError("原场景未通过，不能作为验收录像：" + str(folder))
        # 三维首张位于施放前，可能仍是旧世界发布纹理；从第一张施放后帧开始，完整保留接触和恢复。
        rows.append(encode(case, folder, output, args.native, label, "4.25 兵器关系", 0 if args.native else 1))
    if not args.case:
        (output / "movies.ffconcat").write_text("ffconcat version 1.0\n" + "".join(f"file '{case}.mp4'\n" for case in CASES))
        subprocess.run(["ffmpeg", "-y", "-v", "error", "-safe", "0", "-f", "concat", "-i", "movies.ffconcat", "-c", "copy", "-movflags", "+faststart", "all.mp4"], cwd=output, check=True)
    write_json(manifest, dict(passed=True, view=mode, cases=rows, timeBasis="实际世界时间；功能录像不作为负载性能证明"))


if __name__ == "__main__":
    main()
