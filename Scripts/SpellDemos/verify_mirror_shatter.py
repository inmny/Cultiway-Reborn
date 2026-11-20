#!/usr/bin/env python3
"""在独立游戏里核对镜中碎身的原身、镜口和退场。"""
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix='mirror-shatter-image-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.MirrorShatter")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    shared = (scripts / 'verify_wind_combat.script').read_text()
    shared = shared.replace('const string inputRoot = "";',
                            'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    shared = shared.replace('var mana = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.Wind.Mana")',
                            'var mana = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.MirrorShatter.Mana") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild("Cultiway.Verify.MirrorShatter.Mana")')
    script = output / 'input.script'
    script.write_text(helpers + '\n' + shared + '\n' + (scripts / 'verify_mirror_shatter_image.script').read_text())
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'镜中碎身复验结果位于 {output}') from error
    write_json(output / 'result.json', result)
    print(json.dumps({'output': str(output), 'result': result}, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('镜中碎身未通过')


if __name__ == '__main__':
    main()
