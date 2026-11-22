#!/usr/bin/env python3
"""用已通过正式存读的功能术复验城市工程。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import write_json
from verify_weapon_flight_archive import world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', required=True, type=Path, help='功能术正式存读准备阶段目录')
    parser.add_argument('--section', choices=('eligibility', 'work', 'all'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    archive = args.archive.resolve()
    world = archive / 'archive'
    with (world / 'map.wbox').open('rb') as save:
        digest = hashlib.file_digest(save, 'sha256').hexdigest()
    if digest != (archive / 'archive.sha256').read_text().strip():
        parser.error('正式功能术存档校验未通过')
    source = json.loads((archive / 'result.json').read_text())['actor']
    output = Path(tempfile.mkdtemp(prefix=f'utility-city-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    input_world = output / 'input-world'
    shutil.copytree(world, input_world)
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Core.CollectiveProjects;\n'
               'using Cultiway.Core.Semantics;\nusing Cultiway.Core.SkillLibV3;\n'
               'using Cultiway.Core.SkillLibV3.Components;\nusing Cultiway.Core.SkillLibV3.Utils;\n'
               'using Cultiway.Core.SkillLibV3.Blueprints;\nusing Cultiway.Content.Components.Skill;\n')
    script = output / 'input.script'
    script.write_text(imports + world_helpers(output, input_world, restore=True) +
                      f'\nstring section = "{args.section}";\nlong sourceId = {source};\n' +
                      Path(__file__).with_suffix('.script').read_text())

    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '195000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'功能术城市复验失败，见 {output}') from None
    result['inputSha256'] = digest
    if result.get('passed') and result.get('archive'):
        with (Path(result['archive']) / 'map.wbox').open('rb') as save:
            (output / 'work-save.sha256').write_text(hashlib.file_digest(save, 'sha256').hexdigest() + '\n')
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'功能术城市复验未通过，见 {output}')
    print(output)


if __name__ == '__main__':
    main()
