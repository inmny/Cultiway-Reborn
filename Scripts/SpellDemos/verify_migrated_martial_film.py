#!/usr/bin/env python3
"""录制原地掌击与九项原骑士战技的连续实机画面。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from record_body_motion import encode


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--view', choices=('native', 'worldfall'), required=True)
    parser.add_argument('--move', action='append', choices=('palm', 'thrust', 'committed', 'guard', 'counter',
                                                           'flurry', 'repulse', 'bulwark', 'charge', 'skyfall'))
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix=f'migrated-martial-{args.view}-',
                                   dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    scripts = Path(__file__).parent
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    base = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    base = base.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    base = base.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(PROJECT / fixture)))
    base = base.replace('StartsWith("test.body-footwork.")', 'StartsWith("Cultiway.")')
    base = base.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                        'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                        'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    actors = (scripts / 'verify_short_weapons.script').read_text().split('var bindingType =', 1)[0]
    camera = (scripts / 'verify_unarmed_film.script').read_text()
    setup = camera.split('    foreach (var item in cases.Where', 1)[0]
    setup = setup.replace('    ControllableUnit.clear(false);\n', '')
    setup = setup.replace('camera.orthographicSize = 1.65f;', 'camera.orthographicSize = shotZoom;')
    setup = setup.replace('camera.targetTexture = nativeTarget;', 'camera.transform.position = shotPosition;\n'
                          '            camera.targetTexture = nativeTarget;')
    setup = setup.replace('threeDimensional ? 59 : 61', '59').replace('(threeDimensional ? 75 : 68)', '76')
    setup = setup.replace('threeDimensional ? 66 : 69', '67').replace('(threeDimensional ? 79 : 76)', '79')
    setup = setup.replace('time = World.world.getCurWorldTime(), ability',
                          'time = World.world.getCurWorldTime(), ability, '
                          'sourceX = source.current_position.x, sourceY = source.current_position.y, '
                          'sourceHeight = source.position_height')
    cleanup = '\nfinally\n' + camera.rsplit('\nfinally\n', 1)[1]
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Content.KnightCombat;\n'
               'using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Utils;\n'
               'using Cultiway.Core.Combat;\nusing Cultiway.Core.EventSystem.Events;\n'
               'using Cultiway.Core.EventSystem.Systems;\n')
    settings = (f'\nconst string view = "{args.view}";\n'
                f'var selectedMoves = new string[] {{ {", ".join(map(json.dumps, args.move or []))} }};\n'
                'float shotZoom = 1.8f;\nVector3 shotPosition = default;\n'
                'Ready(source);\nsource.attack_timer = 999f;\nawait Advance(.18f);\n')
    film = (scripts / 'verify_migrated_martial_film.script').read_text()
    script = output / 'input.script'
    script.write_text(imports + base + actors + settings + setup + film + cleanup)
    try:
        result = baseline.call('run', str(script), '570000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'战技实录输出位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'战技实录未通过，见 {output}')
    encode(output, json.loads((output / 'recording.json').read_text()), args.view)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
