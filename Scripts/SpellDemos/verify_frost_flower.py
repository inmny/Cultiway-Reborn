#!/usr/bin/env python3
"""在同一现有世界检查霜花葬形的薄霜、附着、围合与碎裂。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, encode, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('ground', 'coating', 'enclosure', 'bloom', 'save', 'film'), default='ground')
    parser.add_argument('--enclosure-part', choices=('all', 'contrast'), default='all')
    parser.add_argument('--view', choices=('native', 'worldfall'), default='native')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix=f'frost-flower-{args.section}-{args.view}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/霜花葬形.json', folder / 'definition.json')
    if args.section == 'save':
        shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/霜花葬形·狭瓣.json', folder / 'variant.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', '.StartsWith("Cultiway.Demo.FrostFlowerBurial")')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    camera = (scripts / 'verify_wind_combat.script').read_text()
    film_at = camera.index('async Task<List<double>> Film')
    film = camera[film_at:camera.index('\n}\n', film_at) + 3]
    camera = (camera[camera.index('object cameraSettings'):camera.index('void ReleaseChecks()')] +
              camera[camera.index('async Task Shot('):camera.index('async Task<List<double>> Film')])
    # Capture calls reuse the camera setup without changing the already frozen test ground.
    camera = camera.replace('async Task SetCamera(float zoom)', 'async Task SetCamera(float zoom, bool clearScenery)')
    camera = camera.replace('    var scenery = new HashSet<Building>();',
                            '    if (clearScenery) {\n    var scenery = new HashSet<Building>();')
    camera = camera.replace('building.removeBuildingFinal();', 'building.removeBuildingFinal();\n    }')
    camera = camera.replace('67.7f, 72f', '68.5f, 72.5f').replace(
        '    Screen.Capture(Path.Combine(folder, name + ".png"));\n    await Game.NextFrame();',
        '    string path = Path.Combine(folder, name + ".png");\n    Screen.Capture(path);\n'
        '    await Game.WaitUntil(() => File.Exists(path), 300);')
    script = folder / 'input.script'
    verifier = {'ground': 'verify_frost_flower.script', 'coating': 'verify_frost_flower_coatings.script',
                'enclosure': 'verify_frost_flower_enclosure.script',
                'bloom': 'verify_frost_flower_bloom.script', 'save': 'verify_frost_flower_save.script', 'film': 'verify_frost_flower_film.script'}[args.section]
    body = (scripts / verifier).read_text()
    if args.section == 'film':
        worldfall = (scripts / 'verify_hanging_tree_delivery.script').read_text()
        start = worldfall.index('Type WorldfallType()')
        end = worldfall.index('\n}\n', worldfall.index('async Task WaitForWorldfallDraw()')) + 3
        camera += film + worldfall[start:end]
        setup = (scripts / 'verify_frost_flower_bloom.script').read_text()
        before, setup = setup.split('\ntry\n{', 1)
        body = before + '\n{\n' + setup.split('    var hitCast =', 1)[0] + '}\n' + (scripts / 'verify_frost_flower_film.script').read_text()
    script.write_text(helpers + f'\nconst string recordView = "{args.view}";\n' +
                      f'const string enclosurePart = "{args.enclosure_part}";\n' + camera +
                      body)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'霜花葬形未通过：{folder}') from error
    write_json(folder / 'result.json', result)
    if result.get('passed') and args.section == 'film':
        encode(folder.parent, dict(key=folder.name, name='霜花葬形', label='正式施放', variant=1,
                                  trajectory='static', modifiers=[], caption='贴地薄霜、冰瓣闭合、盛放碎裂'), keep_frames=True)
    print(json.dumps({'output': str(folder), 'result': result}, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('霜花葬形未通过')


if __name__ == '__main__':
    main()
