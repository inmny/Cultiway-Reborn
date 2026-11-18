#!/usr/bin/env python3
"""8.06：复用实际地波场景，验证到达、留置及后续收束的衔接。"""
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
    parser.add_argument('--ground', type=Path, default=Path.home() /
                        '.local/state/cultiway-dev/evidence/ground-route-a/passed/definition.json')
    args = parser.parse_args()
    if args.section != 'ui' and args.state is None:
        parser.error('world 和 restart 需要上一段的 --state')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')

    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'propagation-residue'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if args.section == 'ui':
        shutil.copyfile(args.ground, folder / 'ground-input.json')
    elif args.section == 'world':
        for name in ('ground-saved.json', 'recovery-saved.json'):
            if args.state.resolve() != folder.resolve():
                shutil.copyfile(args.state / name, folder / name)
    else:
        shutil.copyfile(args.state / 'pending.json', folder / 'pending.json')
        shutil.copytree(args.state / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "propagation-residue")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.propagation-residue.')
    helpers = helpers.replace('"checks.json"', f'"{args.section}-checks.json"')
    case = (scripts / f'verify_propagation_residue_{args.section}.script').read_text()
    if args.section == 'ui':
        controls = (scripts / 'verify_process_time_world_editor.script').read_text().split('async Task Frames(', 1)[1].split('ConstructionOperation Native(', 1)[0]
        case = case.replace('// COMMON_CONTROLS', 'async Task Frames(' + controls)
    script = folder / f'{args.section}-input.script'
    script.write_text('using Cultiway.Const;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers + '\n' + case)
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'传播与留置复验未通过：{folder}')
    except RuntimeError as error:
        write_json(folder / f'{args.section}-result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / f'{args.section}-result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
