#!/usr/bin/env python3
"""9.23：可编辑双端渡气、旧汲取和两种常速视角。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, encode, write_json
import baseline_scenarios as baseline


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('balance', 'editor', 'legacy'), required=True)
    parser.add_argument('--record', choices=('native', 'worldfall'))
    args = parser.parse_args()
    section, view = args.section, args.record
    if view and section != 'balance':
        parser.error('只录制完整双端渡气')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过 develop 的 dev.py exec 运行两仪渡气复验')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'polarity-{section}-{view or "check"}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(output))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    shared = (scripts / 'verify_wind_combat.script').read_text().replace('const string inputRoot = "";',
        'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    shared = shared.replace('const string recordView = "";', 'const string recordView = ' + json.dumps(view or '') + ';')
    shared = shared.replace('Cultiway.Verify.Wind.Mana', 'Cultiway.Verify.Polarity.Mana')
    imports = 'using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n'
    if section == 'editor':
        ui = (scripts / 'verify_contact_creation_ui.script').read_text()
        body = (scripts / 'verify_polarity_editor.script').read_text().replace('// UI HELPERS',
            ui[ui.index('async Task Frames('):ui.index('try\n{')])
        shared = shared.split('var source = Make(', 1)[0]
    elif section == 'legacy':
        legacy = (scripts / 'verify_yin_combat_intake.script').read_text()
        # 保留账户原样和原正式入口，抽查一次足额的单向汲取。
        start = legacy.index('    Place(victim, 69, 77);')
        body = legacy[:start] + '    return new { passed = true, transferred = added, removed, hits = hitCount };\n}\n' + legacy[legacy.index('finally\n{'):]
    else:
        body = (scripts / 'verify_polarity_balance.script').read_text()
    script = output / 'input.script'
    script.write_text(imports + helpers + '\n' + shared + '\n' + body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('两仪复验失败，请查看 ' + str(output / 'checks.json'))
    if view:
        encode(output.parent, dict(key=output.name, name='两仪渡气：送出、承接、反转与止息', label='原速',
                                   variant=0, trajectory='', modifiers=[]), keep_frames=True)
    print(json.dumps(dict(section=section, output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
