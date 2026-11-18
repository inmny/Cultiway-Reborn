#!/usr/bin/env python3
"""5.17 B：同一真实抓持动作的主动放手和解救。"""
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'grab-release'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    here = Path(__file__).parent
    footwork = (here / 'verify_body_footwork.script').read_text()
    movement = 'var movementType' + footwork.split('var movementType', 1)[1].split('var pairedFrames', 1)[0]
    helpers = footwork.split('var bridge =', 1)[0] + movement
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "grab-release")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")').replace('test.body-footwork.', 'test.grab-release.')
    old = (here / 'verify_body_connections.script').read_text()
    grab = 'AbilityDefinition Grab(string name)' + old.split('bool withdraw = false)', 1)[1].split('const bool verifyParallelOccupancy', 1)[0]
    grab = grab.replace('if (withdraw)\n        {\n            stay.At = .8f;\n            stay.Hand = new[] { .02f, .2f, .52f };\n        }', '')
    grab = grab.replace('test.body-footwork.connections.', 'test.grab-release.').replace('Kind = kind', 'Kind = BodyRelationKind.Grab')
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\n' + helpers + '\nobject worldfall = null;\n' +
                      (here / 'verify_body_connection_placement.script').read_text() + '\n' + grab + '\n' +
                      (here / 'verify_grab_release.script').read_text())
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'抓持解除复验未通过：{folder}')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
