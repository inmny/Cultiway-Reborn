#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""pixel_text —— 像素图与字符矩阵互相转换，让 LLM 用「写字」的方式画像素图。

## 格式（.txt，一个文件 = 调色板 + 矩阵，自包含）

    # 注释以 # 开头
    palette:
      . = transparent
      K = #1a1c1c
      W = #f8f4e8
    grid:
    KKKKKKKK
    KWWWWWWK
    KKKKKKKK

- 一个字符 = 一个像素；矩阵行数与行宽 = 图片高与宽，任意分辨率；
- 大小写敏感；可用字符是防混淆集合：`.`、a-z（不含 l o）、A-Z（不含 I O）、2-9，
  共 57 个，其中 `.` 按约定表示透明；
- 颜色写 `#rgb` / `#rrggbb` / `#rrggbbaa`，`transparent` 表示全透明。

## 命令

    # 图 → 字符矩阵（自动生成颜色映射；不透明色超过 56 种时自动量化）
    python Scripts/pixel_text.py to-text 图.png -o 图.txt [--colors 24] [--alpha-threshold 128]
    # 字符矩阵 → 图（--scale 用最近邻整数放大导出）
    python Scripts/pixel_text.py to-image 图.txt -o 图.png [--scale 4]
    # 快捷预览：默认 8 倍放大，输出 <名字>.preview.png
    python Scripts/pixel_text.py preview 图.txt [--scale 8]

## LLM 作画工作流

