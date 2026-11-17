#!/usr/bin/env python3
"""5.13 A2b1完整实体范围侵蚀及真实完成衔接。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('core', 'world', 'all'), default='all')
    parser.add_argument('--part', choices=('all', 'normal', 'boundaries', 'receipts'), default='all', help='world可单独复验正常过程或边界')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for section in ('core', 'world') if options.section == 'all' else (options.section,):
        label = section + ('-' + options.part if section == 'world' and options.part != 'all' else '')
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'world-erosion-requests' / label
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "world-erosion-requests/' + label + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        shared = Path(__file__).with_name('erosion_case.script').read_text() + '\n' + Path(__file__).with_name('world_erosion_request_case.script').read_text()
        body = Path(__file__).with_name('verify_world_erosion_requests_' + section + '.script').read_text()
        script = folder / 'input.script'; script.write_text(helpers + '\nconst string Part = ' + json.dumps(options.part) + ';\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('范围侵蚀复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
