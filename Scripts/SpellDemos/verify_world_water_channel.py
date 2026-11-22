#!/usr/bin/env python3
"""在独立游戏验证实际落差、分流和一次地形变化。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--view', choices=('native', 'worldfall'), default='worldfall')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix='world-water-channel-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = folder / 'input.script'
    source = PROJECT / 'Doc/技能系统全面重构/构造输入/跨区留水.json'
    script.write_text(world_helpers(folder, PROJECT / FIXTURE, restore=True) + '\n'
                      + f'string flowDefinition = {json.dumps(str(source))};\n'
                      + f'string view = "{args.view}";\n'
                      + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '195000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'水路复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'水路复验未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
