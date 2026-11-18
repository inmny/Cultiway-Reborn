#!/usr/bin/env python3
"""6.02C：正式发布的复合区域、共用作用间隔、存档与两端显示。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('full', 'reentry', 'interval', 'save', 'restore', 'original'), default='full')
    parser.add_argument('--input-save', type=Path, help='复用原版运行保存的同一个世界')
    parser.add_argument('--worldfall', action='store_true', help='同时核对实际三维绘制提交')
    args = parser.parse_args()
    if bool(args.input_save) != (args.section == 'restore') or args.worldfall and args.section != 'restore':
        parser.error('还原阶段需要 --input-save；Worldfall 复用原版存档')
    relative = f'sustained-contact-regions/{args.section}'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    if args.input_save:
        saved = args.input_save.resolve()
        shutil.copytree(saved, folder / 'input-world', dirs_exist_ok=True)
        shutil.copy2(saved.parent / 'saved-expected.json', folder / 'saved-expected.json')
    else:
        shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), {json.dumps(relative)})')
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.sustained-region.')
    if args.section == 'original':
        helpers = helpers.replace('fact.AbilityId.StartsWith("test.sustained-region.")', 'fact.AbilityId == SkillEntities.SwordQi.id')
        helpers = helpers.replace('contact.AbilityId.StartsWith("test.sustained-region.")', 'contact.AbilityId == SkillEntities.SwordQi.id')
    if args.input_save:
        # 正式读回测试不重新布置地形，保留存档中的同一处承托面。
        first = helpers.index('for (int x = 60; x <= 75; x++)')
        last = helpers.index('var holdStats =', first)
        helpers = helpers[:first] + helpers[last:]
    script = folder / 'input.script'
    imports = 'using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Utils;\nusing Friflo.Engine.ECS;\n'
    script.write_text(imports + helpers + f'\nstring section = "{args.section}";\nbool worldfall = {str(args.worldfall).lower()};\n' +
                      (scripts / 'verify_sustained_contact_regions.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('持续复合区域未通过：' + str(folder))
    print('持续复合区域：' + str(folder))


if __name__ == '__main__':
    main()
