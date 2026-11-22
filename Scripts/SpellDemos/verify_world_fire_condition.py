#!/usr/bin/env python3
"""实测原版点火对区域和全图防火条件的查询及恢复。"""
import argparse
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'source'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    folder = Path(tempfile.mkdtemp(prefix=f'world-fire-condition-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    definitions = PROJECT / 'Doc/技能系统全面重构/构造输入'
    for name, target in [('镇火安居', 'region'), ('息焰敕令', 'world')]:
        shutil.copy2(definitions / f'{name}.json', folder / f'{target}.json')
    script = folder / 'input.script'
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
                      + world_helpers(folder, PROJECT / FIXTURE)
                      + f'\nbool checkRegion = {str(args.section == "all").lower()};\n'
                      + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'防火条件复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'防火条件复验未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
