#!/usr/bin/env python3
"""用原狼形和狐形实测正式尾扫、翼击及同身分别出手。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('tail', 'pair', 'loss', 'archive', 'cold'), required=True)
    parser.add_argument('--view', choices=('native', 'worldfall'))
    parser.add_argument('--archive', type=Path, help='archive 场景保存的目录')
    args = parser.parse_args()
    if args.view and args.case not in ('tail', 'pair'):
        parser.error('录像使用 tail 或 pair')
    if args.case == 'cold' and (not args.archive or not all(
            (args.archive / file).is_file() for file in ('archive/map.wbox', 'expected.json'))):
        parser.error('读档需要 --archive <archive 场景输出目录>')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix='nonhuman-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helper = (scripts / 'verify_body_organs.script').read_text().split('\nBodyFrame Frame(object binding, Actor actor)', 1)[0] + '\n'
    helper = helper.replace('Path.Combine(root, "artifacts/skill_rebuild/body-organs")', json.dumps(str(folder)))
    archive = args.archive.resolve() if args.case == 'cold' else None
    fixture = archive / 'archive' if archive else PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helper = helper.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helper = helper.replace('contact.AbilityId.StartsWith("test.body-organs.")',
                            'contact.AbilityId.StartsWith("Cultiway.Body.") || contact.AbilityId.StartsWith("Cultiway.Edited.")')
    helper = helper.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                            'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    setup = (f'const string selection = "{args.case}";\nconst string view = "{args.view or ""}";\n'
             f'const string archived = {json.dumps(str(archive)) if archive else "null"};\n')
    initial, run = (scripts / 'verify_nonhuman_actions.script').read_text().split('// 开始本段\n', 1)
    if args.view:
        camera = (scripts / 'verify_unarmed_film.script').read_text()
        start = camera.split('    foreach (var item in cases.Where', 1)[0]
        start = start.replace('64.73f, 72.34f', '64.45f, 72.42f')
        start = start.replace('    CombatStatusEffects.RemoveStatus(source, hold);\n    source.updateStats();\n', '')
        capture = (scripts / 'verify_footwork_actions_film.script').read_text().split('    Action<string, int, float>', 1)[0]
        save_film = '''Action<string, int, float> saveFilm = (ability, count, cost) =>
        {
            string name = selection == "tail" ? "卷尾横扫" : "同身分别出手 · 尾扫与翼切";
            recordings.Add(new { view, ability, name, rate = 1, first = 0, last = rows.Count - 1,
                contacts = count, duration = World.world.getCurWorldTime() - firstFrameAt.Value, paid = cost });
            SaveRecording();
        };\n'''
        begin = start + capture + save_film
        end = '\n}\nfinally\n' + camera.rsplit('\nfinally\n', 1)[1]
        end = end.replace('    ConstructedAbilityService.ContactConfirmed -= Hit;\n', '')
        end = end.replace('    ConstructedAbilityService.CastEnded -= Ended;\n', '')
    else:
        begin = 'Func<string, Task> captureFrame = null;\nAction<string, int, float> saveFilm = null;\n'
        end = ''
    script = folder / 'input.script'
    ui = (scripts / 'verify_nonhuman_actions_archive.script').read_text()
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.SkillLibV3;\n'
                      'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' +
                      helper + setup + initial + ui + begin + run + end)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'非人战技复验见 {folder}') from error
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'非人战技复验未通过，见 {folder}')
    if args.view:
        from record_body_motion import encode

        encode(folder, json.loads((folder / 'recording.json').read_text()), args.view)
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
