#!/usr/bin/env python3
"""复验器官来源、实际身体出口和出手扣费。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', default='source', choices=('source', 'horn', 'mouth', 'torso', 'poison'))
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')
    output = Path(tempfile.mkdtemp(prefix=f'organ-ability-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    body = Path(__file__).with_suffix('.script') if args.section == 'source' else Path(__file__).with_name('verify_organ_outlets.script')
    script.write_text(IMPORTS + 'using Cultiway.Content.CreatureCompositions.ActiveAbilities;\n'
                      + 'using Cultiway.Core.Visuals.Forms;\n'
                      + world_helpers(output, PROJECT / FIXTURE)
                      + ('' if args.section == 'source' else f'const string section = "{args.section}";\n')
                      + body.read_text())
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
