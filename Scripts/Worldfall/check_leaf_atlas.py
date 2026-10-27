"""核对实际叶簇运行图占用，按原生逐行摆放方式估算共享图集容量。"""
import json
import random
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
ATLAS_SIDE = 6144


def shelf_height(images, size):
    """与原生相同地保留一像素间距，返回不覆盖旧图所需的高度。"""
    x = y = height = 0
    for width, high in images:
        if x + width + 1 > size:
            y += height + 1
            x = height = 0
        x += width + 1
        height = max(height, high)
    return y + height + 1


def check():
    """只读取已发布配方，分别统计三档和全部图，并保存真实文件证据。"""
    levels = {name: set() for name in ('Near', 'Middle', 'Far', 'All')}
    rows = {}
    families = {}
    for path in sorted((ROOT / 'Content/Worldfall/LeafCards').glob('*.json')):
        document = json.loads(path.read_text(encoding='utf-8-sig'))
        family = document['AssetId']
        family_paths = families.setdefault(family, set())
        for level in ('Near', 'Middle', 'Far'):
            for card in document['Levels'][level]:
                resource = document['Textures'][card['Image']] + '.png'
                levels[level].add(resource)
                levels['All'].add(resource)
                family_paths.add(resource)
                if resource not in rows:
                    file = ROOT / 'GameResources' / resource
                    with Image.open(file) as image:
                        width, high = image.size
                    assert max(width, high) <= 512, resource
                    rows[resource] = {'File': file.relative_to(ROOT).as_posix(),
                                      'Width': width, 'Height': high}
    totals = {}
    for level, paths in levels.items():
        sizes = [(rows[path]['Width'], rows[path]['Height']) for path in sorted(paths)]
        area = sum(width * high for width, high in sizes)
        permutations = [sizes, list(reversed(sizes)), sorted(sizes, key=lambda s: -s[1])]
        for seed in range(32):
            shuffled = sizes.copy()
            random.Random(seed).shuffle(shuffled)
            permutations.append(shuffled)
        totals[level] = {'Images': len(sizes), 'Pixels': area,
                         'Exceeds2048Area': area > 2048 ** 2,
                         'ShelfHeights': [shelf_height(order, ATLAS_SIDE) for order in permutations],
                         'SampledOrdersFitAtlas': all(shelf_height(order, ATLAS_SIDE) <= ATLAS_SIDE for order in permutations)}
    result = {'Basis': '实际已发布PNG；35种排列是离线容量抽样，不是游戏验收或任意顺序保证',
              'SingleImageLimit': 512, 'AtlasSide': ATLAS_SIDE,
              'AtlasRgbaBytes': ATLAS_SIDE ** 2 * 4, 'ExtraRgbaBytes': (ATLAS_SIDE ** 2 - 2048 ** 2) * 4,
              'Levels': totals,
              'Families': {family: {'Images': len(paths),
                                    'Pixels': sum(rows[path]['Width'] * rows[path]['Height'] for path in paths)}
                           for family, paths in families.items()},
              'Files': [rows[path] for path in sorted(rows)], 'GameTest': 'pending_user_manual'}
    destination = ROOT / 'Art/Worldfall/biome-leaf-cards-v1/atlas-check.json'
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(json.dumps(result, ensure_ascii=False, indent=2) + '\n', encoding='utf-8')
    print(json.dumps({'families': len(families), 'levels': {
        level: {**{key: value for key, value in row.items() if key != 'ShelfHeights'},
                'MaxSampledShelfHeight': max(row['ShelfHeights'])}
        for level, row in totals.items()}}, ensure_ascii=False))
    return result


if __name__ == '__main__':
    check()
