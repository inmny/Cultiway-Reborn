#!/usr/bin/env python3
"""核对同一名化神修士实际使用精细小术。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import write_json
from verify_weapon_flight_archive import world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True, help='11.03 已通过的城市用术存档准备目录')
    parser.add_argument('--case', nargs='+', choices=('combat', 'world'), default=('combat', 'world'))
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    archive = args.archive.resolve()
    prepared = json.loads((archive / 'result.json').read_text())
    if not prepared['passed']:
        parser.error('城市档的准备段未通过')

    folder = Path(tempfile.mkdtemp(prefix='small-art-control-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = folder / 'input-world'
    shutil.copytree(archive / 'archive', world)
    helpers = world_helpers(folder, world, restore=True)
    start = helpers.index('for (int x = 60; x <= 75; x++)')
    helpers = helpers[:start] + helpers[helpers.index('var holdStats', start):]
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\nusing Cultiway.Core.Components;\n' + helpers
                      + f'\nlong sourceId = {prepared["actor"]};\nbool testCombat = {str("combat" in args.case).lower()};\n'
                      + f'bool testWorld = {str("world" in args.case).lower()};\n'
                      + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'精细小术复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'精细小术未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
