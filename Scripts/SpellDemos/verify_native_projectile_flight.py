#!/usr/bin/env python3
"""核对御器离手、真实接触、原版弹丸和双视角共用的实际飞行。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    melee = {'melee': ('spear_iron', 'MeleeThrust'),
             'melee-sweep': ('sword_iron', 'MeleeSweep'),
             'melee-crush': ('hammer_iron', 'MeleeCrush')}
    artifacts = ('artifact-sword', 'artifact-array', 'artifact-array-recall')
    parser.add_argument('--section', choices=('projectiles', 'bow', 'bow-world', *melee, *artifacts),
                        default='projectiles')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'native-projectile-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    base = base.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(PROJECT / fixture)))
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    program = output / 'input.script'
    imports = ''
    body = Path(__file__).with_suffix('.script').read_text()
    if args.section in ('bow', 'bow-world'):
        world_only = str(args.section == 'bow-world').lower()
        body = (body.split('\ntry\n{', 1)[0] + f'\nconst bool worldOnly = {world_only};\n'
                + (scripts / 'verify_ranged_weapon_return.script').read_text())
    if args.section in melee:
        item, mode = melee[args.section]
        body = f'const string itemId = "{item}", expectedMode = "{mode}";\n'
        body += (scripts / 'verify_melee_weapon_return.script').read_text()
    if args.section in artifacts:
        imports = ("using Cultiway.Content.Artifacts;\nusing Cultiway.Content.Artifacts.Baibao;\n"
                   "using Cultiway.Content.Extensions;\nusing Cultiway.Content.Libraries;\n"
                   "using Cultiway.Core.Components;\n")
        if args.section == 'artifact-sword':
            body = (scripts / 'verify_artifact_sword_flight.script').read_text()
        else:
            recall = str(args.section == 'artifact-array-recall').lower()
            body = f'bool recallOnly = {recall};\n' + (scripts / 'verify_artifact_sword_array.script').read_text()
    program.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.SkillLibV3;\n'
                       'using Cultiway.Core.SkillLibV3.Components;\n' + imports + base + body)
    try:
        result = baseline.call('run', str(program), '150000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'御器飞行复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'御器飞行复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
