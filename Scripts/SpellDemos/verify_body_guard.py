#!/usr/bin/env python3
"""6.09：在同一现成世界复验真实格挡面的方向、余量和正常退出。"""
import argparse
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('palm', 'weapon'), required=True)
    section = parser.parse_args().section
    relative = f'body-guard/{section}'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    setup = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    setup = setup.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                          f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    setup = setup.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    setup = setup.replace('test.body-footwork.', 'test.body-guard.')
    script = folder / 'input.script'
    script.write_text(setup + f'\nstring section = "{section}";\n' + (scripts / 'verify_body_guard.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '160000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('格挡未通过：' + str(folder))
    print('真实格挡：' + str(folder))


if __name__ == '__main__':
    main()
