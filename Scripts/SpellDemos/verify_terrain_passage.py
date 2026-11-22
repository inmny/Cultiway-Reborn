#!/usr/bin/env python3
"""复用原步法与地形操作，实测同一水隙的阻挡和恢复。"""
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix='terrain-passage-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers_file = Path(__file__).with_name('verify_body_footwork.script')
    _, _, motions = helpers_file.read_text().partition('AbilityDefinition Motion(')
    motions = 'AbilityDefinition Motion(' + motions.split('var movementType =', 1)[0]
    script = folder / 'input.script'
    script.write_text(world_helpers(folder, PROJECT / FIXTURE, restore=True) + '\n' + motions
                      + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '195000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'地形通行复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'地形通行复验未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
