#!/usr/bin/env python3
"""核对真实法钟的扣血、眩晕、震退、净化及两个视角的圆面。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('both', 'shake', 'cleanse', 'views'), default='both')
    parser.add_argument('--skip-views', action='store_true', help='只复验行为与共同范围，不切换显示视角')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'artifact-chime-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    inputs = f'\nconst string section = "{args.section}";\nconst bool shots = {str(not args.skip_views).lower()};\n'
    script = folder / 'input.script'
    script.write_text(IMPORTS + world_helpers(folder, PROJECT / FIXTURE)
                      + (SCRIPTS / 'weapon_flight_scene.script').read_text()
                      + (SCRIPTS / 'verify_bell_views.script').read_text() + inputs
                      + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(script), '170000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'法钟复验输出位于 {folder}') from error
    write_json(folder / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'法钟复验未通过，见 {folder}')
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
