#!/usr/bin/env python3
"""5.19：在独立 WorldBox 中验证尚缺实机证据的通用操作。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请用 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('motion', 'turn', 'turn-save', 'reload'), default='motion')
    parser.add_argument('--state', type=Path, help='reload 所用的 motion 输出目录')
    args = parser.parse_args()
    section = args.section
    scripts = Path(__file__).parent
    relative = 'operation-acceptance/' + section
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if section == 'reload':
        if not args.state or not (args.state / 'save/map.wbox').exists():
            parser.error('reload 需要 --state 指向已产生正式档的 motion 输出目录')
        shutil.copytree(args.state / 'save', folder / 'saved-world', dirs_exist_ok=True)
        for name in ('definitions.json', 'saved-actors.json'):
            shutil.copyfile(args.state / name, folder / name)
    else:
        name = '命名步骤行剑' if section == 'motion' else '折向续行'
        shutil.copyfile(PROJECT / f'Doc/技能系统全面重构/构造输入/{name}.json', folder / 'basis.json')
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.operation-acceptance.')
    script = folder / 'input.script'
    body = 'turn' if section == 'turn-save' else section
    prefix = 'bool saveOnly = ' + str(section == 'turn-save').lower() + ';\n' if body == 'turn' else ''
    script.write_text(helpers + '\n' + prefix + (scripts / f'verify_operation_acceptance_{body}.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '160000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('通用操作验证未通过：' + str(folder))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
