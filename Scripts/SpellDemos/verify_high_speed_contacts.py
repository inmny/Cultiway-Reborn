#!/usr/bin/env python3
"""6.03：剑刃、飞掌及快速转向的实际接触。"""
import argparse
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('linear', 'blade', 'palm', 'turn', 'breaks'), default='linear')
    args = parser.parse_args()
    relative = f'high-speed-contacts/{args.section}'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.high-speed.')
    script = folder / 'input.script'
    script.write_text(helpers + f'\nstring section = "{args.section}";\n' +
                      (scripts / 'verify_high_speed_contacts.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '160000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('高速接触未通过：' + str(folder))
    print('高速实际接触：' + str(folder))


if __name__ == '__main__':
    main()
