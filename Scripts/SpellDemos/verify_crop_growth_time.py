#!/usr/bin/env python3
"""核对原版麦田自身的生长时间及正式读回。"""
import argparse
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True, help='11.03 已通过的城市用术存档准备目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    folder = Path(tempfile.mkdtemp(prefix='crop-growth-time-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/借时育禾.json', folder / 'definition.json')
    world = folder / 'input-world'
    shutil.copytree(args.archive.resolve() / 'archive', world)
    script = folder / 'input.script'
    script.write_text('using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n'
                      + world_helpers(folder, world, restore=True)
                      + '\n' + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '150000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'麦田节奏复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'麦田节奏复验未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
