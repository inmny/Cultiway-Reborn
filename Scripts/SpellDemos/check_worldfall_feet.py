#!/usr/bin/env python3
"""对照现有 Worldfall 原模型核对足部制作区域，不创建身体或装备样本。"""
import argparse
import json
import os
from pathlib import Path
from common import PROJECT, write_json


def preview(models, destination):
    """并排查看原模型及共用裁面实际输出，仅生成开发预览。"""
    from PIL import Image, ImageDraw
    cell_w, cell_h = 400, 220
    sheet = Image.new('RGB', (cell_w * 2, cell_h * len(models)), '#252b33')
    draw = ImageDraw.Draw(sheet)
    for row, model in enumerate(models):
        legs = [part for part in model['parts'] if part['name'].startswith('leg_')]
        for column in range(2):
            offset_x, offset_y = column * cell_w, row * cell_h
            title = model['family'] + (' / source body' if column == 0 else f" / foot top {model['top']}")
            draw.text((offset_x + 10, offset_y + 8), title, fill='white')
            if not legs:
                draw.text((offset_x + 10, offset_y + 35), 'No actual foot or leg', fill='#a4a9af')
                continue
            selected = model['parts'] if column == 0 else [legs[0]]
            feet = model['feet'] if column == 0 else [part for part in model['feet'] if part['name'] == legs[0]['name']]
            points = [part['positions'][index:index + 3] for part in selected for index in range(0, len(part['positions']), 3)]
            lo_y, hi_y = min(p[1] for p in points), max(p[1] for p in points)
            lo_z, hi_z = min(p[2] for p in points), max(p[2] for p in points)
            scale = min((cell_w - 50) / max(hi_y - lo_y, .001), (cell_h - 55) / max(hi_z - lo_z, .001))

            def project(point):
                """保留真实模型纵向和高度的同一比例。"""
                return (offset_x + 25 + (point[1] - lo_y) * scale,
                        offset_y + cell_h - 18 - (point[2] - lo_z) * scale)

            for parts, gold in ((selected, False), (feet, True)):
                faces = []
                for part in parts:
                    raw = part['positions']
                    vertices = [raw[index:index + 3] for index in range(0, len(raw), 3)]
                    for index in range(0, len(part['triangles']), 3):
                        face = [vertices[v] for v in part['triangles'][index:index + 3]]
                        faces.append(face)
                for face in sorted(faces, key=lambda face: sum(point[0] for point in face)):
                    draw.polygon([project(point) for point in face], fill='#edb75a' if gold else '#87939f')
            if column == 1:
                line_y = project([0, lo_y, model['top']])[1]
                draw.line((offset_x + 15, line_y, offset_x + cell_w - 15, line_y), fill='#e0bd85')
                px, py = project(legs[0]['pivot'])
                draw.ellipse((px - 3, py - 3, px + 3, py + 3), fill='white')
    destination.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(destination)


def main():
    """复用已有独立三维安装，只读取实际模型资源。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--preview', type=Path, help='开发区域预览，需要 Pillow')
    args = parser.parse_args()
    install = PROJECT / 'artifacts/skill_rebuild/view-switch/environment/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    baseline.connect(True, validate_runtime=False)
    folder = PROJECT / 'artifacts/skill_rebuild/body-data/worldfall'
    folder.mkdir(parents=True, exist_ok=True)
    result = baseline.call('run', str(Path(__file__).with_suffix('.script')), '60000')
    write_json(folder / 'result.json', result)
    if args.preview:
        preview(json.loads((folder / 'models.json').read_text()), args.preview)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
