#!/usr/bin/env python3
"""5.14 A生成部位与原人物的真实对应、有限传伤及正式保存。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('core', 'runtime', 'effects', 'save', 'all'), default='all')
    parser.add_argument('--state', type=Path, help='save复用已通过的runtime或effects目录')
    parser.add_argument('--legacy', type=Path, help='真实旧12或旧13的正式完成世界')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    sections = ('core', 'runtime', 'effects', 'save') if options.section == 'all' else (options.section,)
    for section in sections:
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'correspondence' / section
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        if section == 'save':
            source = options.state or folder.parent / 'runtime'
            if not json.loads((source / 'result.json').read_text()).get('passed'):
                raise RuntimeError('需要已通过的实机结果')
            shutil.copytree(source / 'completed-save', folder / 'completed-save', dirs_exist_ok=True)
            shutil.copy2(source / 'expected.json', folder / 'expected.json')
            legacy = options.legacy or Path.home() / '.local/state/cultiway-dev/evidence/erosion-creation/runs/20261117T164637.349221Z-6368e674/output/erosion-creation/save/completed-save'
            shutil.copytree(legacy, folder / 'legacy-save', dirs_exist_ok=True)
            raw = (legacy / 'map.wbox').read_bytes()
            saved = json.loads(zlib.decompress(raw))
            payload = json.loads(saved['mapStats']['custom_data']['custom_data_string']['cultiway.world'])
            old = payload['sections']['ability_constructions']
            if old['version'] not in (12, 13):
                raise RuntimeError('需要真实旧12或旧13构造区段')
            write_json(folder / 'legacy-definition.json', old)
            write_json(folder / 'legacy-source.json', dict(path=str(legacy), sha256=hashlib.sha256(raw).hexdigest()))
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "correspondence/' + section + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        shared = Path(__file__).with_name('correspondence_case.script').read_text()
        body = Path(__file__).with_name('verify_correspondence_' + section + '.script').read_text()
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
            raise RuntimeError('映照与关联复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
