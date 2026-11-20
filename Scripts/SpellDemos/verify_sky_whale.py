#!/usr/bin/env python3
"""复用固定世界，检查天鲸的真实起跃、托举和收尾。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('breach', 'spill', 'cutoff', 'stones', 'impact', 'save', 'native', 'worldfall'), default='breach')
    parser.add_argument('--resume-save', type=Path, help='只用上次输出目录中的正式世界复验重开后改角复放')
    args = parser.parse_args()
    section = args.section
    if args.resume_save and section != 'save':
        parser.error('--resume-save 仅用于 --section save')
    view = section if section in ('native', 'worldfall') else 'native'
    filming = section in ('native', 'worldfall')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'sky-whale-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(args.resume_save / 'world' if args.resume_save else PROJECT / fixture, folder / 'input-world')
    if args.resume_save:
        for name in ('definition.json', 'variant.json', 'replay.json'):
            shutil.copy2(args.resume_save / name, folder / name)
    else:
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/天鲸覆城.json', folder / 'definition.json')
    if section == 'save' and not args.resume_save:
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/天鲸覆城·逆卷.json', folder / 'variant.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.SkyWhale", StringComparison.Ordinal)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    if args.resume_save:
        helpers = helpers.replace('foreach (Actor a in World.world.units.getSimpleList().ToArray())',
                                  'foreach (Actor a in Array.Empty<Actor>())')
    camera = 'object cameraSettings' + (scripts / 'verify_wind_combat.script').read_text().split('object cameraSettings', 1)[1]
    camera = camera[:camera.index('void ReleaseChecks()')] + camera[camera.index('async Task SetCamera('):]
    camera = camera.replace('67.7f, 72f', '72.5f, 74f')
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\n' + helpers + f'\nconst string recordView = {json.dumps(view)};\n' +
                      f'const string section = {json.dumps(section)};\n' + camera +
                      (scripts / 'verify_sky_whale.script').read_text().replace(
                          '// 翻身和垂流。', (scripts / 'verify_sky_whale_spill.script').read_text() +
                          (scripts / 'verify_sky_whale_stones.script').read_text() +
                          (scripts / 'verify_sky_whale_landing.script').read_text()))
    try:
        result = baseline.call('run', str(script), '225000' if section == 'save' and not args.resume_save else '150000' if filming else '90000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'天鲸覆城 {section} 未通过：{folder}')
    if filming:
        encode(folder.parent, dict(key=folder.name, name='天鲸覆城：起跃、卷石、翻身、垂流与覆压', label='原速',
                                   variant=0, trajectory='', modifiers=[], caption='撞散后余水沿真实地形奔流'), keep_frames=True)
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