1. 直接手写 txt：palette 里定义颜色映射，grid 里画矩阵 → `to-image` 渲染；
2. 改现有图：`to-text` 导出矩阵 → 改矩阵或调色板 → `to-image` 渲染；
3. 换色 = 只改 palette 行，矩阵不动；所有迭代都发生在文本层面。
"""

from __future__ import annotations

import argparse
import sys
from collections import Counter
from pathlib import Path

from PIL import Image

# 防混淆字符集：排除 l o I O 0 1（写矩阵时不会认错）。`.` 固定表示透明。
SAFE_CHARS = "." + "abcdefghijkmnpqrstuvwxyz" + "ABCDEFGHJKLMNPQRSTUVWXYZ" + "23456789"
COLOR_CHARS = SAFE_CHARS[1:]
MAX_COLORS = len(COLOR_CHARS)


class PixelTextError(ValueError):
    """格式错误，消息面向使用者。"""


def _parse_color(value: str) -> tuple[int, int, int, int]:
    text = value.strip()
    if text.lower() == "transparent":
        return (0, 0, 0, 0)
    if not text.startswith("#"):
        raise PixelTextError(f"颜色必须是 #hex 或 transparent，收到 {value!r}")
    hex_part = text[1:]
    if len(hex_part) == 3:
        hex_part = "".join(ch * 2 for ch in hex_part)
    if len(hex_part) not in (6, 8):
        raise PixelTextError(f"颜色长度非法: {value!r}（支持 #rgb #rrggbb #rrggbbaa）")
    r, g, b = int(hex_part[0:2], 16), int(hex_part[2:4], 16), int(hex_part[4:6], 16)
    a = int(hex_part[6:8], 16) if len(hex_part) == 8 else 255
    return (r, g, b, a)


def parse_text(text: str, source: str = "<text>") -> tuple[dict[str, tuple[int, int, int, int]], list[str]]:
    """解析字符矩阵文本，返回（调色板，矩阵行）。"""
    palette: dict[str, tuple[int, int, int, int]] = {}
    grid: list[str] = []
    section = None
    for line_no, raw in enumerate(text.splitlines(), start=1):
        line = raw.rstrip("\r")
        stripped = line.strip()
        if not stripped or stripped.startswith("#"):
            continue
        lowered = stripped.lower().rstrip(":").strip()
        if stripped.rstrip(":").lower() == "palette":
            section = "palette"
            continue
        if stripped.rstrip(":").lower() == "grid":
            section = "grid"
            continue
        if section == "palette":
            if "=" not in stripped:
                raise PixelTextError(f"{source}:{line_no} 调色板行缺少 '=': {stripped!r}")
            char, _, value = stripped.partition("=")
            char = char.strip()
            if len(char) != 1 or char not in SAFE_CHARS:
                raise PixelTextError(
                    f"{source}:{line_no} 调色板字符 {char!r} 不在可用字符集内"
                    f"（可用: 点 + a-z 除 l o + A-Z 除 I O + 2-9）"
                )
            if char in palette:
                raise PixelTextError(f"{source}:{line_no} 调色板字符重复定义: {char!r}")
            palette[char] = _parse_color(value)
        elif section == "grid":
            if "palette:" not in lowered and "grid:" not in lowered:
                if line != line.rstrip():
                    raise PixelTextError(f"{source}:{line_no} 矩阵行不能有行首缩进或行尾空白: {line!r}")
                for ch in line:
                    if ch not in SAFE_CHARS:
                        raise PixelTextError(f"{source}:{line_no} 矩阵出现非法字符 {ch!r}（列 {line.index(ch) + 1}）")
                grid.append(line)
        else:
            raise PixelTextError(f"{source}:{line_no} 内容出现在 palette:/grid: 段之前: {stripped!r}")

    if not palette:
        raise PixelTextError(f"{source} 缺少 palette: 段")
    if not grid:
        raise PixelTextError(f"{source} 缺少 grid: 段（至少要有一行矩阵）")
    width = len(grid[0])
    for index, row in enumerate(grid, start=1):
        if len(row) != width:
            raise PixelTextError(f"{source} 矩阵第 {index} 行宽度 {len(row)} 与第 1 行 {width} 不一致")
        for ch in row:
            if ch not in palette:
                raise PixelTextError(f"{source} 矩阵第 {index} 行的字符 {ch!r} 没有在 palette 中定义")
    return palette, grid


def render_text(text_path: Path, out_path: Path, scale: int = 1) -> Image.Image:
    """字符矩阵 → PNG（scale 为最近邻整数放大）。"""
    palette, grid = parse_text(Path(text_path).read_text(encoding="utf-8"), str(text_path))
    height, width = len(grid), len(grid[0])
    image = Image.new("RGBA", (width, height))
    pixels = image.load()
    for y, row in enumerate(grid):
        for x, ch in enumerate(row):
            pixels[x, y] = palette[ch]
    if scale > 1:
        image = image.resize((width * scale, height * scale), Image.Resampling.NEAREST)
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    image.save(out_path)
    return image


def export_text(
    image_path: Path,
    out_path: Path,
    colors: int | None = None,
    alpha_threshold: int = 128,
) -> Path:
    """PNG → 字符矩阵文本。颜色超过上限时按 --colors 量化。"""
    image = Image.open(image_path).convert("RGBA")
    source = image.load()
    # 第一遍：硬化透明度，逐像素记录 (x, y, 颜色或 None=透明)
    flat: list[tuple[int, int, tuple[int, int, int, int] | None]] = []
    counter: Counter[tuple[int, int, int, int]] = Counter()
    for y in range(image.height):
        for x in range(image.width):
            r, g, b, a = source[x, y]
            if a < alpha_threshold:
                flat.append((x, y, None))
            else:
                entry = (r, g, b, 255)
                flat.append((x, y, entry))
                counter[entry] += 1

    limit = max(1, min(colors or MAX_COLORS, MAX_COLORS))
    if len(counter) > limit:
        # 量化后用量化色的调色板整体替换（原色不再出现）
        quantized = image.quantize(colors=limit, method=Image.Quantize.FASTOCTREE).convert("RGBA")
        q_pixels = quantized.load()
        counter = Counter()
        remapped_flat = []
        for x, y, entry in flat:
            if entry is None:
                remapped_flat.append((x, y, None))
                continue
            r, g, b, _ = q_pixels[x, y]
            new_entry = (r, g, b, 255)
            remapped_flat.append((x, y, new_entry))
            counter[new_entry] += 1
        flat = remapped_flat

    # 按使用频次降序分配字符（同频次按颜色值排序，保证确定性）
    ordered = sorted(counter.items(), key=lambda item: (-item[1], item[0]))
    if len(ordered) > MAX_COLORS:
        raise PixelTextError(f"量化后仍有 {len(ordered)} 色，超过上限 {MAX_COLORS}")
    mapping: dict[tuple[int, int, int, int], str] = {}
    palette_lines = ["  . = transparent"]
    for index, (color, _count) in enumerate(ordered):
        char = COLOR_CHARS[index]
        mapping[color] = char
        palette_lines.append(f"  {char} = #{color[0]:02x}{color[1]:02x}{color[2]:02x}")

    rows = ["".join("." if entry is None else mapping[entry] for _x, _y, entry in flat[row_start:row_start + image.width])
            for row_start in range(0, len(flat), image.width)]

    header = f"# pixel_text  尺寸 {image.width}x{image.height}  色数 {len(ordered)}"
    out_path = Path(out_path)
    out_path.parent.mkdir(parents=True, exist_ok=True)
    out_path.write_text(
        "\n".join([header, "palette:", *palette_lines, "grid:", *rows, ""]), encoding="utf-8",
    )
    return out_path


def main(argv: list[str] | None = None) -> int:
    parser = argparse.ArgumentParser(prog="pixel_text", description="像素图 ⇄ 字符矩阵 双向转换")
    sub = parser.add_subparsers(dest="command", required=True)

    p_text = sub.add_parser("to-text", help="图 → 字符矩阵")
    p_text.add_argument("image", type=Path)
    p_text.add_argument("-o", "--out", type=Path)
    p_text.add_argument("--colors", type=int, help="限制色数（默认全保留，超过 56 才量化）")
    p_text.add_argument("--alpha-threshold", type=int, default=128)

    p_image = sub.add_parser("to-image", help="字符矩阵 → 图")
    p_image.add_argument("text", type=Path)
    p_image.add_argument("-o", "--out", type=Path)
    p_image.add_argument("--scale", type=int, default=1)

    p_preview = sub.add_parser("preview", help="字符矩阵 → 8 倍放大预览")
    p_preview.add_argument("text", type=Path)
    p_preview.add_argument("--scale", type=int, default=8)

    args = parser.parse_args(argv)

    try:
        if args.command == "to-text":
            default_out = args.image.with_suffix(".txt")
            out = export_text(args.image, args.out or default_out, args.colors, args.alpha_threshold)
            print(f"[to-text] {args.image} -> {out}")
        elif args.command == "to-image":
            if not args.out:
                raise PixelTextError("to-image 需要用 -o 指定输出路径")
            image = render_text(args.text, args.out, args.scale)
            print(f"[to-image] {args.text} -> {args.out} ({image.width}x{image.height})")
        elif args.command == "preview":
            source = Path(args.text)
            out = source.with_name(source.stem + ".preview.png")
            image = render_text(source, out, args.scale)
            print(f"[preview] {source} -> {out} ({image.width}x{image.height})")
        return 0
    except (PixelTextError, OSError) as error:
        print(f"错误: {error}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
