#!/usr/bin/env python3
"""把当前基础法术登记、旧索引和原始实机证据逐项对齐。"""
import argparse
import hashlib
import json
from pathlib import Path

from common import write_json
from verify_base_spell_archive import read_base_variants


def read(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


def verify(inventory_path, delivery_path):
    variants = read_base_variants()
    expected = {(asset, index) for asset, indices in variants.items() for index in indices}
    inventory = read(inventory_path)
    entries = {entry['id']: entry for entry in inventory['entries'] if entry['kind'] == '技能资产'}
    for asset, indices in variants.items():
        entry = entries.get(asset)
        if not entry or not entry['learnable'] or entry['variants'] != len(indices):
            raise ValueError(f'当前游戏不能按原索引提供 {asset}')

    delivery = read(delivery_path)
    archive = delivery['archive']
    saved, restored = read(archive['created']), read(archive['coldRead'])
    saved_variants = {(row['id'], row['index']) for row in restored['assets']}
    if (not saved['passed'] or not restored['passed'] or saved['process'] == restored['process'] or
            saved['skills'] != len(expected) or restored['skills'] != len(expected) or
            len(restored['assets']) != len(expected) or saved_variants != expected):
        raise ValueError(f'跨进程读回的索引与清单不同，缺失 {sorted(expected - saved_variants)}，多出 {sorted(saved_variants - expected)}')
    digest = hashlib.sha256(Path(archive['world']).read_bytes()).hexdigest()
    if digest != archive['sha256']:
        raise ValueError(f"实际档案与交付时的存档不同：{archive['world']}")

    rows = {(asset, index): {'asset': asset, 'index': index, 'views': {}}
            for asset, index in sorted(expected)}
    recorded = read(delivery['index'])
    for view in ('native', 'worldfall'):
        for clip in recorded['views'][view]['clips']:
            key = (clip['asset'], clip['variant'])
            if key not in rows or view in rows[key]['views']:
                raise ValueError(f'{view} 实录出现重复或多余的旧索引：{key}')
            result = read(clip['report'])
            if (not result['passed'] or result['errors'] or (result['id'], result['item']['variant']) != key or
                    not any(log['id'] == key[0] and log.get('framework') == 'construction'
                            for log in result['executionLog'])):
                raise ValueError(f"{view} 缺少该索引的共同执行记录：{clip['report']}")
            if not Path(clip['delivery']).is_file():
                raise ValueError(f"缺少该类实际作用的实机索引：{clip['delivery']}")
            rows[key]['views'][view] = {field: clip[field] for field in ('report', 'delivery', 'start', 'seconds')}
        missing = [key for key, row in rows.items() if view not in row['views']]
        if missing:
            raise ValueError(f'{view} 缺少这些旧索引的原记录：{missing}')

    return {'passed': True, 'inventory': str(inventory_path), 'version': inventory['version'],
            'originalDelivery': str(delivery_path), 'archive': archive, 'entries': list(rows.values())}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--inventory', type=Path, required=True, help='当前游戏导出的 inventory.json')
    parser.add_argument('--delivery', type=Path, required=True, help='10.01 实机录像及存档交付的 delivery.json')
    parser.add_argument('--output', type=Path, required=True)
    args = parser.parse_args()
    result = verify(args.inventory.resolve(), args.delivery.resolve())
    args.output.parent.mkdir(parents=True, exist_ok=True)
    write_json(args.output, result)
    print(f'基础法术的当前登记、旧索引、共同执行及正式档已对齐：{args.output}')


if __name__ == '__main__':
    main()
