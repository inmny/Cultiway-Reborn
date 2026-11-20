#!/usr/bin/env python3
"""复用现成世界，核对影中葬身的真实接触与控制归还。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('grip', 'drag', 'release', 'save', 'variant', 'cold', 'film'), default='grip')
    parser.add_argument('--case', choices=('all', 'rescue', 'escape', 'death', 'world'), default='all')
    parser.add_argument('--view', choices=('native', 'worldfall'), default='native')
    parser.add_argument('--saved', type=Path)
    args = parser.parse_args()
    if args.section in ('variant', 'cold') and args.saved is None:
        parser.error('variant 和 cold 都需要 --saved 指向 save 段输出目录')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'shadow-burial-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(fixture)))
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.ShadowBurial")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    shared = (scripts / 'verify_wind_combat.script').read_text()
    shared = shared.replace('const string inputRoot = "";',
                            'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.ShadowBurial.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.ShadowBurial.Mana")')
    shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.view}";')
    shared = shared.replace('67.7f, 72f', '69.6f, 72.5f')
    grip = (scripts / 'verify_shadow_burial.script').read_text()
    drag = (scripts / 'verify_shadow_burial_drag.script').read_text()
    if args.section == 'grip':
        body = grip
    elif args.section in ('drag', 'release'):
        body = grip.split('try\n{', 1)[0] + f'\nconst string section = "{args.section}", part = "{args.case}";\n' + drag
    elif args.section in ('save', 'variant', 'film'):
        saved = str(args.saved.resolve()) if args.saved else ''
        body = grip.split('try\n{', 1)[0] + drag.split('try\n{', 1)[0] + (
            f'\nconst string section = "{args.section}", existing = {json.dumps(saved)};\n' +
            (scripts / 'verify_shadow_burial_delivery.script').read_text())
    else:
        helpers = helpers.split('Game.Resume();', 1)[0]
        shared = ''
        body = 'string saved = ' + json.dumps(str(args.saved.resolve())) + ';\n' + (
            scripts / 'verify_shadow_burial_cold.script').read_text()
    script = output / 'input.script'
    script.write_text(helpers + '\n' + shared + '\n' + body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'影中葬身复验结果位于 {output}') from error
    write_json(output / 'result.json', result)
    if result.get('passed') and args.section == 'film':
        encode(output.parent, dict(key=output.name, name='影中葬身', label='正式施放', variant=1,
                                   trajectory='static', modifiers=[], caption='原身的影反向站起，合臂后拖入深影，再归还活体'),
               keep_frames=True)
    print(json.dumps({'output': str(output), 'result': result}, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('影中葬身未通过')


if __name__ == '__main__':
    main()
