#!/usr/bin/env python3
"""复验器官的实际出手、减伤和保命。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


SURVIVAL_SECTIONS = ('shell', 'phoenix', 'tails')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', default='source',
                        choices=('source', 'horn', 'mouth', 'torso', 'poison', *SURVIVAL_SECTIONS))
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')
    output = Path(tempfile.mkdtemp(prefix=f'organ-ability-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    name = 'verify_organ_outlets.script'
    if args.section == 'source':
        name = 'verify_organ_ability.script'
    elif args.section in SURVIVAL_SECTIONS:
        name = 'verify_organ_survival.script'
    body = Path(__file__).with_name(name)
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
        raise RuntimeError(f'器官能力复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'器官能力复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
