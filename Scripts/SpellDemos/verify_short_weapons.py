#!/usr/bin/env python3
"""用同一人形对练验证短兵器形、握持与实际接触。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('fighting', 'archive', 'cold', 'film', 'knight'), default='fighting')
    parser.add_argument('--item', choices=('all', 'iron', 'stone'), default='all')
    parser.add_argument('--move', choices=('thrust', 'cut', 'sweep', 'miss', 'deflect',
                                            'committed', 'flurry', 'flurry-swap',
                                            'guard', 'guard-hammer', 'counter', 'stance-loss',
                                            'repulse', 'repulse-swap'))
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--view', choices=('native', 'worldfall'))
    args = parser.parse_args()
    section = args.section
    stance_moves = ('guard', 'guard-hammer', 'counter', 'stance-loss')
    repulse_moves = ('repulse', 'repulse-swap')
    knight_moves = ('committed', 'flurry', 'flurry-swap') + stance_moves + repulse_moves
    if args.move is not None and (args.move in knight_moves) != (section == 'knight'):
        parser.error('骑士战技使用 --section knight 并选择相应的 --move')
    if args.move == 'deflect' and args.item == 'stone':
        parser.error('拨开复验使用铁剑')
    if (section == 'cold') != (args.archive is not None) or (section == 'film') != (args.view is not None):
        parser.error('冷读需指定 --archive，录像需指定 --view')
    if section == 'film' and args.move in ('miss', 'deflect'):
        parser.error('选择短刺、斜切或横扫录像')
    if args.archive and not all((args.archive / name).is_file() for name in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式存档及记录：{args.archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    move = args.move or ('committed' if section == 'knight' else 'thrust')
    name = f'{move}-{args.item}' if section == 'fighting' else section
    output = Path(tempfile.mkdtemp(prefix=f'short-weapons-{name}-',
                                   dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    base = base.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(PROJECT / fixture)))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.ShortBlade")')
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    script = output / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text()
    imports = 'using Friflo.Engine.ECS;\n'
    if section == 'knight':
        base = base.replace('Cultiway.Body.ShortBlade', 'Cultiway.KnightTechnique.')
        imports += ('using Cultiway.Core.SkillLibV3;\n'
                    'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Content.KnightCombat;\n')
        script_name = 'verify_knight_duelist.script'
        if move in stance_moves:
            script_name = 'verify_knight_stances.script'
            imports += ('using Cultiway.Core.Combat;\nusing Cultiway.Core.EventSystem.Events;\n'
                        'using Cultiway.Core.EventSystem.Systems;\n')
        elif move in repulse_moves:
            script_name = 'verify_knight_repulse.script'
        body = body.split('var bindingType =', 1)[0] + (scripts / script_name).read_text()
    elif section == 'archive':
        imports += 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
        editor = (scripts / 'verify_unarmed_hand_editor.script').read_text().split('\ntry\n', 1)[0]
        body = editor.replace('Cultiway.Body.PalmPush', 'Cultiway.Body.ShortBladeCut')
        body += '\n' + (scripts / 'verify_short_weapons_archive.script').read_text()
    elif section == 'cold':
        imports += 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
        base = base.split('Game.Resume();', 1)[0]
        body = f'const string archived = {json.dumps(str(args.archive.resolve()))};\n'
        body += (scripts / 'verify_short_weapons_cold.script').read_text()
    elif section == 'film':
        recording = (scripts / 'verify_unarmed_film.script').read_text()
        body = body.split('var bindingType =', 1)[0] + f'\nconst string view = "{args.view}", selectedMove = "{args.move or ""}";\n'
        body += (scripts / 'verify_short_weapons_film.script').read_text()
        body += recording.replace('Place(source, 64.35f);\n        Place(target, 64.35f + height * item.range);',
                                  'Equip(source, item.asset);\n        Place(source, 64.35f);\n        Place(target, 64.35f + item.range);')
        body = body.replace('selectedMove == "" ? 6 : 1', 'selectedMove == "" ? cases.Length : 1')
    elif move == 'deflect':
        body = body.split('var clock =', 1)[0] + (scripts / 'verify_short_weapons_deflect.script').read_text()
    script.write_text(imports + base +
                      f'\nconst string section = "{section}", selected = "{args.item}", move = "{move}";\n' + body)
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'短兵复验记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'短兵复验未通过，见 {output}')
    if section == 'film':
        from record_body_motion import encode

        encode(output, json.loads((output / 'recording.json').read_text()), args.view)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
