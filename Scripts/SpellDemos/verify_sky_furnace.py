#!/usr/bin/env python3
"""用同一正式施放检查天炉的出口与落地分流。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('launch', 'upright', 'visual', 'cooling', 'cutoff', 'damage', 'save', 'native', 'worldfall', 'all'), default='all')
    parser.add_argument('--city', action='store_true', help='在原版场地加城区并记录边界两侧的熔料')
    args = parser.parse_args()
    section = args.section
    if args.city and section != 'native':
        parser.error('--city 只用于原版主术录像')
    view = section if section in ('native', 'worldfall') else ''
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'sky-furnace-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/天炉倾海.json', folder / 'definition.json')
    if section == 'save':
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/天炉倾海·偏口.json', folder / 'variant.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.SkyFurnace", StringComparison.Ordinal)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    script = folder / 'input.script'
    body = (scripts / 'verify_sky_furnace.script').read_text()
    thermal = (scripts / 'verify_sky_furnace_cooling.script').read_text()
    camera = 'object cameraSettings' + (scripts / 'verify_wind_combat.script').read_text().split('object cameraSettings', 1)[1]
    camera = camera[:camera.index('void ReleaseChecks()')] + camera[camera.index('async Task SetCamera('):]
    camera = camera.replace('67.7f, 72f', '71f, 73f' if args.city else '69.5f, 72.6f')
    delivery = (scripts / 'verify_sky_furnace_delivery.script').read_text()
    code = (f'const string recordView = {json.dumps(view)};\nbool cityFilm = {str(args.city).lower()};\n'
            + thermal + camera + (scripts / 'city_film_scene.script').read_text() + delivery)
    script.write_text('using Cultiway.Const;\n' + helpers + f'\nconst string section = {json.dumps(section)};\n' +
                      body.replace('// 实机入口。', code))
    try:
        result = baseline.call('run', str(script), '145000' if view else '90000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'天炉倾海 {section} 未通过：{folder}')
    if view:
        encode(folder.parent, dict(key=folder.name, name='天炉倾海：出炉、分流、凝壳与破口', label='城区实录' if args.city else '原速',
                                   variant=0, trajectory='', modifiers=[], caption='余焰随熔料的热量熄灭'), keep_frames=True)
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
