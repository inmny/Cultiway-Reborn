#!/usr/bin/env python3
"""用同一对练档检查实际锤面下落、命中回弹和空挥收势。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('drop', 'miss', 'guard', 'interrupt', 'archive', 'cold', 'film'), required=True)
    parser.add_argument('--archive', type=Path, required=True, help='正式对练档目录；cold 使用 archive 的输出目录')
    parser.add_argument('--view', choices=('native', 'worldfall'), help='正式录像的实际游戏视角')
    parser.add_argument('--move', choices=('drop', 'miss', 'guard'), help='分别录制命中、空挥触地或架挡')
    args = parser.parse_args()
    if args.case == 'film' and not (args.view and args.move) or args.case != 'film' and (args.view or args.move):
        parser.error('--case film 需要同时指定 --view 和 --move')
    archive = args.archive.resolve()
    if not all((archive / file).is_file() for file in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式对练档或人物核对资料：{archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix='heavy-weapons-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    base = base.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                        json.dumps(str(archive / 'archive')))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.HeavyHammer")')
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    expected = json.loads((archive / 'expected.json').read_text())
    fighters = (scripts / 'verify_short_weapons.script').read_text().split('var bindingType =', 1)[0]
    fighters = fighters.replace('Make(64.35f)', f'World.world.units.get({int(expected["source"])}L)')
    fighters = fighters.replace('Make(65.2f)', f'World.world.units.get({int(expected["target"])}L)')
    imports = 'using Friflo.Engine.ECS;\n'
    settings = f'\nconst string selection = "{args.case}";\n'
    body_name = {
        'guard': 'verify_heavy_weapon_block.script',
        'interrupt': 'verify_heavy_weapon_block.script',
        'archive': 'verify_heavy_weapon_archive.script',
        'cold': 'verify_heavy_weapon_cold.script',
        'film': 'verify_heavy_weapon_film.script',
    }.get(args.case, 'verify_heavy_weapons.script')
    if args.case == 'archive':
        imports += 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
        helpers = (scripts / 'verify_unarmed_hand_editor.script').read_text().split('\ntry\n', 1)[0]
        helpers = helpers[helpers.index('const string basisId'):].replace('Cultiway.Body.PalmPush', 'Cultiway.Body.HeavyHammerDrop')
        fighters += '\nvar hammer = Equip(source, "hammer_iron");\n' + helpers
    elif args.case == 'cold':
        base = base.split('Game.Resume();', 1)[0]
        fighters = ''
        settings += f'const string archived = {json.dumps(str(archive))};\n'
    body = (scripts / body_name).read_text()
    if args.case in ('guard', 'interrupt', 'film'):
        settings += (scripts / 'verify_heavy_weapon_shield.script').read_text() + '\n'
    if args.case == 'film':
        settings += f'const string view = "{args.view}", selectedMove = "{args.move}";\n'
        camera = (scripts / 'verify_unarmed_film.script').read_text()
        setup = camera.split('    foreach (var item in cases.Where', 1)[0].replace('64.73f, 72.34f', '64.8f, 72.18f')
        body = ('owner.E.RemoveComponent<ConstructedKnowledge>();\n' + setup + body +
                '\nfinally\n' + camera.rsplit('\nfinally\n', 1)[1])
    script = output / 'input.script'
    script.write_text(imports + base + fighters + settings + body)
    try:
        result = baseline.call('run', str(script), '260000' if args.case == 'film' else '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'重兵复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'重兵复验未通过，见 {output}')
    if args.case == 'film':
        from record_body_motion import encode

        encode(output, json.loads((output / 'recording.json').read_text()), args.view)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
