#!/usr/bin/env python3
"""在独立游戏里复验寸掌葬岳的命中、岩片落稳、地表断路与归还。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('rocks', 'save', 'native', 'worldfall'), default='rocks')
    section = parser.parse_args().section
    view = section if section in ('native', 'worldfall') else ''
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'bury-mountain-palm-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    documents = PROJECT / 'Doc/技能系统全面重构/构造输入'
    shutil.copy2(documents / '寸掌葬岳.json', folder / 'definition.json')
    if section != 'rocks':
        shutil.copy2(documents / '寸掌葬岳·横断.json', folder / 'variant.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.BuryMountainPalm", StringComparison.Ordinal)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                            'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                            'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    body = (scripts / 'verify_bury_mountain_palm.script').read_text()
    if section != 'rocks':
        camera = (scripts / 'verify_wind_combat.script').read_text().split('object cameraSettings', 1)[1]
        camera = 'object cameraSettings' + camera
        camera = camera[:camera.index('void ReleaseChecks()')] + camera[camera.index('async Task SetCamera('):]
        body = body.split('AbilityCast Live()', 1)[0] + body[body.index('object Trace()'):body.index('\ntry\n')]
        body += f'\nconst string recordView = {json.dumps(view)};\n' + camera
        body += '\n' + (scripts / 'verify_bury_mountain_palm_delivery.script').read_text()
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\n' + helpers + '\n' + body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'寸掌葬岳 {section} 未通过：{folder}')
    if view:
        encode(folder.parent, dict(key=folder.name, name='寸掌葬岳：掌击、岩层推进与回落', label='原速',
                                   variant=0, trajectory='', modifiers=[], caption='实际命中后才沿地面传播'), keep_frames=True)
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
