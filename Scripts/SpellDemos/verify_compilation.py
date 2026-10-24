#!/usr/bin/env python3
"""按范围验证构造编译、改造顺序、缺项和版本重建，保存本次输入与结果。"""
import argparse
import datetime
import hashlib
import json
from pathlib import Path
import shutil

from common import PROJECT, version_stamp, write_json
from benchmark_baselines import private_installation
import verify_view_switch as view

# 每个脚本只运行一次；顺序固定，资源发布检查最后执行。
SUITES = {
    "compile": ("构造编译与执行预算", (
        ("verify_operation_contracts", "operation-contracts"),
        ("verify_output_bindings", "output-bindings"),
        ("verify_construction_budget", "construction-budget"),
    )),
    "layers": ("改造优先次序", (("verify_definition_layers", "definition-layers"),)),
    "missing": ("资源、部位与实际输入缺项", (("verify_construction_requirements", "construction-requirements"),)),
    "versions": ("定义与资源版本重建", (("verify_compilation_versions", "compiler-versions"),)),
}
INPUT_FOLDERS = (
    "Content/Skills", "Content/Forms", "Doc/技能系统全面重构/构造输入", "Doc/技能系统全面重构/合成输入",
    "Doc/技能系统全面重构/编译验证输入", "Scripts/WorldSaveTests/Fixtures/world-format-1",
)


def digest(path):
    """按实际字节记录输入，而非只记录文件时间。"""
    return hashlib.sha256(path.read_bytes()).hexdigest()


def prepare_inputs(folder):
    """保存定义及固定世界副本，脚本读取本轮快照。"""
    records = []
    for name in INPUT_FOLDERS:
        for source in sorted((PROJECT / name).rglob("*")):
            if not source.is_file():
                continue
            relative = source.relative_to(PROJECT)
            destination = folder / "inputs" / relative
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)
            records.append(dict(path=relative.as_posix(), sha256=digest(destination)))
    write_json(folder / "inputs.json", records)
    return records


def prepare_resources(folder):
    """只复制可修改的形体说明；其余内容通过只读使用的链接加载。"""
    content = folder / "versions" / "resources" / "Content"
    content.mkdir(parents=True)
    for source in (PROJECT / "Content").iterdir():
        destination = content / source.name
        if source.name == "Forms":
            shutil.copytree(folder / "inputs/Content/Forms", destination)
        else:
            destination.symlink_to(source, target_is_directory=source.is_dir())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--suite", nargs="+", choices=tuple(SUITES), help="仅运行所选范围；缺省运行四类")
    parser.add_argument("--list", action="store_true", help="只列出范围，不启动游戏")
    parser.add_argument("--output", type=Path, help="项目内尚不存在的结果目录")
    parser.add_argument("--worldfall", type=Path, default=Path("/data/Downloads/S3COND2_WORLDFALL"))
    args = parser.parse_args()
    if args.list:
        for key, (label, scripts) in SUITES.items():
            print(key + "：" + label + "；" + "、".join(name for name, _ in scripts))
        return
    selected = [key for key in SUITES if args.suite is None or key in args.suite]
    folder = (args.output or PROJECT / "artifacts/skill_rebuild/compilation-checks" /
              datetime.datetime.now().strftime("%Y%m%d-%H%M%S-%f")).resolve()
    if not folder.is_relative_to(PROJECT):
        parser.error("结果目录须位于本项目内，游戏脚本使用项目相对路径")
    folder.mkdir(parents=True, exist_ok=False)
    relative = folder.relative_to(PROJECT).as_posix()
    inputs = prepare_inputs(folder)
    if "versions" in selected:
        prepare_resources(folder)
    version, _ = version_stamp()
    planned = [(key, name, previous) for key in selected for name, previous in SUITES[key][1]]
    metadata = dict(version=version, selected=selected, scripts=[name for _, name, _ in planned])
    write_json(folder / "metadata.json", metadata)
    results = []
    summary = dict(passed=False, selected=selected, results=results, checks=0)
    try:
        private_installation("cultiway", root=view.ROOT / "environment", worldfall=args.worldfall.resolve())
        metadata["connection"] = view.baseline.connect(True)
        write_json(folder / "metadata.json", metadata)
        for suite, name, previous in planned:
            target = folder / suite / name
            target.mkdir(parents=True, exist_ok=True)
            original = Path(__file__).with_name(name + ".script")
            source = original.read_text()
            source = source.replace("artifacts/skill_rebuild/" + previous, target.relative_to(PROJECT).as_posix())
            for name_in in INPUT_FOLDERS:
                if name_in != "Content/Forms":
                    source = source.replace(name_in, relative + "/inputs/" + name_in)
            # 资源副本位于范围目录，不受本项脚本输出目录的深度影响。
            source = source.replace("COMPILATION_RESOURCE_COPY", relative + "/versions/resources")
            source = source.replace("COMPILATION_INPUT_COPY", relative + "/inputs")
            script = target / "input.script"
            script.write_text(source)
            write_json(target / "source.json", dict(path=original.relative_to(PROJECT).as_posix(),
                       sha256=digest(original), executedSha256=digest(script)))
            try:
                result = view.baseline.call("run", str(script), "120000")
            except Exception as error:
                (target / "error.txt").write_text(str(error))
                results.append(dict(name=name, suite=suite, passed=False, error=str(target / "error.txt")))
                raise
            write_json(target / "result.json", result)
            count = result["checks"]
            row = dict(name=name, suite=suite, passed=result["passed"] is True,
                       checks=count if isinstance(count, int) else len(count))
            results.append(row)
            summary["checks"] += row["checks"]
            write_json(folder / "summary.json", summary)
            print(json.dumps(row, ensure_ascii=False), flush=True)
            if not row["passed"]:
                raise RuntimeError(name + " 未通过，后续项目没有执行")
        after, _ = version_stamp()
        summary["sameContent"] = after["sha256"] == version["sha256"]
        summary["sameInputs"] = all(digest(PROJECT / row["path"]) == row["sha256"] for row in inputs)
        summary["passed"] = len(results) == len(planned) and summary["sameContent"] and summary["sameInputs"]
        if not summary["passed"]:
            raise RuntimeError("验证期间代码、资源或输入发生改变，本轮不计为同版本通过")
    finally:
        write_json(folder / "summary.json", summary)
        print("结果：" + str(folder), flush=True)
    print(json.dumps(dict(passed=True, checks=summary["checks"], scripts=len(results)), ensure_ascii=False), flush=True)


if __name__ == "__main__":
    main()
