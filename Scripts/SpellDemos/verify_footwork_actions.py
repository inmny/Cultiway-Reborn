#!/usr/bin/env python3
"""复用防御战技对练档，实测正式步法的根点、落脚、转向和作用。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('retreat', 'charge', 'wall', 'vault', 'interrupt', 'archive', 'archive-view', 'cold'),
                        required=True)
    parser.add_argument('--archive', type=Path, required=True, help='防御战技对练档或本次保存的步法世界目录')
    parser.add_argument('--view', choices=('native', 'worldfall'), help='记录原版或 Worldfall 的正式步法')
    args = parser.parse_args()
    if args.view and args.case not in ('retreat', 'charge', 'wall', 'vault'):
        parser.error('录像请选择 retreat、charge、wall 或 vault')
    archive = args.archive.resolve()
    if not all((archive / file).is_file() for file in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式对练档或人物资料：{archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    folder = Path(tempfile.mkdtemp(prefix='footwork-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    base = base.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                        json.dumps(str(archive / 'archive')))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.")')
    base = base.replace('var hold = Cultiway.ModClass.L.StatusEffectLibrary.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    expected = json.loads((archive / 'expected.json').read_text())
    fighters = (scripts / 'verify_short_weapons.script').read_text().split('var bindingType =', 1)[0]
    fighters = fighters.replace('Make(64.35f)', f'World.world.units.get({int(expected["source"])}L)')
    fighters = fighters.replace('Make(65.2f)', f'World.world.units.get({int(expected["target"])}L)')
    config = f'const string selection = "{args.case}";\nconst string archived = {json.dumps(str(archive))};\n'
    imports = 'using Friflo.Engine.ECS;\n'
    if args.case in ('archive', 'archive-view'):
        imports += 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
        helpers = (scripts / 'verify_martial_footwork.script').read_text()
        config += helpers[helpers.index('string T('):helpers.index('Click("Page2");')]
        basis = expected['edited'] if args.case == 'archive-view' else 'Cultiway.Body.AngledRetreat'
        setup = f'var basis = ConstructionEditing.Access.Read({json.dumps(basis)});\n'
        if args.case == 'archive':
            setup += 'ConstructedAbilityService.Learn(owner, basis.Id);\n'
        config = setup + config
        body_name = 'verify_footwork_actions_archive.script'
    elif args.case == 'interrupt':
        body_name = 'verify_footwork_actions_interrupt.script'
    else:
        body_name = 'verify_footwork_actions.script'
    body = (scripts / body_name).read_text()
    if body_name == 'verify_footwork_actions.script':
        if args.view:
            config += f'const string view = "{args.view}";\n'
            camera = (scripts / 'verify_unarmed_film.script').read_text()
            setup = camera.split('    foreach (var item in cases.Where', 1)[0]
            setup = setup.replace('64.73f, 72.34f', '64.85f, 72.10f')
            hooks = (scripts / 'verify_footwork_actions_film.script').read_text()
            body = (setup + hooks + body + '\n}\nfinally\n' + camera.rsplit('\nfinally\n', 1)[1])
        else:
            config += ('const string view = "";\nFunc<string, Task> captureFrame = null;\n'
                       'Action<string, int, float> saveFilm = null;\n')
    script = folder / 'input.script'
    script.write_text(imports + base + fighters + config + body)
    try:
        result = baseline.call('run', str(script), '240000' if args.view else '140000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'步法战技复验见 {folder}') from error
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'步法战技复验未通过，见 {folder}')
    if args.view:
        from record_body_motion import encode

        encode(folder, json.loads((folder / 'recording.json').read_text()), args.view)
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
