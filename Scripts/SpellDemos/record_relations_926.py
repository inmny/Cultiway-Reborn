#!/usr/bin/env python3
"""录制 9.26 的真实折景、魂契和传讯；沿实际世界时间编码。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json

CASES = {'gate': '折景渡物', 'pact': '魂魄替负', 'signal': '照影传讯'}


CAPTIONS = {
    'gate': ('取景、通行与关门', '飞剑从真实出口通过'),
    'pact': ('确实替负、耗尽与撤去', '实际承伤后才传递'),
    'signal': ('记录、传达与过期', '留在交战时记下的位置'),
}


def encode_take(output, case):
    suffix, caption = CAPTIONS[case]
    encode(output.parent, dict(key=output.name, name=f'{CASES[case]}：{suffix}', label='原速', variant=0,
                               trajectory='', modifiers=[], caption=caption), keep_frames=True)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=CASES, required=True)
    parser.add_argument('--view', choices=('native', 'worldfall'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    name = CASES[args.case]
    output = Path(tempfile.mkdtemp(prefix=f'relations-926-{args.case}-{args.view}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', output / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入' / f'{name}.json', output / 'definition.json')
    scripts = Path(__file__).parent
    helper = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helper = helper.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helper = helper.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helper = helper.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                            'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    wind = (scripts / 'verify_wind_combat.script').read_text()
    if args.case == 'gate':
        helper = helper.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
        camera = wind[wind.index('async Task<List<double>> Film('):].replace(
            '            Screen.Capture',
            '            World.world.console.Hide();\n            Screen.Capture')
        setup = (scripts / 'verify_fold_gate.script').read_text().split('// CHECKS', 1)[0]
        setup = f'const string view = "{args.view}";\n' + setup
        scenery = (scripts / 'verify_fold_gate_visual.script').read_text()
        scenery = scenery[scenery.index('Type external'):]
        start = scenery.index('    Place(source, 64);')
        end = scenery.index('\n}\nfinally', start)
        body = setup + scenery[:start] + (scripts / 'record_relations_926_gate.script').read_text() + scenery[end:]
    else:
        camera = 'const string recordView = ' + json.dumps(args.view) + ';\n' + wind[wind.index('object cameraSettings'):]
        cut = camera.index('void ReleaseChecks()'); tail = camera.index('async Task SetCamera(')
        camera = camera[:cut] + camera[tail:]
        body = f'const string sceneCase = "{args.case}";\n' + (scripts / 'record_relations_926_pact_signal.script').read_text()
    script = output / 'input.script'
    script.write_text('using Cultiway.Const;\n' + helper + '\n' + camera + '\n' + body)
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed') or not result.get('times'):
        raise RuntimeError(f'常速录像未覆盖真实结果：{output}')
    encode_take(output, args.case)
    print(json.dumps(dict(output=str(output), video=str(output / 'demo.mp4'), view=args.view), ensure_ascii=False))


if __name__ == '__main__':
    main()
