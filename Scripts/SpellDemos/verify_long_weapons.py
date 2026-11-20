#!/usr/bin/env python3
"""用原版铁枪复验滑握、枪尖和杆身各自的实际作用及完整回收。"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('all', 'strike', 'near', 'sweep', 'interrupt', 'press', 'edit', 'range',
                                           'obstacle', 'archive', 'cold', 'film'), default='all')
    parser.add_argument('--edited', type=Path, help='--case edit 生成的 edited-definition.json')
    parser.add_argument('--part', choices=('tip', 'shaft'), help='障碍复验使用的实物部位')
    parser.add_argument('--archive', type=Path, help='--case archive 的实际世界和外部核对记录目录')
    parser.add_argument('--view', choices=('native', 'worldfall'), help='录制指定实机视角')
    parser.add_argument('--move', choices=('strike', 'sweep', 'press'), help='只拍指定的正式长枪招式')
    args = parser.parse_args()
    if (args.case == 'range') != (args.edited is not None):
        parser.error('--case range 需且仅需搭配 --edited')
    if (args.case == 'obstacle') != (args.part is not None):
        parser.error('--case obstacle 需且仅需搭配 --part tip 或 --part shaft')
    if args.case in ('cold', 'film') and args.archive is None:
        parser.error('--case cold 和 --case film 都需要 --archive')
    if args.archive is not None and args.case not in ('cold', 'press', 'film'):
        parser.error('--archive 用于 cold、press 或 film')
    if (args.case == 'film') != (args.view is not None) or (args.move is not None and args.case != 'film'):
        parser.error('--case film 需要 --view，可选 --move')
    if args.edited is not None and not args.edited.is_file():
        parser.error(f'找不到已改造的正式资料：{args.edited}')
    if args.archive and not all((args.archive / name).is_file() for name in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式世界档或外部核对记录：{args.archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    output = Path(tempfile.mkdtemp(prefix='long-weapons-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    if args.case == 'film' and args.move is None:
        clips = []
        for move in ('strike', 'sweep', 'press'):
            args.move = move
            shot = output / move
            shot.mkdir()
            result = run(args, shot)
            clips.append(dict(move=move, output=str(shot), result=result))
        manifest = output / 'clips.txt'
        manifest.write_text(''.join(f'file {clip["move"]}/{args.view}/body-motion.mp4\n' for clip in clips))
        video = output / f'{args.view}.mp4'
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-f', 'concat', '-i', str(manifest), '-c', 'copy', str(video)],
                       check=True)
        result = dict(passed=True, clips=clips, video=str(video))
        write_json(output / 'result.json', result)
    else:
        result = run(args, output)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


def run(args, output):
    """用固定场景运行一段验证或从同一世界档拍一招。"""
    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    world = args.archive.resolve() / 'archive' if args.archive else PROJECT / fixture
    base = base.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(world)))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.LongSpear")')
    if args.case in ('press', 'film'):
        base = base.replace('contact.AbilityId.StartsWith("Cultiway.Body.LongSpear")',
                            'contact.AbilityId.StartsWith("Cultiway.Body.LongSpear") || contact.AbilityId == "Cultiway.Body.SwordThrust"')
    if args.case == 'film':
        base = base.replace('busy = BodyControlState.IsBusy(a.id)',
                            'busy = BodyControlState.IsBusy(a.id), '
                            'block = ActiveAbilityService.ResolveControlState(a.GetExtend(), handle).BlockReason.ToString(), '
                            'canUse = ActiveAbilityService.CanUse(a.GetExtend(), handle, choice)')
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    fighters = (scripts / 'verify_short_weapons.script').read_text()
    fighters = fighters[:fighters.index('var bindingType =')].replace('Cultiway.Body.ShortBlade', 'Cultiway.Body.LongSpear')
    if args.archive and args.case in ('press', 'film'):
        expected = json.loads((args.archive / 'expected.json').read_text())
        fighters = fighters.replace('Make(64.35f)', f'World.world.units.get({int(expected["source"])}L)')
        fighters = fighters.replace('Make(65.2f)', f'World.world.units.get({int(expected["target"])}L)')
    script = output / 'input.script'
    imports = 'using Friflo.Engine.ECS;\n'
    if args.case in ('edit', 'archive'):
        imports += 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
        helpers = (scripts / 'verify_unarmed_hand_editor.script').read_text().split('\ntry\n', 1)[0]
        fighters = helpers.replace('Cultiway.Body.PalmPush', 'Cultiway.Body.LongSpearThrust')
    elif args.case == 'cold':
        base = base.split('Game.Resume();', 1)[0]
        fighters = ''
    case_file = {
        'edit': 'verify_long_weapons_edit.script',
        'archive': 'verify_long_weapons_edit.script',
        'cold': 'verify_long_weapons_cold.script',
        'press': 'verify_long_weapons_press.script',
        'range': 'verify_long_weapons_range.script',
        'obstacle': 'verify_long_weapons_obstacle.script',
        'film': 'verify_long_weapons_film.script',
    }.get(args.case, 'verify_long_weapons.script')
    body = (scripts / case_file).read_text()
    settings = f'\nconst string selection = "{args.case}";\n'
    if args.case in ('press', 'film'):
        settings += 'const float pressDistance = 1.16f, pressDelay = .26f;\n'
    if args.edited:
        settings += f'const string editedDefinitionPath = {json.dumps(str(args.edited.resolve()))};\n'
    if args.part:
        settings += f'const string obstaclePart = "{args.part}";\n'
    if args.case == 'cold':
        settings += f'const string archived = {json.dumps(str(args.archive.resolve()))};\n'
    if args.case == 'film':
        settings += f'const string view = "{args.view}", selectedMove = "{args.move or ""}";\n'
        camera = (scripts / 'verify_unarmed_film.script').read_text()
        setup = camera.split('    foreach (var item in cases.Where', 1)[0].replace('64.73f, 72.34f', '64.98f, 72.18f')
        setup = setup.replace('Set("_godClose", 1.6f)', 'Set("_godClose", 2.15f)')
        setup = setup.replace('Set("_godCloseTarget", 1.6f)', 'Set("_godCloseTarget", 2.15f)')
        body = ('owner.E.RemoveComponent<ConstructedKnowledge>();\n' + setup + body +
                '\nfinally\n' + camera.rsplit('\nfinally\n', 1)[1])
    script.write_text(imports + base + fighters + settings + body)
    try:
        result = baseline.call('run', str(script), '260000' if args.case == 'film' else '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'长兵复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'长兵复验未通过，见 {output}')
    if args.case == 'film':
        from record_body_motion import encode

        encode(output, json.loads((output / 'recording.json').read_text()), args.view)
    return result


if __name__ == '__main__':
    main()
