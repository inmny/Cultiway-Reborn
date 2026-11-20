#!/usr/bin/env python3
"""用独立游戏核对雷神凿世的显形、锤面和真实撞地。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--view', choices=('native', 'worldfall'), default='worldfall')
    parser.add_argument('--section', choices=('swing', 'contrast', 'eruption', 'guard', 'permit', 'save', 'film'), default='swing')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'thunder-earthbreaker-{args.section}-{args.view}-',
                                  dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/雷神凿世.json', folder / 'definition.json')
    if args.section == 'save':
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/雷神凿世·偏雷.json', folder / 'variant.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.ThunderGodEarthbreaker")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    helpers = helpers.replace("new ActiveAbilityTarget(null, aim.Value)",
                              "new ActiveAbilityTarget(null, aim.Value, worldScope: scope)")
    camera = (scripts / 'verify_wind_combat.script').read_text()
    film_at = camera.index('async Task<List<double>> Film')
    film = camera[film_at:camera.index('\n}\n', film_at) + 3]
    camera = (camera[camera.index('object cameraSettings'):camera.index('void ReleaseChecks()')] +
              camera[camera.index('async Task Shot('):camera.index('async Task<List<double>> Film')])
    camera = camera.replace('67.7f, 72f', '68.5f, 73.9f').replace(
        '    Screen.Capture(Path.Combine(folder, name + ".png"));\n    await Game.NextFrame();',
        '    string path = Path.Combine(folder, name + ".png");\n    Screen.Capture(path);\n'
        '    await Game.WaitUntil(() => File.Exists(path), 300);')
    worldfall = (scripts / 'verify_hanging_tree_delivery.script').read_text()
    start = worldfall.index('Type WorldfallType()')
    end = worldfall.index('\n}\n', worldfall.index('async Task WaitForWorldfallDraw()')) + 3
    script = folder / 'input.script'
    script.write_text(helpers + f'\nconst string recordView = "{args.view}";\n' +
                      f'const string section = "{args.section}";\n' + camera + film + worldfall[start:end] +
                      (scripts / 'verify_thunder_earthbreaker.script').read_text() +
                      (scripts / 'verify_thunder_earthbreaker_eruption.script').read_text() +
                      (scripts / 'verify_thunder_earthbreaker_save.script').read_text() +
                      (scripts / 'verify_thunder_earthbreaker_film.script').read_text())
    try:
        result = baseline.call('run', str(script), '190000' if args.section == 'save' else '145000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'雷神凿世未通过：{folder}') from error
    write_json(folder / 'result.json', result)
    if result.get('passed') and args.section == 'film':
        encode(folder.parent, dict(key=folder.name, name='雷神凿世', label='正式施放', variant=1,
                                  trajectory='static', modifiers=[], caption='巨神举锤、真实撞地、裂层碎岩和喷雷'), keep_frames=True)
    print(json.dumps({'output': str(folder), 'result': result}, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('雷神凿世未通过')


if __name__ == '__main__':
    main()
