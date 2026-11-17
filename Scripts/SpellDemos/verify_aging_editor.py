#!/usr/bin/env python3
"""5.13 C1原窗口侵蚀、老化、独立需求和实际自主使用。"""
import argparse
import json
import os
from pathlib import Path
import shutil
from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('ui', 'ui-save', 'ui-world', 'pricing', 'runtime', 'ai', 'all'), default='all')
    parser.add_argument('--ui', type=Path, help='已通过的窗口输出目录')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for section in ('ui', 'pricing', 'runtime', 'ai') if options.section == 'all' else (options.section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'aging-editor' / section
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        if section != 'ui':
            source = options.ui or folder.parent / 'ui'
            if section != 'ui-save' and not json.loads((source / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已通过的窗口成品')
            for name in ('contact-definition.json', 'saved-definition.json'):
                shutil.copy2(source / name, folder / name)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "aging-editor/' + section + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        body = Path(__file__).with_name('verify_aging_editor_' + section + '.script').read_text()
        if section in ('ui', 'ui-save', 'ui-world'):
            harness = Path(__file__).with_name('verify_world_contact_creation_ui.script').read_text()
            harness = harness[harness.index('LocalizedTextManager.instance.setLanguage'):harness.index('void SaveCase')]
            body = Path(__file__).with_name('aging_editor_case.script').read_text() + ('\ndefinition = Read("saved");\n' if section in ('ui-save', 'ui-world') else '') + '\nConstructedAbilityService.Register(definition); ConstructedAbilityService.Learn(owner, definition.Id);\n' + harness + '\n' + body
        else:
            body = Path(__file__).with_name('aging_editor_case.script').read_text() + '\n' + body
        script = folder / 'input.script'
        script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\nusing Cultiway.Const;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('侵蚀老化窗口复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
