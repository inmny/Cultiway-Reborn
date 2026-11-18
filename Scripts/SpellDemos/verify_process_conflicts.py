#!/usr/bin/env python3
"""8.08：复验多项改造的冲突定位、恢复及实际执行次序。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('section', nargs='?', choices=('ui', 'save', 'world'), default='ui')
    parser.add_argument('--state', type=Path, help='上一段的输出目录')
    args = parser.parse_args()
    if args.section == 'world' and args.state is None:
        parser.error('world 需要上一段的 --state')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')

    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'process-conflicts'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if args.section == 'world' and args.state.resolve() != folder.resolve():
        shutil.copyfile(args.state / 'edited.json', folder / 'edited.json')
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "process-conflicts")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.process-conflicts.')
    helpers = helpers.replace('"checks.json"', f'"{args.section}-checks.json"')
    case_name = 'ui' if args.section == 'save' else args.section
    case = (scripts / f'verify_process_conflicts_{case_name}.script').read_text()
    if case_name == 'ui':
        controls = (scripts / 'verify_process_time_world_editor.script').read_text().split('async Task Frames(', 1)[1].split('ConstructionOperation Native(', 1)[0]
        case = case.replace('// COMMON_CONTROLS', 'async Task Frames(' + controls)
        case = 'bool saveOnly = ' + ('true' if args.section == 'save' else 'false') + ';\n' + case
    if args.section == 'world':
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.process-conflicts."))', 'if (true)')
    script = folder / f'{args.section}-input.script'
    imports = 'using Cultiway.Const;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
    script.write_text(imports + helpers + '\n' + case)
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'多项改造复验未通过：{folder}')
    except RuntimeError as error:
        write_json(folder / f'{args.section}-result.json', dict(passed=False, error=str(error)))
        raise
    result['output'] = str(folder)
    write_json(folder / f'{args.section}-result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
