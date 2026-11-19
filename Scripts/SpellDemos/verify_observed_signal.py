#!/usr/bin/env python3
"""从亲历交战发出方位，验证旧位置、仅指定收件与到期。"""
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过 dev.py exec 运行')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix='observed-signal-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', output / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/照影传讯.json', output / 'definition.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    script = output / 'input.script'
    script.write_text('using Cultiway.Core.Combat.Tactical;\n' + helpers + '\n' + (scripts / 'verify_observed_signal.script').read_text())
    try:
        result = baseline.call('run', str(script), '120000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'照影传讯验证未通过：{output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
