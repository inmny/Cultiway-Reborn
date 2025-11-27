#!/usr/bin/env python3
# AI Generated
import datetime
import subprocess
import sys
from pathlib import Path


def ensure_repo():
    """确认当前目录在 git 仓库内。"""
    try:
        subprocess.run(["git", "rev-parse", "--is-inside-work-tree"], check=True, capture_output=True)
    except subprocess.CalledProcessError:
        print("不在 git 仓库内，无法统计。", file=sys.stderr)
        sys.exit(1)


def collect_stats(since: str):
    """执行 git log --numstat 获取自指定时间以来的增删行数。"""
    cmd = [
        "git",
        "log",
        "--no-merges",
        f"--since={since}",
        "--numstat",
        "--pretty=format:%H",
    ]
    try:
        result = subprocess.run(cmd, check=True, capture_output=True, text=True)
    except FileNotFoundError:
        print("未找到 git 命令。", file=sys.stderr)
        sys.exit(1)
    except subprocess.CalledProcessError as exc:
        print(f"git 命令失败: {exc.stderr}", file=sys.stderr)
        sys.exit(exc.returncode)

    added_total = 0
    removed_total = 0
    for line in result.stdout.splitlines():
        parts = line.split("\t")
        if len(parts) != 3:
            continue  # 跳过提交行或非 numstat 行
        added, removed, _ = parts
        if added == "-" or removed == "-":  # 二进制文件
            continue
        try:
            added_total += int(added)
            removed_total += int(removed)
        except ValueError:
            continue
    return added_total, removed_total


def main():
    """统计今天 00:00 起的代码增删行数。"""
    ensure_repo()
    today = datetime.date.today().isoformat() + " 00:00"
    added, removed = collect_stats(today)
    print(f"统计区间: {today} 之后")
    print(f"增加行数: {added}")
    print(f"删除行数: {removed}")
    print(f"净增行数: {added - removed}")


if __name__ == "__main__":
    main()
