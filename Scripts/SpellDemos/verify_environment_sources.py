#!/usr/bin/env python3
"""复验原版启灵雨落地和共用的实际地表变化。"""
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中使用 dev.py exec')
    output = Path(tempfile.mkdtemp(prefix='environment-sources-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    helpers = world_helpers(output, PROJECT / FIXTURE).replace('test.body-footwork.', 'test.environment-sources.')
    script.write_text(IMPORTS + helpers + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '100000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'环境来源实测见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'环境来源未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
