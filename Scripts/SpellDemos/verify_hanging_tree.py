#!/usr/bin/env python3
"""复用固定世界，检查森罗倒生的实际生长、钉地和清场。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('growth', 'short', 'grip', 'cut', 'escape', 'source-loss', 'ungrounded', 'save', 'native', 'worldfall'), default='growth')
    parser.add_argument('--view', choices=('native', 'worldfall'), default='worldfall')
    parser.add_argument('--city', action='store_true', help='在原版场地加城区并比较边界两侧的抓持')
    args = parser.parse_args()
    if args.city and args.section != 'native':
        parser.error('--city 只用于原版主术录像')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'hanging-tree-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/森罗倒生.json', folder / 'definition.json')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/森罗倒生·偏庭.json', folder / 'variant.json')
    filming = args.section in ('native', 'worldfall')
    view = args.section if filming else args.view
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.HangingTree", StringComparison.Ordinal)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    views = (scripts / 'verify_wind_combat.script').read_text()
    start = views.index('async Task<List<double>> Film')
    film = views[start:views.index('\n}\n', start) + 3]
    views = views[views.index('object cameraSettings'):views.index('async Task<List<double>> Film')]
    views = views[:views.index('void ReleaseChecks()')] + views[views.index('async Task Shot('):]
    views = views.replace('67.7f, 72f', '74f, 74f' if args.city else '73.5f, 74f' if filming and view == 'native' else '73.5f, 72.5f').replace(
        '    Screen.Capture(Path.Combine(folder, name + ".png"));\n    await Game.NextFrame();',
        '    string path = Path.Combine(folder, name + ".png");\n    Screen.Capture(path);\n'
        '    await Game.WaitUntil(() => File.Exists(path), 300);')
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\n' + helpers + f'\nconst string recordView = {json.dumps(view)};\n' +
                      f'const string section = {json.dumps(args.section)};\nbool cityFilm = {str(args.city).lower()};\n' + views +
                      (scripts / 'city_film_scene.script').read_text() +
                      (scripts / 'verify_hanging_tree_grip.script').read_text() +
                      film + (scripts / 'verify_hanging_tree.script').read_text() +
                      (scripts / 'verify_hanging_tree_delivery.script').read_text())
    try:
        result = baseline.call('run', str(script), '215000' if args.section == 'save' else '145000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'森罗倒生 {args.section} 未通过：{folder}')
    if filming:
        encode(folder.parent, dict(key=folder.name, name='森罗倒生', label='城区实录' if args.city else '正式施放', variant=1,
                                  trajectory='static', modifiers=[], caption='扎根、抓持、松脱、收回'), keep_frames=True)
    print(json.dumps({'output': str(folder), 'result': result}, ensure_ascii=False))


if __name__ == '__main__':
    main()
