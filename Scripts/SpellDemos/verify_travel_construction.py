#!/usr/bin/env python3
"""7.11A：复验原出行任务如何选择本人双门，再继续原路线。"""
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'travel-construction'
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    setup = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    setup = setup.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                          'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "travel-construction")')
    setup = setup.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    setup = setup.replace('test.body-footwork.', 'test.travel-construction.')
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\n' + setup + (scripts / 'verify_travel_construction.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '165000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('出行复验未通过：' + str(folder))
    print(folder)


if __name__ == '__main__':
    main()
