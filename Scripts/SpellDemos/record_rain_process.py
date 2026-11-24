#!/usr/bin/env python3
"""用原版雨云存读验证录制冷启动后仍在继续的天气。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, version_stamp, write_json


def save_fingerprint(folder):
    """手工搬动输出目录后仍可核对同一份存档及其预期状态。"""
    files = (folder / "save/map.wbox", folder / "restart-expected.json")
    hashes = {}
    for path in files:
        with path.open("rb") as stream:
            hashes[str(path.relative_to(folder))] = hashlib.file_digest(stream, "sha256").hexdigest()
    return hashes


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("phase", choices=("prepare", "resume"))
    parser.add_argument("--archive", type=Path, help="准备阶段输出目录，供新的游戏进程读入")
    parser.add_argument("--output", type=Path, required=True, help="本阶段结果的上级目录")
    args = parser.parse_args()
    resuming = args.phase == "resume"
    if resuming != bool(args.archive):
        parser.error("只有续录阶段需要提供准备目录")
    if not os.environ.get("CULTIWAY_DEV_ACTIVE"):
        parser.error("请通过独立游戏的 dev.py exec 运行")

    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    folder = Path(tempfile.mkdtemp(prefix=f"rain-{args.phase}-", dir=output))
    if resuming:
        archive = args.archive.resolve()
        receipt = json.loads((archive / "receipt.json").read_text(encoding="utf-8"))
        if not receipt["passed"] or save_fingerprint(archive) != receipt["saved"]:
            parser.error("雨云续录需要准备阶段的原始存档和预期进度")
        shutil.copytree(archive / "save", folder / "save")
        shutil.copy2(archive / "restart-expected.json", folder)

    script = Path(__file__).with_name("verify_rain_process_restart.script" if resuming else "verify_rain_process.script")
    body = script.read_text(encoding="utf-8").replace(
        'folder=Path.Combine(root,"artifacts/skill_rebuild/rain-process")', f"folder={json.dumps(str(folder))}", 1
    )
    fixture = PROJECT / "Scripts/WorldSaveTests/Fixtures/world-format-1"
    body = body.replace('Path.Combine(root,"Scripts/WorldSaveTests/Fixtures/world-format-1")',
                        json.dumps(str(fixture)), 1)
    if not resuming:
        body = body.replace("const bool verifyLegacySource = true;", "const bool verifyLegacySource = false;", 1)
    program = folder / "input.script"
    program.write_text(body, encoding="utf-8")
    version, _ = version_stamp()
    connection = baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call("run", str(program), "180000")
    except RuntimeError as error:
        write_json(folder / "result.json", dict(passed=False, error=str(error)))
        raise RuntimeError(f"天气验证未通过，见 {folder}") from error
    write_json(folder / "result.json", result)
    write_json(folder / "receipt.json", dict(passed=result["passed"], saved=save_fingerprint(folder),
                                             version=version, connection=connection, phase=args.phase))
    if not result["passed"]:
        raise RuntimeError(f"天气验证未通过，见 {folder}")
    if resuming:
        encode(output, dict(key=folder.name, name="雨云冷读续行", label="继续降雨", variant=1,
                            trajectory="原天气路径", modifiers=[]), keep_frames=True)
    print(folder, flush=True)


if __name__ == "__main__":
    main()
