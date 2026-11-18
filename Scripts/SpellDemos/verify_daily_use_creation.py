#!/usr/bin/env python3
"""7.11D：实际修炼灵感、日用创法与两代正式等待档。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json


def verify_legacy(legacy, folder):
    expected = json.loads((legacy / 'pending-expected.json').read_text())
    original = legacy / 'pending-save/map.wbox'
    data = original.read_bytes()
    world = json.loads(zlib.decompress(data))
    actor = next(row for row in world['actors_data'] if row['id'] == expected['sourceId'])
    section = json.loads(actor['custom_data_string']['cultiway.actor'])['sections']['ability_creation']
    if section['version'] != 34 or section['data']['Inspiration']['Rules'] != 33 or not section['data']['Pending']:
        raise RuntimeError('历史资料没有真实等待中的规则33请求')
    shutil.copytree(legacy / 'pending-save', folder / 'legacy-save', dirs_exist_ok=True)
    expected['originalEvidence'] = {'map': str(original), 'sha256': hashlib.sha256(data).hexdigest()}
    write_json(folder / 'legacy-expected.json', expected)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('section', choices=('creation', 'restart'))
    parser.add_argument('--state', type=Path)
    parser.add_argument('--legacy', type=Path, default=Path.home() / '.local/state/cultiway-dev/evidence/propagation-creation-d2/creation')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 dev.py exec 运行')
    relative = 'daily-use-creation/' + args.section
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    if args.section == 'restart':
        if not args.state or not json.loads((args.state / 'result.json').read_text()).get('passed'):
            parser.error('需要上一进程通过的 creation --state')
        shutil.copytree(args.state / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
        shutil.copyfile(args.state / 'pending-expected.json', folder / 'pending-expected.json')
        verify_legacy(args.legacy, folder)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('async Task Fire(', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))', '')
    helpers = '\n'.join(line for line in helpers.splitlines() if not line.startswith('object Field(')) + '\n'
    inventory = (scripts / 'inventory_case.script').read_text()
    material = 'Entity Material(' + inventory.split('Entity Material(', 1)[1].split('JObject Original(', 1)[0]
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Content.Const;\nusing Cultiway.Content.Behaviours;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\n' +
                      helpers + (material if args.section == 'creation' else '') +
                      (scripts / f'verify_daily_use_creation_{args.section}.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '165000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'日用创法复验未通过：{folder}')
    print(folder)


if __name__ == '__main__':
    main()
