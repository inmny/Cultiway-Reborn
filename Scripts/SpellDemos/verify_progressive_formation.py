#!/usr/bin/env python3
"""5.06限定验证，使用develop准备的同一独立游戏。"""
import argparse
import json
import os
import shutil
from pathlib import Path

from common import PROJECT, write_json


def main():
    """复用现有世界助手，保留实际输入、结果和原始失败。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'creation', 'legacy'), default='all')
    section = parser.parse_args().section
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过develop的dev.py exec运行此入口，以使用所属独立游戏')
    import baseline_scenarios as baseline
    connected = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'progressive-formation' / section
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    shutil.copy2(Path(__file__).parent / 'fixtures/formation-rule22.json', folder / 'rule22.json')
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "progressive-formation/' + section + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    body = Path(__file__).with_suffix('.script').read_text()
    body = body.replace('Path.Combine(root,"Scripts/SpellDemos/fixtures/formation-rule22.json")', 'Path.Combine(folder,"rule22.json")')
    if section == 'legacy':
        body = (body.split('try\n{', 1)[0] + 'try\n{\n    var saved=' + body.split('    var saved=', 1)[1])
        body = body.replace('逐段成形的冻结输入、独立创作与衍生接入', '正式旧请求保持')
    if section == 'creation':
        body = body.split('    var saved=', 1)[0] + '    return new' + body.rsplit('    return new', 1)[1]
    script = folder / 'input.script'
    script.write_text('using Cultiway.Core.Persistence;\n' + helpers + '\n' + body)
    write_json(folder / 'connection.json', connected)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('逐段成形创作未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
