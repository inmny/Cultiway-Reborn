#!/usr/bin/env python3
"""5.13 A2b2曲面局部侵蚀、共同表面与正式存读。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json
from verify_world_erosion import replace_section


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('core', 'world', 'save', 'all'), default='all')
    parser.add_argument('--state', type=Path, help='save读取已通过的world结果')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for section in ('core', 'world', 'save') if options.section == 'all' else (options.section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'partial-erosion' / section
        folder.mkdir(parents=True, exist_ok=True)
        fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
        shutil.copytree(fixture, folder / 'input-world', dirs_exist_ok=True)
        if section == 'save':
            source = options.state or folder.parent / 'world'
            if not json.loads((source / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已完成的局部侵蚀实机结果')
            shutil.copytree(source / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
            shutil.copy2(source / 'expected.json', folder / 'expected.json')
            legacy = json.loads(Path(__file__).parent.joinpath('fixtures/erosion-world-v7.json').read_text())
            shutil.copytree(fixture, folder / 'legacy-world', dirs_exist_ok=True)
            replace_section(folder / 'legacy-world', legacy['section'])
            write_json(folder / 'legacy.json', legacy)
            shutil.copytree(source / 'pending-save', folder / 'bad-world', dirs_exist_ok=True)
            expected = json.loads((source / 'expected.json').read_text())
            broken = json.loads(json.dumps(expected['section']))
            next(row for row in broken['data']['Records'] if row['Id'] == expected['active'])['Material']['Erosion']['Id'] = broken['data']['Next']
            replace_section(folder / 'bad-world', broken)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "partial-erosion/' + section + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        shared = Path(__file__).with_name('erosion_case.script').read_text() + '\n' + Path(__file__).with_name('world_erosion_request_case.script').read_text()
        body = Path(__file__).with_name('verify_partial_erosion_' + section + '.script').read_text()
        script = folder / 'input.script'; script.write_text('using Cultiway.Core.Persistence;\n' + helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('曲面局部侵蚀复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
