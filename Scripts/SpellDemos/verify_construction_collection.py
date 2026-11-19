#!/usr/bin/env python3
"""8.11：验证收藏和分享，再用同一存档及本机收藏冷启动。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--reload-from', type=Path, help='第一次运行输出的目录；必须从新启动的独立游戏运行')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')

    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / ('construction-collection-reload' if args.reload_from else 'construction-collection')
    folder.mkdir(parents=True, exist_ok=True)
    scripts = Path(__file__).parent
    if args.reload_from:
        prior = args.reload_from.resolve()
        if not (prior / 'expected.json').is_file() or not (prior / 'save' / 'map.wbox').is_file():
            parser.error('--reload-from 需要同时有 expected.json 和 save/map.wbox')
        program = (scripts / 'verify_construction_collection_reload.script').read_text()
        program = program.replace('__PRIOR_OUTPUT__', json.dumps(str(prior))).replace('__OUTPUT__', json.dumps(str(folder)))
    else:
        shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
        helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
        helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
        controls = (scripts / 'verify_process_time_world_editor.script').read_text().split('async Task Frames(', 1)[1].split('ConstructionOperation Native(', 1)[0]
        case = (scripts / 'verify_construction_collection.script').read_text().replace('// COMMON_CONTROLS', 'async Task Frames(' + controls)
        program = 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing Cultiway.Core.SkillLibV3.Wanfa;\nusing UnityEngine.UI;\n' + helpers + '\n' + case
    script = folder / 'input.script'
    script.write_text(program)
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'构造收藏复验未通过：{folder}')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    result['output'] = str(folder)
    write_json(folder / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
