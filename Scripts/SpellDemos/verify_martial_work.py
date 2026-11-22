#!/usr/bin/env python3
"""复验原城工人用实际铁锤筑路、通行和正式读档。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import write_json
from verify_weapon_flight_archive import world_helpers


def main():
    """复用功能术城市原档，或读取已完成的筑路工作档。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True, help='11.03 或筑路工作阶段准备目录')
    parser.add_argument('--section', choices=('work', 'cold'), default='work')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')
    import baseline_scenarios as baseline

    original = args.archive.resolve()
    data = json.loads((original / 'result.json').read_text())
    if not data.get('passed'):
        parser.error('待使用的地图准备阶段未通过')
    output = Path(tempfile.mkdtemp(prefix=f'martial-opening-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = output / 'input-world'
    shutil.copytree(original / 'archive', world)
    helpers = world_helpers(output, world, restore=True)
    start = helpers.index('for (int x = 60; x <= 75; x++)')
    helpers = helpers[:start] + helpers[helpers.index('var holdStats', start):]
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Core.CollectiveProjects;\n'
               'using Cultiway.Core.Semantics;\nusing Cultiway.Core.SkillLibV3;\n')
    reference = Path(__file__).with_name('verify_body_footwork.script').read_text()
    motions = 'AbilityDefinition Motion(' + reference.split('AbilityDefinition Motion(', 1)[1].split('var movementType =', 1)[0]
    script = output / 'input.script'
    script.write_text(imports + helpers + '\n' + motions
                      + f'\nstring section = "{args.section}";\nlong sourceId = {data["actor"]};\n'
                      + f'string original = {json.dumps(str(original))};\n'
                      + Path(__file__).with_suffix('.script').read_text())
    write_json(output / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '195000')
    except RuntimeError as error:
        write_json(output / 'failure.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'筑路工作未通过，见 {output}')
    print(output)


if __name__ == '__main__':
    main()
