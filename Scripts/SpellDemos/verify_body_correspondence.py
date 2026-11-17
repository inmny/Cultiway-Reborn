#!/usr/bin/env python3
"""5.14 B1真实身体及借用兵器对映像的接触、频率及控制归还。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('normal', 'frequency', 'control', 'all'), default='all')
    parser.add_argument('--frequency-case', choices=('timed', 'reentry', 'all'), default='all',
                        help='只补间隔及脉冲，或实际离开再进入')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    sections = ('normal', 'frequency', 'control') if options.section == 'all' else (options.section,)
    for section in sections:
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'body-correspondence' / section
        if section == 'frequency' and options.frequency_case != 'all':
            folder = folder.with_name('frequency-' + options.frequency_case)
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "body-correspondence/' + folder.name + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        helpers = helpers.replace('    ControllableUnit.clear(false);\n    ControllableUnit.setControllableCreature(source);',
                                  '    if (!ControllableUnit.isControllingUnit(source))\n    {\n        ControllableUnit.clear(false);\n        ControllableUnit.setControllableCreature(source);\n    }')
        shared = Path(__file__).with_name('correspondence_case.script').read_text()
        shared += '\n' + Path(__file__).with_name('body_correspondence_case.script').read_text()
        body = Path(__file__).with_name('verify_body_correspondence_' + section + '.script').read_text()
        if section == 'frequency':
            body = 'var frequencyCase = ' + json.dumps(options.frequency_case) + ';\n' + body
        script = folder / 'input.script'
        script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\nusing Cultiway.Const;\n' + helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('身体映像接触复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
