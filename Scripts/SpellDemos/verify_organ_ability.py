#!/usr/bin/env python3
"""用同一只雷角妖兽复验主动来源、出手扣费和撤去器官。"""
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')
    output = Path(tempfile.mkdtemp(prefix='organ-ability-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    script.write_text(IMPORTS + 'using Cultiway.Content.CreatureCompositions.ActiveAbilities;\n'
                      + world_helpers(output, PROJECT / FIXTURE)
                      + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '90000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'器官出手复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'器官出手复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
