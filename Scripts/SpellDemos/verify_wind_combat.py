#!/usr/bin/env python3
"""9.19：普通风场、原物卷起与赑风接触的独立实机复验。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, encode, write_json
import baseline_scenarios as baseline


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('carry', 'erosion', 'trial'), required=True)
    parser.add_argument('--record', choices=('native', 'worldfall'))
    args = parser.parse_args()
    section, view = args.section, args.record
    if view and section == 'trial':
        parser.error('连续录像用普通风垒或实际削甲；天劫保留原入口的命中证据')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过 develop 的 dev.py exec 运行风系复验')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'wind-{section}-{view or "check"}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(output))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('    if (fact.AbilityId.StartsWith("test.body-footwork."))\n', '')
    body = Path(__file__).with_name(f'verify_wind_combat_{section}.script').read_text()
    shared = Path(__file__).with_suffix('.script').read_text() if section != 'trial' else ''
    shared = shared.replace('const string inputRoot = "";',
                            'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    shared = shared.replace('const string recordView = "";', 'const string recordView = ' + json.dumps(view or '') + ';')
    script = output / 'input.script'
    script.write_text(helpers + '\n' + shared + '\n' + body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('风系复验失败，请查看 ' + str(output / 'checks.json'))
    if view:
        name = '挟砂风垒：原物卷起与落地' if section == 'carry' else '赑风削甲：剪切与实际损耗'
        encode(output.parent, dict(key=output.name, name=name, label='原速', variant=0,
                                   trajectory='', modifiers=[]), keep_frames=True)
    print(json.dumps(dict(section=section, output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
