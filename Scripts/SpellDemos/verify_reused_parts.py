#!/usr/bin/env python3
"""复验同一根段与刃片怎样分别变为飞刃和地面屏障。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('blade', 'screen', 'miss'), required=True)
    args = parser.parse_args()
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
    shared = shared.replace('if (fact.SourceId != source.id) return;',
                            'if (fact.SourceId != source.id && fact.SourceId != victim.id) return;')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.Wind.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")')
    five = (scripts / 'verify_five_phase_district.script').read_text()
    probe = five[five.index('AbilityCast Send('):five.index('var owner =')]
    probe = probe.replace('Cultiway.Demo.FivePhaseDistrict.Probe.', 'Cultiway.Demo.ReusedParts.Probe.')
    path = output / 'input.script'
    path.write_text('\n'.join(['using Friflo.Engine.ECS;\n' + helpers, shared, probe,
                               f'const string section = "{args.section}";', Path(__file__).with_suffix('.script').read_text()]))
    try:
        result = baseline.call('run', str(path), '145000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'共用部件复验位于 {output}') from error
    write_json(output / 'result.json', result)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('共用部件复验未通过')


if __name__ == '__main__':
    main()
