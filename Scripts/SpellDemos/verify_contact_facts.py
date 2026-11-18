#!/usr/bin/env python3
"""6.05：复验交锋、触地、生命和护盾的实际结算。"""
import argparse
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('opposition', 'ground', 'damage', 'body'), required=True)
    section = parser.parse_args().section
    relative = f'contact-facts/{section}'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    if section == 'opposition':
        source = (scripts / 'verify_reflection.script').read_text().replace('bool factsOnly = false;', 'bool factsOnly = true;')
        source = source.replace(f'Path.Combine(root, "{fixture}")',
                                f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}", "input-world")')
    else:
        helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                                  f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
        helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
        if section == 'ground':
            source = helpers + '\n' + (scripts / 'verify_world_contact_events.script').read_text()
        else:
            helpers = helpers.replace('test.body-footwork.', 'test.contact-facts.')
            source = (helpers + f'\nstring section = "{section}";\n' +
                      (scripts / 'verify_contact_damage_facts.script').read_text())
    script = folder / 'input.script'
    script.write_text(source)
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '160000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('接触记录未通过：' + str(folder))
    print('真实接触记录：' + str(folder))


if __name__ == '__main__':
    main()
