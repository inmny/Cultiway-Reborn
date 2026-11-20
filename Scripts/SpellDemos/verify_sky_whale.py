#!/usr/bin/env python3
"""复用固定世界，检查天鲸的真实起跃、托举和收尾。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('breach', 'spill', 'cutoff', 'stones'), default='breach')
    section = parser.parse_args().section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'sky-whale-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/天鲸覆城.json', folder / 'definition.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.SkyWhale", StringComparison.Ordinal)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    camera = 'object cameraSettings' + (scripts / 'verify_wind_combat.script').read_text().split('object cameraSettings', 1)[1]
    camera = camera[:camera.index('void ReleaseChecks()')] + camera[camera.index('async Task SetCamera('):camera.index('async Task<List<double>> Film(')]
    camera = camera.replace('67.7f, 72f', '72.5f, 74f')
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\n' + helpers + '\nconst string recordView = "native";\n' +
                      f'const string section = {json.dumps(section)};\n' + camera +
                      (scripts / 'verify_sky_whale.script').read_text().replace(
                          '// 翻身和垂流。', (scripts / 'verify_sky_whale_spill.script').read_text() +
                          (scripts / 'verify_sky_whale_stones.script').read_text()))
    try:
        result = baseline.call('run', str(script), '90000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'天鲸覆城 {section} 未通过：{folder}')
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
