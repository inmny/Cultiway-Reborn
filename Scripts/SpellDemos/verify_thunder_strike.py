#!/usr/bin/env python3
"""9.18：正式聚雷、落雷接触和退场的实机复验。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
import baseline_scenarios as baseline


def main():
    """复用现成世界和角色准备，把复验脚本与逐步结果一并留档。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise RuntimeError('请通过 develop 的 dev.py exec 运行落雷复验')
    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix='thunder-strike-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(output))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1')))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);',
                              'contacts.Add(contact);')
    helpers = helpers.replace('    if (fact.AbilityId.StartsWith("test.body-footwork."))\n', '')
    body = Path(__file__).with_suffix('.script').read_text()
    body = body.replace('const string inputRoot = "";',
                        'const string inputRoot = ' + json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入')) + ';')
    script = output / 'input.script'
    script.write_text(helpers + '\n' + body)
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('落雷复验失败，请查看 ' + str(output / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
