#!/usr/bin/env python3
"""实测世界防火条件正式保存、真正重启及原来源失效。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile
import zlib

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    """从现成原版世界准备正式档，另一进程读取同一档。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('prepare', 'cold'), required=True)
    parser.add_argument('--archive', type=Path, help='准备阶段的输出目录')
    parser.add_argument('--film', action='store_true', help='录制正式冷读后两份条件先后到期的原版画面')
    args = parser.parse_args()
    if (args.section == 'cold') != bool(args.archive):
        parser.error('只在冷读阶段提供 --archive')
    if args.film and args.section != 'cold':
        parser.error('录像使用已通过的正式世界条件档')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix=f'world-condition-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    source = args.archive.resolve() if args.archive else folder
    world = source / 'archive' if args.archive else PROJECT / FIXTURE
    if args.film:
        world = folder / 'input-world'
        shutil.copytree(source / 'archive', world)
    helpers = world_helpers(folder, world, restore=True)
    start = helpers.index('for (int x = 60; x <= 75; x++)')
    helpers = helpers[:start] + helpers[helpers.index('var holdStats', start):]
    if not args.archive:
        definitions = PROJECT / 'Doc/技能系统全面重构/构造输入'
        shutil.copy2(definitions / '镇火安居.json', folder / 'region.json')
        shutil.copy2(definitions / '息焰敕令.json', folder / 'world.json')
    script = folder / 'input.script'
    camera = Path(__file__).with_name('record_independent_casting_camera.script').read_text()
    camera = camera.replace('    await Advance(.15f);\n', '')
    recording = Path(__file__).with_name('record_archive_stage.script').read_text()
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers
                      + f'\nstring sourceFolder = {json.dumps(str(source))};\n'
                      + f'bool restoring = {str(bool(args.archive)).lower()};\n'
                      + f'const bool recordArchive = {str(args.film).lower()};\n' + camera + recording
                      + Path(__file__).with_suffix('.script').read_text())
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '160000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'长期世界条件未通过，见 {folder}')
    if args.film:
        if (world / 'map.wbox').read_bytes() != (source / 'archive/map.wbox').read_bytes():
            raise RuntimeError('录像输入副本发生改变')
        from record_body_connections import encode
        encode('world-condition', folder, folder, True, label='防火条件读回与到期', milestone='12.15 实机存读')
    if not args.archive:
        native = json.loads(zlib.decompress((source / 'archive/map.wbox').read_bytes()))
        custom = native['mapStats']['custom_data']['custom_data_string']
        section = json.loads(custom['cultiway.world'])['sections']['constructed_world_conditions']
        rows = section['data']['Records']
        if section['version'] != 1 or len(rows) != 2 or sum(row['EntireWorld'] for row in rows) != 1:
            raise RuntimeError(f'正式地图的世界条件范围不符，见 {folder}')
        write_json(folder / 'formal-section.json', section)
    print(folder)


if __name__ == '__main__':
    main()
