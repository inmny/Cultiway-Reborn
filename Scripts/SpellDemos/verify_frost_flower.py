#!/usr/bin/env python3
"""在现有世界中检查霜花葬形的地面薄霜。"""
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    folder = Path(tempfile.mkdtemp(prefix='frost-flower-ground-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world')
    shutil.copy2(PROJECT / 'Doc/技能系统全面重构/构造输入/霜花葬形.json', folder / 'definition.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('.StartsWith("test.body-footwork.")', ' == "Cultiway.Demo.FrostFlowerBurial"')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    camera = (scripts / 'verify_wind_combat.script').read_text()
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
    script.write_text(helpers + '\nconst string recordView = "native";\n' + camera +
                      (scripts / 'verify_frost_flower.script').read_text())
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'薄霜未通过：{folder}') from error
    write_json(folder / 'result.json', result)
    print(json.dumps({'output': str(folder), 'result': result}, ensure_ascii=False), flush=True)
    if not result.get('passed'):
        raise SystemExit('薄霜未通过')


if __name__ == '__main__':
    main()
