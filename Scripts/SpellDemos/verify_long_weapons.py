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
    parser.add_argument('--case', choices=('all', 'strike', 'near', 'sweep'), default='all')
    args = parser.parse_args()
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
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    fighters = (scripts / 'verify_short_weapons.script').read_text()
    fighters = fighters[:fighters.index('var bindingType =')].replace('Cultiway.Body.ShortBlade', 'Cultiway.Body.LongSpear')
    script = output / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\n' + base + fighters +
                      f'\nconst string selection = "{args.case}";\n' + Path(__file__).with_suffix('.script').read_text())
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
