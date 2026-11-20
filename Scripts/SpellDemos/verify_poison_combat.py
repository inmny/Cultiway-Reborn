#!/usr/bin/env python3
"""验证拂地毒息、蚀甲毒潭和瘴潮凝潭的实际状态、结算及常速录像。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, encode, write_json
import baseline_scenarios as baseline


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('breath', 'pool', 'condense'), required=True)
    parser.add_argument('--record', choices=('native', 'worldfall'))
    parser.add_argument('--miss', action='store_true', help='只检查瘴潮飞空')
    parser.add_argument('--save', action='store_true', help='在瘴潮凝潭退场后保存同一实际世界')
    args = parser.parse_args()
    if args.miss and (args.section != 'condense' or args.record):
        parser.error('飞空复验只适用于瘴潮凝潭，且无需录制')
    if args.save and (args.section != 'condense' or args.miss):
        parser.error('正式存档需要瘴潮凝潭的正常场景')
    section, view = args.section, args.record
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过 develop 的 dev.py exec 运行毒系复验')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'poison-{section}-{view or "check"}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(output))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('    if (fact.AbilityId.StartsWith("test.body-footwork."))\n', '')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    body = Path(__file__).with_name(f'verify_poison_combat_{section}.script').read_text()
    shared = Path(__file__).with_name('verify_wind_combat.script').read_text()
    shared = shared.replace('const string inputRoot = "";',
                            'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    shared = shared.replace('const string recordView = "";', 'const string recordView = ' + json.dumps(view or '') + ';')
    # 两项复用同一施放、取景和录像入口，避免再建测试世界。
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.Poison.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Poison.Mana")')
    if section == 'condense':
        shared += f'\nconst bool miss = {str(args.miss).lower()};\nconst bool save = {str(args.save).lower()};'
    script = output / 'input.script'
    script.write_text(helpers + '\n' + shared + '\n' + body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('毒系复验失败，请查看 ' + str(output / 'checks.json'))
    if view:
        name = {
            'breath': '拂地毒息：传播、附着与退场',
            'pool': '蚀甲毒潭：留在潭内才掉耐久',
            'condense': '瘴潮凝潭：传播、凝聚和蚀甲',
        }[section]
        encode(output.parent, dict(key=output.name, name=name, label='原速', variant=0,
                                   trajectory='', modifiers=[]), keep_frames=True)
    print(json.dumps(dict(section=section, output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
