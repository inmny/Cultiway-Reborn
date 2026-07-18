#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""运行法宝视觉重制原型。"""

from __future__ import annotations

import argparse
from pathlib import Path

from .pipeline import run_prototype


def main() -> int:
    parser = argparse.ArgumentParser(description="验证 Blockbench 法宝模型到多层像素实体的离线管线")
    parser.add_argument(
        "--output",
        type=Path,
        default=Path("artifacts/artifact_visual_prototype"),
        help="输出目录（默认 artifacts/artifact_visual_prototype）",
    )
    parser.add_argument("--keep", action="store_true", help="保留输出目录中的既有文件")
    args = parser.parse_args()
    report = run_prototype(args.output, clean=not args.keep)
    print(f"output={report.output}")
    print(f"models=16 views={len(report.records)} deterministic=true")
    print(f"manifest={report.manifest_path}")
    print(f"sheet={report.contact_sheet_path}")
    for record in report.records:
        print(
            f"{record.instance}/{record.view}: size={record.size} "
            f"opaque={record.opaque_pixels} hash={record.png_hash[:12]}"
        )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
