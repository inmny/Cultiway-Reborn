#!/usr/bin/env python3
"""5.20：以普通构造验证嵌套、换材、连接与真实结果的连续使用。"""
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
    parser.add_argument('--state', type=Path, help='另起游戏后传入初次运行的输出目录')
    parser.add_argument('--skip-interruption', action='store_true', help='已验收中断时只补正常施放与保存')
    args = parser.parse_args()
    section = 'reload' if args.state else 'finish' if args.skip_interruption else 'cast'
    relative = 'composite-change/' + section
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    if args.state:
        shutil.copytree(args.state / 'save', folder / 'saved-world', dirs_exist_ok=True)
        shutil.copyfile(args.state / 'expected.json', folder / 'expected.json')
    else:
        shutil.copyfile(PROJECT / 'Doc/技能系统全面重构/构造输入/聚剑凝冰.json', folder / 'definition.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.composite-change.')
    script = folder / 'input.script'
    script.write_text(helpers + '\nbool reload = ' + str(bool(args.state)).lower() + ';\n' +
                      'bool checkInterruption = ' + str(section == 'cast').lower() + ';\n' +
                      (scripts / 'verify_composite_change.script').read_text())
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
        raise RuntimeError('复合变化验收未通过：' + str(folder))


if __name__ == '__main__':
    main()
