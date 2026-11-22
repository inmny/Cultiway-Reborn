#!/usr/bin/env python3
"""从人物自然交战复验共同推进、实际扣费和伤害结算。"""
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立原版游戏 dev.py exec 运行')

    import baseline_scenarios as baseline

    output = Path(tempfile.mkdtemp(prefix='natural-cast-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    helpers = world_helpers(output, PROJECT / FIXTURE)
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    script.write_text('using Cultiway.Core.SkillLibV3;\n' + helpers + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '75000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'自然出手复验失败，见 {output}') from None
    write_json(output / 'result.json', result)
    print(output)


if __name__ == '__main__':
    main()
