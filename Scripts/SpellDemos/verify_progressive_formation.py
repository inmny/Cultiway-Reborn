#!/usr/bin/env python3
"""5.06限定验证，使用develop准备的同一独立游戏。"""
import argparse
import hashlib
import json
import os
import shutil
import zlib
from pathlib import Path

from common import PROJECT, write_json


def run_section(section, baseline, connected, state=None):
    """每段独立准备现成世界，只保留该段的真实结果。"""
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'progressive-formation' / section
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    shutil.copy2(Path(__file__).parent / 'fixtures/formation-rule22.json', folder / 'rule22.json')
    if state:
        shutil.copytree(state, folder / 'input-state', dirs_exist_ok=True)
    if section == 'cold':
        expected = json.loads((folder / 'input-state/pending-expected.json').read_text())
        map_bytes = (folder / 'input-state/pending-save/map.wbox').read_bytes()
        world = json.loads(zlib.decompress(map_bytes))
        saved_inputs = dict(mapSha256=hashlib.sha256(map_bytes).hexdigest())
        for name, key in (('current', 'sourceId'), ('old', 'legacyId')):
            actor = next(actor for actor in world['actors_data'] if actor['id'] == expected[key])
            saved_inputs[name] = json.loads(actor['custom_data_string']['cultiway.actor'])['sections']['ability_creation']
        write_json(folder / 'pending-inputs.json', saved_inputs)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "progressive-formation/' + section + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    body = Path(__file__).with_suffix('.script').read_text()
    if section == 'ui':
        body = Path(__file__).with_name('verify_progressive_formation_ui.script').read_text()
    if section in ('branch', 'surface', 'stopped'):
        body = ('string scenario = "' + section + '";\n' +
                Path(__file__).with_name('verify_progressive_formation_behavior.script').read_text())
    if section in ('save', 'cold'):
        body = Path(__file__).with_name('verify_progressive_formation_' + section + '.script').read_text()
    body = body.replace('Path.Combine(root,"Scripts/SpellDemos/fixtures/formation-rule22.json")', 'Path.Combine(folder,"rule22.json")')
    if section == 'legacy':
        body = (body.split('try\n{', 1)[0] + 'try\n{\n    var saved=' + body.split('    var saved=', 1)[1])
        body = body.replace('逐段成形的冻结输入、独立创作与衍生接入', '正式旧请求保持')
    if section == 'creation':
        body = body.split('    var saved=', 1)[0] + '    return new' + body.rsplit('    return new', 1)[1]
    script = folder / 'input.script'
    script.write_text('using Cultiway.Core.Persistence;\nusing UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n' + helpers + '\n' + body)
    write_json(folder / 'connection.json', connected)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('逐段成形验证未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


def main():
    """复用现有世界助手，保留实际输入、结果和原始失败。"""
    sections = ('creation', 'legacy', 'ui', 'branch', 'surface', 'stopped', 'save')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'cold') + sections, default='all')
    parser.add_argument('--state', type=Path, help='cold读取上次save输出目录，需先停止并重启同一环境')
    options = parser.parse_args()
    section = options.section
    if section == 'cold' and not options.state:
        parser.error('cold需要--state指定已完成的save输出目录')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过develop的dev.py exec运行此入口，以使用所属独立游戏')
    import baseline_scenarios as baseline
    connected = baseline.connect(False, validate_runtime=False)
    for current in sections if section == 'all' else (section,):
        run_section(current, baseline, connected, options.state)


if __name__ == '__main__':
    main()
