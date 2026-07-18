#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""重建全部 Blockbench 可编辑的法器运行时模型。"""

from __future__ import annotations

import argparse
from pathlib import Path

from .model_rebuild import rebuild_catalog


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument(
        "--catalog",
        type=Path,
        default=Path("Content/Artifacts/AppearanceCatalog"),
        help="AppearanceCatalog 路径",
    )
    parser.add_argument("--check", action="store_true", help="只检查当前输出是否与重制配方一致")
    args = parser.parse_args(argv)
    stats = rebuild_catalog(args.catalog, check=args.check)
    if args.check and stats.changed_files:
        print(f"有 {stats.changed_files} 个模型文件与重制配方不一致")
        return 1
    action = "验证" if args.check else "重建"
    print(
        f"已{action} {stats.modules} 个 module、{stats.variants} 个 variant、"
        f"{stats.faces} 个面；写入变化 {stats.changed_files} 个文件"
    )
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
