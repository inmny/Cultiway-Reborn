#!/usr/bin/env python3
"""复用地表传播场景，核对新到达条件的正式施放。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'arrival', 'interruption'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'ground-route'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "ground-route")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.ground-route.')
    script = folder / 'input.script'
    script.write_text(helpers + '\nstring section = ' + json.dumps(args.section) + ';\n' +
                      (scripts / 'verify_ground_route.script').read_text())
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'地波到达复验未通过：{folder}')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, section=args.section, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
