#!/usr/bin/env python3
"""5.13真实接触老化、装备腐蚀及原版结果的正式存读。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('core', 'runtime', 'save', 'all'), default='all')
    parser.add_argument('--state', type=Path, help='save读取已通过的runtime目录')
    parser.add_argument('--family', choices=('actor', 'equipment'), default='actor')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    prefix = 'aging' if options.family == 'actor' else 'equipment-erosion'
    script_prefix = 'verify_aging_' if options.family == 'actor' else 'verify_equipment_erosion_'
    for section in ('core', 'runtime', 'save') if options.section == 'all' else (options.section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / prefix / section
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        if section == 'save':
            source = options.state or folder.parent / 'runtime'
            if not json.loads((source / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已通过的生灵老化实机结果')
            archives = ('aged-save',) if options.family == 'actor' else ('worn-save', 'broken-save')
            for name in archives:
                shutil.copytree(source / name, folder / name, dirs_exist_ok=True)
            shutil.copy2(source / 'expected.json', folder / 'expected.json')
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "' + prefix + '/' + section + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        shared = Path(__file__).with_name('aging_case.script').read_text()
        if options.family == 'equipment':
            shared += '\n' + Path(__file__).with_name('equipment_erosion_case.script').read_text()
        body = Path(__file__).with_name(script_prefix + section + '.script').read_text()
        script = folder / 'input.script'; script.write_text(helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('生灵老化复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
