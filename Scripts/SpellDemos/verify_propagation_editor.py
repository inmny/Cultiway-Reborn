#!/usr/bin/env python3
"""5.18 D1：用实机已通过的构造编辑三种实际到达条件。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--evidence', type=Path, default=Path.home() / '.local/state/cultiway-dev/evidence')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'propagation-editor'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    inputs = {
        'ground': 'ground-route-a/passed/definition.json',
        'water': 'network-arrival-b/passed/test.network-arrival.water.json',
        'signal': 'network-arrival-b/passed/test.network-arrival.signal.json',
    }
    for case, relative in inputs.items():
        shutil.copyfile(args.evidence / relative, folder / f'{case}-input.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "propagation-editor")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    controls = (scripts / 'verify_process_time_world_editor.script').read_text().split('async Task Frames(', 1)[1].split('ConstructionOperation Native(', 1)[0]
    case = (scripts / 'verify_propagation_editor.script').read_text().replace('// COMMON_CONTROLS', 'async Task Frames(' + controls)
    script = folder / 'input.script'
    script.write_text('using Cultiway.Const;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers + '\n' + case)
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'传播条件窗口复验未通过：{folder}')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
