#!/usr/bin/env python3
"""用独立游戏和同一存档核对折景门的真实取景、通行及关门。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('view', 'blocked', 'visual'), default='view')
    parser.add_argument('--view', choices=('native', 'worldfall'), default='native')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    name = f'fold-gate-visual-{args.view}' if args.section == 'visual' else f'fold-gate-{args.section}'
    folder = Path(tempfile.mkdtemp(prefix=f'{name}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world')
    files = Path(__file__).parent
    helpers = (files / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    body = (files / 'verify_fold_gate.script').read_text()
    if args.section == 'blocked':
        helpers = helpers.replace('test.body-footwork.', 'test.spatial-mapping.')
        body = body.split('// ACTOR', 1)[1]
        staging = (files / 'verify_spatial_mapping_staging.script').read_text()
        at, end = staging.index('    SetExit(false);'), staging.index('    SetExit(true);\n    AbilityCast closes')
        staging = staging[:at] + staging[end:]
        at, end = staging.index('    await Advance(.8f);'), staging.index('    return new { passed')
        staging = staging[:at] + '    Check(NoDestination(), "出口关闭即撤去对端地貌");\n' + staging[end:]
        staging = staging.replace('    await Advance(.20f);', '''    Config.paused = false;
    await Game.WaitUntil(() => !Held() && Boundary(), 1800);
    Config.paused = true;''', 1)
        body += staging.replace('Form = "fireball"', 'Form = "space_fold_gate"').replace('    Waiting(closes);',
                            '    Waiting(closes);\n    Check(!NoDestination(), "受阻地貌仍来自实际出口");')
    else:
        helpers = helpers.replace('contact.AbilityId.StartsWith("test.body-footwork.")', 'contact.AbilityId == "Cultiway.Demo.FoldGate"')
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/折景渡物.json', folder / 'definition.json')
        body = body.split('// ACTOR', 1)[0]
        if args.section == 'visual':
            body = f'const string view = "{args.view}";\n' + body.split('// CHECKS', 1)[0] + (files / 'verify_fold_gate_visual.script').read_text()
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + body)
    try:
        result = baseline.call('run', str(script), '120000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'折景门未通过：{folder}')
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
