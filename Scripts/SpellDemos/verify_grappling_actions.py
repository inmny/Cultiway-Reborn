#!/usr/bin/env python3
"""用步法正式对练档复验抓投、实际环境撞击和中断。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('throw', 'press', 'miss', 'interrupt', 'archive', 'archive-view', 'cold'),
                        required=True)
    parser.add_argument('--archive', type=Path, required=True, help='正式对练存档目录')
    parser.add_argument('--view', choices=('native', 'worldfall'), help='录制实际抓投过程')
    args = parser.parse_args()
    if args.view and args.case not in ('throw', 'press'):
        parser.error('录像请选择 throw 或 press')
    archive = args.archive.resolve()
    if not all((archive / name).is_file() for name in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式对练档或人物资料：{archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    folder = Path(tempfile.mkdtemp(prefix='grappling-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    footwork = (scripts / 'verify_body_footwork.script').read_text()
    prefix = footwork.split('var bindingType =', 1)[0]
    prefix = prefix.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    prefix = prefix.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                            json.dumps(str(archive / 'archive')))
    prefix = prefix.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.")')
    prefix = prefix.replace('var hold = Cultiway.ModClass.L.StatusEffectLibrary.StartBuild',
                            'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    expected = json.loads((archive / 'expected.json').read_text())
    fighters = (scripts / 'verify_short_weapons.script').read_text().split('var bindingType =', 1)[0]
    fighters = fighters.replace('Make(64.35f)', f'World.world.units.get({int(expected["source"])}L)')
    fighters = fighters.replace('Make(65.2f)', f'World.world.units.get({int(expected["target"])}L)')
    imports = 'using Friflo.Engine.ECS;\n'
    setup = f'const string selection = "{args.case}";\nconst string archived = {json.dumps(str(archive))};\n'
    if args.case in ('archive', 'archive-view'):
        imports += 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\nusing UnityEngine.EventSystems;\n'
        editor = (scripts / 'verify_unarmed_hand_editor.script').read_text().split('\ntry\n', 1)[0]
        basis = expected['edited'] if args.case == 'archive-view' else 'Cultiway.Body.PressingThrow'
        setup += editor[editor.index('const string basisId'):].replace('"Cultiway.Body.PalmPush"', json.dumps(basis))
        body = (scripts / 'verify_grappling_actions_archive.script').read_text()
    else:
        body = (scripts / 'verify_grappling_actions.script').read_text()
        if args.view:
            setup += f'const string view = "{args.view}";\n'
            camera = (scripts / 'verify_unarmed_film.script').read_text()
            camera_setup = camera.split('    foreach (var item in cases.Where', 1)[0]
            camera_setup = camera_setup.replace('64.73f, 72.34f', '64.20f, 72.43f')
            capture = (scripts / 'verify_footwork_actions_film.script').read_text().split('    Action<string, int, float>', 1)[0]
            finish = (scripts / 'verify_grappling_actions_film.script').read_text()
            body = camera_setup + capture + finish + body + '\n}\nfinally\n' + camera.rsplit('\nfinally\n', 1)[1]
        else:
            setup += 'Func<string, Task> captureFrame = null;\nAction<string, int, float> saveFilm = null;\n'
    script = folder / 'input.script'
    script.write_text(imports + prefix + fighters + setup + body)
    try:
        result = baseline.call('run', str(script), '245000' if args.view else '160000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'抓投复验见 {folder}') from error
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'抓投复验未通过，见 {folder}')
    if args.view:
        from record_body_motion import encode

        encode(folder, json.loads((folder / 'recording.json').read_text()), args.view)
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
