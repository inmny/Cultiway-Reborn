#!/usr/bin/env python3
"""用原版实际地形更新测量共同提交开销。"""
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    folder = Path(tempfile.mkdtemp(prefix='world-commit-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = folder / 'input.script'
    script.write_text('using HarmonyLib;\nusing System.Diagnostics;\n'
                      + world_helpers(folder, PROJECT / FIXTURE)
                      + '\n' + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'世界提交实测失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'世界提交实测未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
