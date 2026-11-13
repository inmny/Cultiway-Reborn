#!/usr/bin/env python3
"""对照原版实际图片快速检查兽形部位资料，可导出制作叠图。"""
import argparse
import json
import math
from pathlib import Path
import sys

PROJECT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(PROJECT))
from PIL import Image, ImageDraw
from Scripts.organ_overlay_preview.compositor import _load_native_frame


def check():
    """检查资源尺寸、实际像素、原关节和各足承接，不运行战斗矩阵。"""
    catalog = json.loads((PROJECT / 'Content/BodyVisuals/animal-regions.json').read_text())
    roles = catalog['Roles']
    pictures = []
    for actor, frames in catalog['Actors'].items():
        for name, frame in frames.items():
            label = actor + '/' + name
            source, _, _ = _load_native_frame(actor, name)
            width, height = source.size
            if (frame['Width'], frame['Height']) != source.size or len(frame['Rows']) != height or any(len(row) != width for row in frame['Rows']):
                raise ValueError(label + '：制作尺寸不匹配原帧')
            present = set()
            for y, row in enumerate(frame['Rows']):
                for x, glyph in enumerate(row):
                    if glyph not in roles:
                        raise ValueError(label + '：未知部位字母')
                    if glyph not in '.H' and not source.getpixel((x, y))[3]:
                        raise ValueError(f'{label}：部位像素{x},{height - 1 - y}不在原图中')
                    if source.getpixel((x, y))[3]:
                        present.add(roles[glyph])
            for part, point in frame['Joints'].items():
                if len(point) != 2 or any(not math.isfinite(v) for v in point):
                    raise ValueError(label + '：原关节坐标无效')
                if part == 'head':
                    continue
                x, y = min(width - 1, int(point[0])), min(height - 1, int(point[1]))
                if not 0 <= point[0] <= width or not 0 <= point[1] <= height or not source.getpixel((x, height - 1 - y))[3]:
                    raise ValueError(label + '：原关节不在实际身体中：' + part)
            parents = frame.get('Parents', {})
            for part in frame.get('Supports', []):
                bone = parents.get(part)
                if part not in present or bone not in frame['Joints'] or parents.get(bone) != 'torso' or 'torso' not in frame['Joints']:
                    raise ValueError(label + '：足面或原承接缺失：' + part)
            if frame.get('Supports'):
                pictures.append((label, source, frame))
    return catalog, pictures


def preview(pictures, destination):
    """原像素保持不变，仅在开发叠图中标示制作区域和关节。"""
    palette = {'F': '#df5555', 'f': '#ffb0b0', 'R': '#d29339', 'r': '#ffdf97',
               'B': '#4c7fc8', 'b': '#a4c8ff', 'D': '#39988a', 'd': '#92e5cf'}
    columns, cell_w, cell_h, scale = 3, 350, 165, 10
    sheet = Image.new('RGB', (columns * cell_w, math.ceil(len(pictures) / columns) * cell_h), '#272b34')
    draw = ImageDraw.Draw(sheet)
    for index, (name, source, frame) in enumerate(pictures):
        x, y = index % columns * cell_w + 10, index // columns * cell_h + 24
        tile = Image.new('RGBA', source.size, '#494b55')
        tile.alpha_composite(source)
        tile = tile.resize((source.width * scale, source.height * scale), Image.Resampling.NEAREST)
        paint = ImageDraw.Draw(tile)
        for iy, row in enumerate(frame['Rows']):
            for ix, glyph in enumerate(row):
                if glyph in palette:
                    paint.rectangle((ix * scale, iy * scale, (ix + 1) * scale - 1, (iy + 1) * scale - 1), fill=palette[glyph])
        for part, point in frame['Joints'].items():
            if part == 'head':
                continue
            px, py = point[0] * scale, (frame['Height'] - point[1]) * scale
            paint.line((px - 3, py, px + 3, py), fill='white', width=1)
            paint.line((px, py - 3, px, py + 3), fill='white', width=1)
        sheet.paste(tile.convert('RGB'), (x, y))
        draw.text((x, y - 18), name, fill='white')
    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(destination)


def main():
    """按需检查并查看现有制作表，不生成模组图片。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--preview', type=Path, help='开发叠图的输出位置')
    args = parser.parse_args()
    _, pictures = check()
    if args.preview:
        preview(pictures, args.preview)
    print('原版兽形部位资料与原图一致')


if __name__ == '__main__':
    main()
