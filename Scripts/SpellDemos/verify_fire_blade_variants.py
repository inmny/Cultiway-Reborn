#!/usr/bin/env python3
"""沿原已学入口实测火刃的四份旧索引。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--variant', type=int, choices=range(4), action='append', help='只复验指定旧索引，可重复指定')
    args = parser.parse_args()
    if not os.getenv('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    folder = Path(tempfile.mkdtemp(prefix='fire-blade-variants-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('StartsWith("test.body-footwork.")', 'Equals("Cultiway.FireBlade")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    imports = ('using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Blueprints;\nusing Friflo.Engine.ECS;\n')
    body = Path(__file__).with_suffix('.script').read_text()
    variants = ', '.join(map(str, args.variant or range(4)))
    script = folder / 'input.script'
    script.write_text(imports + helpers + f'int[] chosenVariants = {{ {variants} }};\n' + body)
    try:
        result = baseline.call('run', str(script), '150000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'火刃实测见 {folder}') from error
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'火刃实测未通过，见 {folder}')
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
