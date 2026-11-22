#!/usr/bin/env python3
"""复验原功能法术的落地状态与持续地块。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('landing', 'field', 'damage', 'motion', 'carrier', 'combo', 'affixes', 'branches', 'all'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    output = Path(tempfile.mkdtemp(prefix=f'utility-status-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\nusing Cultiway.Core.SkillLibV3;\n'
               'using Cultiway.Core.SkillLibV3.Blueprints;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Content.Components.Skill;\n')
    script.write_text(imports + world_helpers(output, PROJECT / FIXTURE) +
                      f'\nstring section = {json.dumps(args.section)};\n' + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '115000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'功能法术状态复验失败，见 {output}') from None
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'功能法术状态复验未通过，见 {output}')
    print(output)


if __name__ == '__main__':
    main()
