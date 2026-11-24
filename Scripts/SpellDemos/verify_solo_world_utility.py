#!/usr/bin/env python3
"""复用同一位修士和已有城市，核对城市净化及全图防火。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import write_json
from verify_weapon_flight_archive import world_helpers
from world_progress_capture import encode_progress, progress_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True, help='11.03 已通过的城市用术存档准备目录')
    parser.add_argument('--record', action='store_true', help='录制原版常速进度')
    parser.add_argument('--case', choices=('all', 'city', 'world'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    archive = args.archive.resolve()
    prepared = json.loads((archive / 'result.json').read_text())
    if not prepared['passed']:
        parser.error('城市档的准备段未通过')

    folder = Path(tempfile.mkdtemp(prefix='solo-world-utility-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = folder / 'input-world'
    shutil.copytree(archive / 'archive', world)
    helpers = world_helpers(folder, world, restore=True)
    start = helpers.index('for (int x = 60; x <= 75; x++)')
    helpers = helpers[:start] + helpers[helpers.index('var holdStats', start):]
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\nusing Cultiway.Core.Components;\n' + helpers
                      + f'\nlong sourceId = {prepared["actor"]};\nlong readerId = {prepared["reader"]};\n'
                      + f'string utilityCase = {json.dumps(args.case)};\n'
                      + progress_helpers(args.record) + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '240000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'城域和全域功能复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'城域和全域功能未通过，见 {folder}')
    if args.record:
        titles = {'city': '镇域净土', 'world': '息焰敕令'}
        cases = titles if args.case == 'all' else {args.case: titles[args.case]}
        encode_progress(folder, cases, result)
    print(folder)


if __name__ == '__main__':
    main()
