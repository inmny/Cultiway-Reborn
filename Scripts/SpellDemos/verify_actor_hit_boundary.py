#!/usr/bin/env python3
"""6.01：复验当前技能的人物受击范围与世界高度。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true', help='要求实际 Worldfall 地表已接入')
    parser.add_argument('--section', choices=('construction', 'all'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请用 develop 的 dev.py exec 运行')
    relative = f'actor-hit-boundary/{"worldfall" if args.worldfall else "native"}/{args.section}'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.actor-hit-boundary.')
    script = folder / 'input.script'
    script.write_text(helpers + f'\nstring section = "{args.section}";\n' +
                      f'bool worldfall = {str(args.worldfall).lower()};\n' +
                      (scripts / 'verify_actor_hit_boundary.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '160000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False))
    if not result.get('passed'):
        raise RuntimeError('人物受击范围未通过：' + str(folder))


if __name__ == '__main__':
    main()
