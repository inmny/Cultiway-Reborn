#!/usr/bin/env python3
"""复验一次性法术物品的实际受理、目标失效和拒绝恢复。"""
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
    parser.add_argument('--section', required=True, choices=('scroll', 'rejection'))
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'consumable-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    program = output / 'input.script'
    program.write_text(IMPORTS + 'using Cultiway.Utils;\nusing Cultiway.Core.SkillLibV3.Utils;\n'
                       + 'using Cultiway.Core.SkillLibV3.Modifiers;\nusing Cultiway.Content.Components.Skill;\n'
                       + world_helpers(output, PROJECT / FIXTURE)
                       + f'const string section = "{args.section}";\n'
                       + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(program), '90000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'一次性法术物品复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'一次性法术物品复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
