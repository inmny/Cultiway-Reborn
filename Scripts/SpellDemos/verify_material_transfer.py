#!/usr/bin/env python3
"""5.11 C1生成材料交给原版永久结果，复用真实窗口、接触和世界保存。"""
import argparse
import json
import os
from pathlib import Path
import shutil
from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('ui', 'runtime', 'save', 'precision', 'all'), default='all')
    parser.add_argument('--ui', type=Path)
    parser.add_argument('--state', type=Path)
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for section in ('ui', 'runtime', 'save', 'precision') if options.section == 'all' else (options.section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'material-transfer' / section
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        if section in ('runtime', 'precision'):
            source = options.ui or folder.parent / 'ui'
            if not json.loads((source / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已通过的窗口成品')
            shutil.copy2(source / 'saved-definition.json', folder / 'saved-definition.json')
        if section == 'save':
            source = options.state or folder.parent / 'runtime'
            if not json.loads((source / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已通过的实际交出与正式保存')
            shutil.copytree(source / 'successful-save', folder / 'successful-save', dirs_exist_ok=True)
            shutil.copy2(source / 'saved-expected.json', folder / 'saved-expected.json')
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "material-transfer/' + section + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        body = Path(__file__).with_name('verify_material_transfer_' + section + '.script').read_text()
        if section == 'ui':
            ui = Path(__file__).with_name('verify_world_contact_creation_ui.script').read_text().split('try\n{', 1)[0]
            ui = ui.replace('test.world-contact-creation.editor', 'test.material-transfer.editor').replace('地块接触编辑', '生成材料交出')
            ui = ui.replace('Lifetime = .8f', 'Lifetime = 1.2f').replace('Power = 0', 'Power = 8')
            ui = ui.replace('Id = "surface", Form = "sword"', 'Id = "surface", Volume = 8, Form = "sword"')
            ui = ui.replace('Ground = ConstructionGroundResponse.Follow }', 'Ground = ConstructionGroundResponse.Follow }, new ConstructionOperation { Kind = ConstructionOperationKind.Leave, At = .6f, Residue = new ConstructionResidue() }')
            body = ui + body
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Persistence;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('材料永久结果复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
