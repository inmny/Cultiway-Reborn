#!/usr/bin/env python3
"""在独立游戏里复验徒手战技的实际接触和收势。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('upper', 'palm', 'punch', 'elbow', 'miss', 'lower', 'knee',
                                              'sweep', 'shoulder', 'sweep-miss', 'hand-edit', 'archive', 'cold', 'film'),
                        default='upper')
    parser.add_argument('--archive', type=Path, help='新进程读取的正式存档输出目录')
    parser.add_argument('--view', choices=('native', 'worldfall'), help='录像视角')
    parser.add_argument('--move', choices=('palm', 'punch', 'elbow', 'knee', 'sweep', 'shoulder'),
                        help='只录指定的一招')
    args = parser.parse_args()
    section = args.section
    if (section == 'cold') != (args.archive is not None):
        parser.error('--archive 只与 --section cold 一起使用')
    if (section == 'film') != (args.view is not None) or args.move and section != 'film':
        parser.error('--view 和 --move 用于 --section film')
    if args.archive and not all((args.archive / name).is_file() for name in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式世界或其记录：{args.archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'unarmed-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    base = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    base = base.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(PROJECT / fixture)))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.")')
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    script = output / 'input.script'
    imports = 'using Friflo.Engine.ECS;\n'
    scripts = Path(__file__).parent
    body = Path(__file__).with_suffix('.script').read_text()
    if section in ('hand-edit', 'archive'):
        imports += 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
        hand = (scripts / 'verify_unarmed_hand_editor.script').read_text()
        if section == 'archive':
            body = hand.split('\ntry\n', 1)[0].replace('Cultiway.Body.PalmPush', 'Cultiway.Body.ShoulderDrive')
            body += '\n' + (scripts / 'verify_unarmed_archive.script').read_text()
        else:
            body = hand
    if section == 'cold':
        base = base.split('Game.Resume();', 1)[0]
        body = f'\nconst string archived = {json.dumps(str(args.archive.resolve()))};\n'
        body += (scripts / 'verify_unarmed_cold.script').read_text()
    if section == 'film':
        helpers = body.split('AbilityCast live =', 1)[0]
        cases = body[body.index('    var cases ='):body.index('    var selectedCases')]
        body = helpers + cases + f'\nconst string view = "{args.view}", selectedMove = "{args.move or ""}";\n'
        body += (scripts / 'verify_unarmed_film.script').read_text()

    script.write_text(imports + base + f'\nconst string section = "{section}";\n' + body)
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'徒手战技复验记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'徒手战技复验未通过，见 {output}')
    if section == 'film':
        from record_body_motion import encode

        encode(output, json.loads((output / 'recording.json').read_text()), args.view)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
