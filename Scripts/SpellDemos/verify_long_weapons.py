#!/usr/bin/env python3
"""用原版铁枪复验滑握、枪尖和杆身各自的实际作用及完整回收。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('all', 'strike', 'near', 'sweep', 'interrupt', 'press', 'edit', 'range',
                                           'obstacle', 'archive', 'cold'), default='all')
    parser.add_argument('--edited', type=Path, help='--case edit 生成的 edited-definition.json')
    parser.add_argument('--part', choices=('tip', 'shaft'), help='障碍复验使用的实物部位')
    parser.add_argument('--archive', type=Path, help='--case archive 的实际世界和外部核对记录目录')
    args = parser.parse_args()
    if (args.case == 'range') != (args.edited is not None):
        parser.error('--case range 需且仅需搭配 --edited')
    if (args.case == 'obstacle') != (args.part is not None):
        parser.error('--case obstacle 需且仅需搭配 --part tip 或 --part shaft')
    if (args.case == 'cold') != (args.archive is not None):
        parser.error('--case cold 需且仅需搭配 --archive')
    if args.edited is not None and not args.edited.is_file():
        parser.error(f'找不到已改造的正式资料：{args.edited}')
    if args.archive and not all((args.archive / name).is_file() for name in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式世界档或外部核对记录：{args.archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix='long-weapons-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    base = base.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(PROJECT / fixture)))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.LongSpear")')
    if args.case == 'press':
        base = base.replace('contact.AbilityId.StartsWith("Cultiway.Body.LongSpear")',
                            'contact.AbilityId.StartsWith("Cultiway.Body.LongSpear") || contact.AbilityId == "Cultiway.Body.SwordThrust"')
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    fighters = (scripts / 'verify_short_weapons.script').read_text()
    fighters = fighters[:fighters.index('var bindingType =')].replace('Cultiway.Body.ShortBlade', 'Cultiway.Body.LongSpear')
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
    }.get(args.case, 'verify_long_weapons.script')
    settings = f'\nconst string selection = "{args.case}";\n'
    if args.edited:
        settings += f'const string editedDefinitionPath = {json.dumps(str(args.edited.resolve()))};\n'
    if args.part:
        settings += f'const string obstaclePart = "{args.part}";\n'
    if args.archive:
        settings += f'const string archived = {json.dumps(str(args.archive.resolve()))};\n'
    script.write_text(imports + base + fighters + settings +
                      (scripts / case_file).read_text())
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'长兵复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'长兵复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
