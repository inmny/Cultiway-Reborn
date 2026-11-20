#!/usr/bin/env python3
"""在独立游戏中复验折界千重的取景、闭合和交付。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('image', 'contact', 'save', 'restored', 'cold', 'film'), default='image')
    parser.add_argument('--view', choices=('native', 'worldfall'), default='native')
    parser.add_argument('--saved', type=Path)
    args = parser.parse_args()
    if args.section in ('restored', 'cold') and args.saved is None:
        parser.error('restored 和 cold 需要 --saved 指向 save 的输出目录')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'thousand-fold-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.ThousandFold")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    if args.section == 'cold':
        script = helpers.split('Game.Resume();', 1)[0]
        script += 'string saved = ' + json.dumps(str(args.saved.resolve())) + ';\n'
    else:
        shared = (scripts / 'verify_wind_combat.script').read_text()
        shared = shared.replace('const string inputRoot = "";',
                                'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
        shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                                'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.ThousandFold.Mana") ?? '
                                'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.ThousandFold.Mana")')
        shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.view}";')
        start, end = shared.index('    // 复用原测试场'), shared.index('    World.world.camera')
        shared = shared[:start] + shared[end:]
        shared = shared.replace('67.7f, 72f', '69f, 72f')
        shared = shared.replace('        Tooltip.blockTooltips(.25f);',
                                '        WorldTip.hideNow();\n        Tooltip.blockTooltips(.25f);')
        script = helpers + '\n' + shared + '\n' + (scripts / 'verify_thousand_fold_scene.script').read_text()
    body = args.section if args.section in ('image', 'contact', 'cold') else 'delivery'
    if body == 'delivery':
        existing = str(args.saved.resolve()) if args.saved else ''
        script += f'\nconst string section = "{args.section}", existing = {json.dumps(existing)};\n'
    script += (scripts / f'verify_thousand_fold_{body}.script').read_text()
    path = output / 'input.script'
    path.write_text(script)
    try:
        result = baseline.call('run', str(path), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'折界千重复验结果位于 {output}') from error
    write_json(output / 'result.json', result)
    if result.get('passed') and args.section == 'film':
        encode(output.parent, dict(key=output.name, name='折界千重', label='正式施放', variant=1,
                                   trajectory='static', modifiers=[], caption='实景错层、依次闭合、断面切割和归位'),
               keep_frames=True)
    print(json.dumps({'output': str(output), 'result': result}, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('折界千重未通过')


if __name__ == '__main__':
    main()
