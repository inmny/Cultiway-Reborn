#!/usr/bin/env python3
"""复验移动火身及实际燃烧、冷却后的显示。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('travel', 'burning'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 在独立游戏中运行')
    import baseline_scenarios as baseline
    directory = Path(__file__).parent
    folder = Path(tempfile.mkdtemp(prefix=f'fire-presentation-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    travel = args.section == 'travel'
    helper = 'verify_body_footwork.script' if travel else 'verify_burning.script'
    marker = 'var bindingType =' if travel else 'var events=new List<ConstructedBurning>();'
    output = 'body-footwork' if travel else 'burning'
    helpers = (directory / helper).read_text().split(marker, 1)[0]
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace(f'Path.Combine(root,"{fixture}")', json.dumps(str(PROJECT / fixture)))
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', json.dumps(str(PROJECT / fixture)))
    separator = ', ' if travel else ','
    helpers = helpers.replace(f'Path.Combine(root{separator}"artifacts/skill_rebuild/{output}")',
                              json.dumps(str(folder)))
    definitions = json.dumps(str(PROJECT / 'Doc/技能系统全面重构/构造输入'), ensure_ascii=False)
    helpers = helpers.replace('Path.Combine(root,"Doc/技能系统全面重构/构造输入/"+name+".json")',
                              f'Path.Combine({definitions}, name + ".json")')
    helpers = helpers.replace('test.body-footwork.', 'Cultiway.Demo.FiniteFire')
    if not travel:
        helpers = 'using Cultiway.Core.Visuals.Forms;\n' + helpers
    body = Path(__file__).with_suffix('.script').read_text()
    travel_body, burning_body = body.split('// 附着燃烧复验', 1)
    text = travel_body if travel else burning_body
    script = folder / 'input.script'
    script.write_text(helpers + text.replace('DEFINITION_ROOT', definitions))
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '125000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'火系复验未完成：{folder}')
    print(json.dumps(dict(result, output=str(folder)), ensure_ascii=False))


if __name__ == '__main__':
    main()
