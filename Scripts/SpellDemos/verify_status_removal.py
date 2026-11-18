#!/usr/bin/env python3
"""5.17 A：指定解除、正常恢复及正式存读。通过 develop 的独立环境运行。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--legacy', type=Path, help='可选：上一版已经完成的正式世界目录')
    options = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'status-removal'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if options.legacy:
        raw = zlib.decompress((options.legacy / 'map.wbox').read_bytes())
        saved = json.loads(raw)
        payload = json.loads(saved['mapStats']['custom_data']['custom_data_string']['cultiway.world'])
        section = payload['sections']['ability_constructions']
        if section['version'] != 15:
            parser.error('旧世界需要正式构造区段15')
        write_json(folder / 'legacy-constructions.json', section['data']['Definitions'])
        shutil.copytree(options.legacy, folder / 'legacy-save', dirs_exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "status-removal")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.status-removal.')
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Core.Components;\n' +
                      helpers + '\n' + Path(__file__).with_name('verify_status_removal.script').read_text())
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'指定解除复验未通过：{folder}')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
