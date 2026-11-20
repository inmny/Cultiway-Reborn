#!/usr/bin/env python3
"""复验同一根段与刃片怎样分别变为飞刃和地面屏障。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('blade', 'screen', 'miss', 'drop', 'editor'), required=True)
    parser.add_argument('--record', choices=('native', 'worldfall'))
    parser.add_argument('--save', action='store_true', help='保存坠锋结束后的世界')
    args = parser.parse_args()
    if args.record and args.section in ('miss', 'editor'):
        parser.error('飞空和编辑场景不需要连续录像')
    if args.save and args.section != 'drop':
        parser.error('正式存档使用坠锋场景')
    if not os.getenv('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix=f'reused-parts-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helpers = helpers.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Demo.")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    shared = (scripts / 'verify_wind_combat.script').read_text()
    shared = shared.replace('const string inputRoot = "";', f'const string inputRoot = {json.dumps(str(PROJECT / "Doc/技能系统全面重构/构造输入"))};')
    shared = shared.replace('const string recordView = "";', f'const string recordView = "{args.record or ""}";')
    shared = shared.replace('if (fact.SourceId != source.id) return;',
                            'if (fact.SourceId != source.id && fact.SourceId != victim.id) return;')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.Wind.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")')
    five = (scripts / 'verify_five_phase_district.script').read_text()
    probe = five[five.index('AbilityCast Send('):five.index('var owner =')]
    probe = probe.replace('Cultiway.Demo.FivePhaseDistrict.Probe.', 'Cultiway.Demo.ReusedParts.Probe.')
    imports = 'using Friflo.Engine.ECS;\n'
    if args.section == 'editor':
        imports += 'using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n'
        editor = (scripts / 'verify_five_phases_editor.script').read_text().split('\ntry\n', 1)[0]
        editor = editor.replace('Spell("五相周流")', 'Spell("坠锋留材")')
        editor = editor.replace('Row("ActivationTrigger").transform', 'Row("KeepSourcePosition").transform')
        editor = editor.replace('Shot("handoff-"', 'Shot("reused-parts-"')
        scenario = editor + '\n' + (scripts / 'verify_reused_parts_editor.script').read_text()
    else:
        flags = f'const string section = "{args.section}";\nconst bool save = {str(args.save).lower()};'
        scenario = '\n'.join([probe, flags, Path(__file__).with_suffix('.script').read_text()])
    path = output / 'input.script'
    path.write_text('\n'.join([imports + helpers, shared, scenario]))
    try:
        result = baseline.call('run', str(path), '145000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'共用部件复验位于 {output}') from error
    write_json(output / 'result.json', result)
    if args.record and result.get('passed'):
        encode(output.parent, dict(key=output.name, name=result['name'], label='正式施放', variant=1,
                                   trajectory='', modifiers=[], caption=result['caption']), keep_frames=True)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('共用部件复验未通过')


if __name__ == '__main__':
    main()
