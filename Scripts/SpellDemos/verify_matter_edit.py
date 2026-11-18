#!/usr/bin/env python3
"""8.07：编辑、施放和正式重读真实换材与独立染色。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('section', nargs='?', choices=('ui', 'world', 'restart'), default='ui')
    parser.add_argument('--state', type=Path, help='上一段的输出目录')
    args = parser.parse_args()
    if args.section != 'ui' and args.state is None:
        parser.error('world 和 restart 需要上一段的 --state')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')

    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'matter-edit'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if args.section == 'world':
        for name in ('original.json', 'dye.json', 'edited.json'):
            if args.state.resolve() != folder.resolve():
                shutil.copyfile(args.state / name, folder / name)
    elif args.section == 'restart' and args.state.resolve() != folder.resolve():
        shutil.copyfile(args.state / 'pending.json', folder / 'pending.json')
        shutil.copytree(args.state / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "matter-edit")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.matter-edit.')
    helpers = helpers.replace('"checks.json"', f'"{args.section}-checks.json"')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.matter-edit."))', 'if (true)')
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.matter-edit."))', 'if (true)')
    case = (scripts / f'verify_matter_edit_{args.section}.script').read_text()
    if args.section == 'ui':
        controls = (scripts / 'verify_process_time_world_editor.script').read_text().split('async Task Frames(', 1)[1].split('ConstructionOperation Native(', 1)[0]
        case = case.replace('// COMMON_CONTROLS', 'async Task Frames(' + controls)
    script = folder / f'{args.section}-input.script'
    imports = ('using Cultiway.Const;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
               'using Cultiway.Core.EventSystem.Events;\nusing Cultiway.Core.EventSystem.Systems;\n'
               'using DebugToolbox.Runtime.Tracing;\n')
    script.write_text(imports + helpers + '\n' + case)
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'材料改造复验未通过：{folder}')
    except RuntimeError as error:
        write_json(folder / f'{args.section}-result.json', dict(passed=False, error=str(error)))
        raise
    result['output'] = str(folder)
    write_json(folder / f'{args.section}-result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
