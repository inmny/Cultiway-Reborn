#!/usr/bin/env python3
"""5.13 A2a限定复验，世界材料的持续侵蚀、交接及正式保存。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json


def replace_section(folder, section):
    path = folder / 'map.wbox'
    world = json.loads(zlib.decompress(path.read_bytes()))
    strings = world['mapStats'].setdefault('custom_data', {}).setdefault('custom_data_string', {})
    document = json.loads(strings.get('cultiway.world', '{"formatVersion":1,"sections":{}}'))
    document['sections']['world_materials'] = section
    strings['cultiway.world'] = json.dumps(document, ensure_ascii=False, separators=(',', ':'))
    path.write_bytes(zlib.compress(json.dumps(world, ensure_ascii=False, separators=(',', ':')).encode()))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('material', 'world', 'save', 'all'), default='all')
    parser.add_argument('--state', type=Path, help='save读取已完成的world输出')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for section in ('material', 'world', 'save') if options.section == 'all' else (options.section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'world-erosion' / section
        folder.mkdir(parents=True, exist_ok=True)
        fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
        shutil.copytree(fixture, folder / 'input-world', dirs_exist_ok=True)
        if section == 'save':
            source = options.state or folder.parent / 'world'
            if not json.loads((source / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已完成的独立侵蚀实机结果')
            shutil.copytree(source / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
            shutil.copy2(source / 'expected.json', folder / 'expected.json')
            shutil.copytree(fixture, folder / 'legacy-world', dirs_exist_ok=True)
            legacy = json.loads(Path(__file__).with_name('fixtures').joinpath('erosion-world-v6.json').read_text())
            replace_section(folder / 'legacy-world', legacy['section'])
            write_json(folder / 'legacy.json', legacy)
            shutil.copytree(source / 'pending-save', folder / 'bad-world', dirs_exist_ok=True)
            expected = json.loads((source / 'expected.json').read_text())
            broken = json.loads(json.dumps(expected['section']))
            next(row for row in broken['data']['Records'] if row['Id'] == expected['active'])['Material']['Erosion']['Progress'] = 2
            replace_section(folder / 'bad-world', broken)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "world-erosion/' + section + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        shared = Path(__file__).with_name('erosion_case.script').read_text()
        body = Path(__file__).with_name('verify_world_erosion_' + section + '.script').read_text()
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Persistence;\n' + helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('世界材料侵蚀复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
