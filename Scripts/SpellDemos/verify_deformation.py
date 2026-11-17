#!/usr/bin/env python3
"""5.09限定复验，复用develop独立环境和现成世界。"""
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
    parser.add_argument('--section', choices=('all', 'core', 'pricing', 'inheritance', 'cast', 'ui', 'creation', 'legacy', 'save', 'cold'), default='all')
    parser.add_argument('--creation', type=Path, help='save读取已通过的creation输出，缺省使用本进程结果')
    parser.add_argument('--state', type=Path, help='cold读取上次save目录，需先停止再启动同一环境')
    options = parser.parse_args()
    section = options.section
    if section == 'cold' and not options.state:
        parser.error('cold需要--state指定已完成的save输出目录')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for current in ('core', 'inheritance', 'cast', 'ui', 'creation', 'legacy', 'save') if section == 'all' else (section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'deformation' / current
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        shutil.copy2(Path(__file__).parent / 'fixtures/deformation-rule23.json', folder / 'rule23.json')
        if current == 'save':
            creation = options.creation or folder.parent / 'creation'
            if not json.loads((creation / 'result.json').read_text()).get('passed'):
                raise RuntimeError('正式保存需要已通过的创法输入')
            shutil.copy2(creation / 'basis.json', folder / 'basis.json')
            shutil.copy2(creation / 'derived.json', folder / 'selected.json')
        if current == 'cold':
            shutil.copytree(options.state, folder / 'input-state', dirs_exist_ok=True)
            expected = json.loads((folder / 'input-state/pending-expected.json').read_text())
            map_bytes = (folder / 'input-state/pending-save/map.wbox').read_bytes()
            world = json.loads(zlib.decompress(map_bytes))
            saved = dict(mapSha256=hashlib.sha256(map_bytes).hexdigest())
            for name, key in (('current', 'sourceId'), ('old', 'legacyId')):
                actor = next(actor for actor in world['actors_data'] if actor['id'] == expected[key])
                saved[name] = json.loads(actor['custom_data_string']['cultiway.actor'])['sections']['ability_creation']
            write_json(folder / 'pending-inputs.json', saved)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "deformation/' + current + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        script = folder / 'input.script'
        shared = Path(__file__).with_name('deformation_case.script').read_text()
        if current == 'ui':
            ui = Path(__file__).with_name('verify_progressive_formation_ui.script').read_text().split('try\n{', 1)[0]
            ui = ui.replace('test.progressive-formation.editor', 'test.deformation.editor').replace('逐段成形编辑', '自由形变编辑')
            body = ui + Path(__file__).with_name('verify_deformation_ui.script').read_text()
        elif current == 'cold':
            body = Path(__file__).with_name('verify_progressive_formation_cold.script').read_text()
            body = body.replace('"rule22.json"', '"rule23.json"').replace('["version"] == 24', '["version"] == 25')
            body = body.replace('storedCurrent["Inspiration"]["Rules"] == 23', 'storedCurrent["Inspiration"]["Rules"] == 24')
            body = body.replace('storedOld["Inspiration"]["Rules"] == 22', 'storedOld["Inspiration"]["Rules"] == 23')
            body = body.replace('pending.Inspiration.Rules == 23', 'pending.Inspiration.Rules == 24').replace('old.Inspiration.Rules == 22', 'old.Inspiration.Rules == 23')
            body = body.replace('逐段成形', '自由形变')
        else:
            body = Path(__file__).with_name('verify_deformation_' + current + '.script').read_text()
        script.write_text('using Cultiway.Core.Persistence;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('形变复验未通过 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
