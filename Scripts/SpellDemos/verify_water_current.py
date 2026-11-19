#!/usr/bin/env python3
"""复验有限水层的分流、薄边与正式接触。"""
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 在独立游戏中运行')
    import baseline_scenarios as baseline
    directory = Path(__file__).parent
    folder = Path(tempfile.mkdtemp(prefix='water-current-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = (directory / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    world = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace(f'Path.Combine(root, "{world}")', json.dumps(str(PROJECT / world)))
    helpers = helpers.replace('test.body-footwork.', 'Cultiway.Demo.VolumeCurrent')
    script = folder / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text().replace(
        'Path.Combine(root, "Doc/技能系统全面重构/构造输入", name + ".json")',
        f'Path.Combine({json.dumps(str(PROJECT / "Doc/技能系统全面重构/构造输入"), ensure_ascii=False)}, name + ".json")')
    script.write_text(helpers + body)
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '120000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'贴地后流复验未完成：{folder}')
    print(json.dumps(dict(result, output=str(folder)), ensure_ascii=False))


if __name__ == '__main__':
    main()
