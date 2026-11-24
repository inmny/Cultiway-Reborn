#!/usr/bin/env python3
"""沿用原锤盾对练档和狼足样本，记录身体推进及真实形状求交的批量开销。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import write_json


def relation_script(scripts, folder):
    """复用已有狼足抓持验证，先对比相同的正反表面求交。"""
    helpers = (scripts / 'verify_body_organs.script').read_text().split('var editor =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-organs")', json.dumps(str(folder)))
    helpers = helpers.replace('var service = typeof(ConstructedAbilityService);',
                              'bool spatial = false;\nvar service = typeof(ConstructedAbilityService);')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    setup = (scripts / 'verify_body_joint.script').read_text().split('ConstructedAbilityService.CastEnded += Ended;', 1)[0]
    config, body = (scripts / 'verify_joint_connections.script').read_text().split('// CONNECTION_READY', 1)
    setup = setup.replace('var plan = AbilityCompiler.Compile(definition, false);',
                          config + '\nvar plan = AbilityCompiler.Compile(definition, false);')
    capture = (scripts / 'verify_joint_handoff.script').read_text().split('ConstructedAbilityService.CastEnded += Ended;', 1)[0]
    benchmark = (scripts / 'benchmark_body_surface_batch.script').read_text()
    body = body.replace('var firstPose =', benchmark + '\nvar firstPose =', 1)
    return ('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\nusing Cultiway.Core.Persistence;\n'
            + helpers + '\ntry\n{\n' + setup + capture + body
            + '\n}\nfinally\n{\nConstructedAbilityService.ContactConfirmed -= Hit;\n}\n')


def contact_script(scripts, folder, archive):
    expected = json.loads((archive / 'expected.json').read_text())
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    base = base.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                        json.dumps(str(archive / 'archive')))
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    equip = (scripts / 'verify_short_weapons.script').read_text().split('Item Equip(', 1)[1].split('var bindingType', 1)[0]
    joint = (scripts / 'verify_body_joint.script').read_text().split('var definition =', 1)[1].split('var plan =', 1)[0]
    fighters = f'var source = World.world.units.get({int(expected["source"])}L);\n' \
               f'var target = World.world.units.get({int(expected["target"])}L);\n'
    return base + fighters + 'Item Equip(' + equip + 'var wolfMove =' + joint + Path(__file__).with_suffix('.script').read_text()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group(required=True)
    mode.add_argument('--archive', type=Path, help='原锤盾对练档目录')
    mode.add_argument('--relations', action='store_true', help='沿用原狼足抓持世界，核对正反表面及双方归还')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 dev.py exec 在独立游戏内运行')
    archive = args.archive.resolve() if args.archive else None
    if archive and not all((archive / name).is_file() for name in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少原对练档和人物编号：{archive}')

    import baseline_scenarios as baseline
    scripts = Path(__file__).parent
    part = 'body-relation-batch' if args.relations else 'body-contact-batch'
    folder = Path(tempfile.mkdtemp(prefix=part + '-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = folder / 'input.script'
    script.write_text(relation_script(scripts, folder) if args.relations else contact_script(scripts, folder, archive))
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '130000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'身体批量测量未完成，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'身体批量测量未通过，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
