#!/usr/bin/env python3
"""实机核对金钟护界的主动入口、原钟体跟随和控制结束。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--views-only', action='store_true', help='只复验启动和实机近景')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix='golden-bell-activation-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    program = folder / 'input.script'
    program.write_text(IMPORTS + world_helpers(folder, PROJECT / FIXTURE)
                       + (SCRIPTS / 'weapon_flight_scene.script').read_text()
                       + (SCRIPTS / 'verify_bell_views.script').read_text()
                       + f'\nconst bool viewsOnly = {str(options.views_only).lower()};\n'
                       + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(program), '170000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'金钟复验输出位于 {folder}') from error
    write_json(folder / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'金钟复验未通过，见 {folder}')
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
