#!/usr/bin/env python3
"""7.11B：复验实际打坐挑选凝神构造、扣费与地块结算。"""
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'meditation-construction'
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    setup = (scripts / 'verify_body_footwork.script').read_text().split('async Task Fire(', 1)[0]
    setup = setup.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                          'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "meditation-construction")')
    setup = setup.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    setup = setup.replace('test.body-footwork.', 'test.meditation-construction.')
    setup = '\n'.join(line for line in setup.splitlines() if not line.startswith('object Field(')) + '\n'
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Content.Behaviours;\nusing Cultiway.Content.Const;\n' + setup + (scripts / 'verify_meditation_construction.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '165000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('打坐复验未通过：' + str(folder))
    print(folder)


if __name__ == '__main__':
    main()
