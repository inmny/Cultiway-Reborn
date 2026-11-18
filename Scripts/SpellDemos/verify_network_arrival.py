#!/usr/bin/env python3
"""复用水路和讯号场景，核对实际送达驱动后续部分。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'water', 'signal'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'network-arrival'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if args.section != 'signal':
        shutil.copyfile(PROJECT / 'Doc/技能系统全面重构/构造输入/循水连雷.json', folder / 'water-input.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "network-arrival")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.network-arrival.')
    script = folder / 'input.script'
    script.write_text(helpers + '\nstring section = ' + json.dumps(args.section) + ';\n' +
                      (scripts / 'verify_network_arrival.script').read_text())
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'介质与讯号送达复验未通过：{folder}')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, section=args.section, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
