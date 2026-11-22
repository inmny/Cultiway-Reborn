#!/usr/bin/env python3
"""复验万法阁功能术试放及回到原草稿。"""
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    output = Path(tempfile.mkdtemp(prefix='utility-preview-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Core.SkillLibV3;\n'
               'using Cultiway.Core.SkillLibV3.Components;\nusing Cultiway.Core.SkillLibV3.Utils;\n'
               'using Cultiway.Core.SkillLibV3.Blueprints;\nusing Cultiway.Core.SkillLibV3.Wanfa;\n'
               'using Cultiway.Content.Components.Skill;\nusing Cultiway.UI;\nusing UnityEngine.UI;\n')
    script.write_text(imports + world_helpers(output, PROJECT / FIXTURE) +
                      Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '115000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'功能术试放验证失败，见 {output}') from None
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'功能术试放验证未通过，见 {output}')
    print(output)


if __name__ == '__main__':
    main()
