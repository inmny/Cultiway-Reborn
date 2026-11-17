#!/usr/bin/env python3
"""5.11 B1的实际库存、消耗及保存复验。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('runtime', 'save', 'world', 'all'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    for current in ('runtime', 'save', 'world') if args.section == 'all' else (args.section,):
        folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'inventory' / current
        folder.mkdir(parents=True, exist_ok=True)
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "inventory/' + current + '")')
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        shared = Path(__file__).with_name('inventory_case.script').read_text()
        body = Path(__file__).with_name('verify_inventory_' + current + '.script').read_text()
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Components;\nusing Cultiway.Utils;\nusing Friflo.Engine.ECS;\n' + helpers + '\n' + shared + '\n' + body)
        write_json(folder / 'connection.json', connection)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('实际库存复验未完成 ' + str(folder))
        print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
