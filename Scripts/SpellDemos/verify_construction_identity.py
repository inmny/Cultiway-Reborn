#!/usr/bin/env python3
"""复验正式构造的编号、修订、出处和学习关系。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import write_json
from verify_weapon_flight_archive import world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True, help='11.23 筑路阶段的正式存档目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')
    import baseline_scenarios as baseline

    source = args.archive.resolve()
    original = json.loads((source / 'result.json').read_text())
    if not original.get('passed'):
        parser.error('所选原世界应已完成筑路阶段')
    out = Path(tempfile.mkdtemp(prefix='construction-identity-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    copy = out / 'input-world'
    shutil.copytree(source / 'archive', copy)
    helpers = world_helpers(out, copy, restore=True)
    start = helpers.index('for (int x = 60; x <= 75; x++)')
    helpers = helpers[:start] + helpers[helpers.index('var holdStats', start):]
    helpers = helpers[:helpers.index('var holdStats')]
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    imports = 'using Friflo.Engine.ECS;\nusing Cultiway.Core.SkillLibV3;\n'
    script = out / 'input.script'
    script.write_text(imports + helpers + f'long ownerId = {original["actor"]};\n'
                      + Path(__file__).with_suffix('.script').read_text())
    write_json(out / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '115000')
    except RuntimeError as error:
        write_json(out / 'failure.json', {'passed': False, 'error': str(error)})
        raise
    write_json(out / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'构造编号验证未通过，见 {out}')
    print(out)


if __name__ == '__main__':
    main()
