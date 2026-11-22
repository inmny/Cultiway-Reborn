#!/usr/bin/env python3
"""在独立游戏中复验命魂等实际施法来源。"""
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
    parser.add_argument('--section', choices=('soul', 'soul-force', 'node', 'avatar'), default='soul')
    section = parser.parse_args().section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')
    output = Path(tempfile.mkdtemp(prefix=f'independent-casting-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = world_helpers(output, PROJECT / FIXTURE)
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    script = output / 'input.script'
    scene = Path(__file__).with_name('independent_casting_scene.script').read_text()
    body = 'verify_independent_casting.script' if section == 'soul' else 'verify_independent_sources.script'
    script.write_text(IMPORTS + 'using Cultiway.Core.SkillLibV3.Utils;\n' + helpers
                      + f'const string section = "{section}";\n' + scene + Path(__file__).with_name(body).read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000' if section == 'avatar' else '90000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'独立施法复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'独立施法未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
