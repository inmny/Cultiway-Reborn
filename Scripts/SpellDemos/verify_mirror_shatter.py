#!/usr/bin/env python3
"""在独立游戏里核对镜中碎身的原身、镜口和退场。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('image', 'travel', 'interrupt', 'save', 'restored', 'cold', 'film'), default='image')
    parser.add_argument('--view', choices=('native', 'worldfall'), default='native')
    parser.add_argument('--saved', type=Path)
    args = parser.parse_args()
    if args.section in ('restored', 'cold') and args.saved is None:
        parser.error('restored 和 cold 都需要 --saved 指向 save 的输出目录')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'mirror-shatter-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.MirrorShatter")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    shared = (scripts / 'verify_wind_combat.script').read_text()
    shared = shared.replace('const string inputRoot = "";',
                            'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.MirrorShatter.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.MirrorShatter.Mana")')
    shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.view}";')
    shared = shared.replace('67.7f, 72f', '67.6f, 73f')
    image = (scripts / 'verify_mirror_shatter_image.script').read_text()
    travel = (scripts / 'verify_mirror_shatter_travel.script').read_text()
    if args.section == 'image':
        body = image
    elif args.section == 'cold':
        helpers = helpers.split('Game.Resume();', 1)[0]
        shared = ''
        body = 'string saved = ' + json.dumps(str(args.saved.resolve())) + ';\n' + (
            scripts / 'verify_mirror_shatter_cold.script').read_text()
    else:
        body = image.split('try\n{', 1)[0]
        if args.section == 'travel':
            body += travel
        else:
            body += travel.split('try\n{', 1)[0]
            if args.section == 'interrupt':
                body += (scripts / 'verify_mirror_shatter_interrupt.script').read_text()
            else:
                existing = str(args.saved.resolve()) if args.saved else ''
                body += f'\nconst string section = "{args.section}", existing = {json.dumps(existing)};\n' + (
                    scripts / 'verify_mirror_shatter_delivery.script').read_text()
    script = output / 'input.script'
    script.write_text(helpers + '\n' + shared + '\n' + body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'镜中碎身复验结果位于 {output}') from error
    write_json(output / 'result.json', result)
    if result.get('passed') and args.section == 'film':
        encode(output.parent, dict(key=output.name, name='镜中碎身', label='正式施放', variant=1,
                                   trajectory='static', modifiers=[], caption='古镜映照、真实击镜、局部裂势、原身反馈和落片'),
               keep_frames=True)
    print(json.dumps({'output': str(output), 'result': result}, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('镜中碎身未通过')


if __name__ == '__main__':
    main()
