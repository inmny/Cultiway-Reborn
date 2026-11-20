#!/usr/bin/env python3
"""用重兵对练档复验正式盾架的方向、时段、余力和打造。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('front', 'rear', 'late', 'overflow', 'craft'), required=True)
    parser.add_argument('--archive', type=Path, required=True, help='重兵复验的正式对练档目录')
    args = parser.parse_args()
    archive = args.archive.resolve()
    if not all((archive / file).is_file() for file in ('archive/map.wbox', 'expected.json')):
        parser.error(f'缺少正式对练档或人物核对资料：{archive}')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    output = Path(tempfile.mkdtemp(prefix='defense-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    base = base.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                        json.dumps(str(archive / 'archive')))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.Body.")')
    base = base.replace('var hold = Cultiway.ModClass.L.StatusEffectLibrary.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    expected = json.loads((archive / 'expected.json').read_text())
    fighters = (scripts / 'verify_short_weapons.script').read_text().split('var bindingType =', 1)[0]
    fighters = fighters.replace('Make(64.35f)', f'World.world.units.get({int(expected["source"])}L)')
    fighters = fighters.replace('Make(65.2f)', f'World.world.units.get({int(expected["target"])}L)')
    body = 'verify_defense_craft.script' if args.case == 'craft' else 'verify_defense.script'
    script = output / 'input.script'
    imports = 'using Friflo.Engine.ECS;\n'
    if args.case == 'craft':
        imports += 'using Cultiway.UI;\nusing UnityEngine.UI;\n'
    script.write_text(imports + base + fighters +
                      f'\nconst string selection = "{args.case}";\n' + (scripts / body).read_text())
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'防御战技复验见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'防御战技复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
