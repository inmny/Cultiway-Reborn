#!/usr/bin/env python3
"""5.17 D：实际讯号到达、条件结算与正式存读。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--saved-world', type=Path, help='只复验已生成的存档：传原输出目录（含 completed-save、定义及收件记录）')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'contact-signal'
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "contact-signal")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.contact-signal.')
    if args.saved_world:
        prior = folder / 'prior'
        shutil.copytree(args.saved_world / 'completed-save', prior / 'completed-save', dirs_exist_ok=True)
        for name in ('saved-definitions.json', 'receipts.json'):
            shutil.copy2(args.saved_world / name, prior / name)
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Core.Components;\n' +
                      helpers + ('\nbool savedOnly = true;\n' if args.saved_world else '\nbool savedOnly = false;\n') +
                      (scripts / 'verify_contact_signal.script').read_text())
    write_json(folder / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'接触讯号复验未通过：{folder}')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
