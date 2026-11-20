#!/usr/bin/env python3
"""复用固定世界，核对山河叩首的刚性翻落、触地和收尾。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('ground', 'raised', 'interrupted'), default='ground')
    parser.add_argument('--view', choices=('native', 'worldfall'), default='native')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'bowing-mountains-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/山河叩首.json', folder / 'definition.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.BowingMountains", StringComparison.Ordinal)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    views = (scripts / 'verify_wind_combat.script').read_text()
    views = views[views.index('object cameraSettings'):views.index('async Task<List<double>> Film')]
    views = views[:views.index('void ReleaseChecks()')] + views[views.index('async Task Shot('):]
    views = views.replace('67.7f, 72f', '71.5f, 73f').replace(
        '    Screen.Capture(Path.Combine(folder, name + ".png"));\n    await Game.NextFrame();',
        '    string path = Path.Combine(folder, name + ".png");\n    Screen.Capture(path);\n'
        '    await Game.WaitUntil(() => File.Exists(path), 300);')
    worldfall = (scripts / 'verify_hanging_tree_delivery.script').read_text()
    start = worldfall.index('Type WorldfallType()')
    worldfall = worldfall[start:worldfall.index('\n}\n', worldfall.index('async Task WaitForWorldfallDraw()')) + 3]
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\n' + helpers + f'\nconst string recordView = {json.dumps(args.view)};\n' +
                      f'const string section = {json.dumps(args.section)};\n' + views + worldfall + (scripts / 'verify_bowing_mountains.script').read_text())
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'山河叩首脚本未能运行：{folder}') from error
    write_json(folder / 'result.json', result)
    print(json.dumps({'output': str(folder), 'result': result}, ensure_ascii=False))
    if not result.get('passed'):
        raise SystemExit(f'山河叩首未通过：{folder}')


if __name__ == '__main__':
    main()
