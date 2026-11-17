#!/usr/bin/env python3
"""5.11 B2库存窗口及创作复验，复用develop独立环境和现成世界。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'ui', 'creation', 'independent', 'runtime', 'legacy', 'save'), default='all')
    parser.add_argument('--ui', type=Path, help='runtime读取已通过的ui成品，缺省使用本进程结果')
    parser.add_argument('--creation', type=Path, help='runtime和save读取已通过的creation输出，缺省使用本进程结果')
    options = parser.parse_args()
    section = options.section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for current in ('ui', 'creation', 'independent', 'runtime', 'legacy', 'save') if section == 'all' else (section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'inventory-creation' / current
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        shutil.copy2(Path(__file__).parent / 'fixtures/inventory-creation-rule25.json', folder / 'rule25.json')
        if current == 'save':
            creation = options.creation or folder.parent / 'creation'
            if not json.loads((creation / 'result.json').read_text()).get('passed'):
                raise RuntimeError('正式保存需要已通过的创作输入')
            shutil.copytree(creation, folder / 'input-creation', dirs_exist_ok=True)
            shutil.copy2(creation / 'basis.json', folder / 'basis.json')
            shutil.copy2(creation / 'derived.json', folder / 'selected.json')
        if current == 'runtime':
            ui = options.ui or folder.parent / 'ui'
            if not json.loads((ui / 'result.json').read_text()).get('passed'):
                raise RuntimeError('实际使用需要已通过的窗口成品')
            shutil.copy2(ui / 'saved-definition.json', folder / 'saved-definition.json')
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "inventory-creation/' + current + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        script = folder / 'input.script'
        shared = Path(__file__).with_name('inventory_case.script').read_text()
        common = Path(__file__).with_name('ending_case.script').read_text()
        shared += '\n' + common[common.index('IEnumerable<ConstructionNode> Nodes('):]
        body = Path(__file__).with_name('verify_inventory_creation_' + current + '.script').read_text()
        if current == 'ui':
            ui = Path(__file__).with_name('verify_progressive_formation_ui.script').read_text().split('try\n{', 1)[0]
            ui = ui.replace('test.progressive-formation.editor', 'test.inventory-creation.editor').replace('逐段成形编辑', '库存材料编辑')
            ui = ui.replace('ConstructedAbilityService.Register(definition);', 'definition.Creator = source.id;\nConstructedAbilityService.Register(definition);', 1)
            body = ui + body
        script.write_text('using Cultiway.Core.Components;\nusing Cultiway.Utils;\nusing Friflo.Engine.ECS;\nusing Cultiway.Core.Persistence;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('库存创作复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
