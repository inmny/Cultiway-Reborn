#!/usr/bin/env python3
"""7.20：近身战技生成、正式使用与等待档。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('stage', choices=('prepare', 'restore'))
    parser.add_argument('--state', type=Path)
    parser.add_argument('--legacy', type=Path, default=Path.home() / '.local/state/cultiway-dev/evidence/daily-use-creation/creation')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 dev.py exec 运行')
    relative = 'martial-study/' + args.stage
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    fixture = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    shutil.copytree(PROJECT / fixture, folder / 'input-world', dirs_exist_ok=True)
    if args.stage == 'restore':
        if not args.state or not json.loads((args.state / 'result.json').read_text()).get('passed'):
            parser.error('需要上一进程通过的 prepare --state')
        shutil.copytree(args.state / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
        shutil.copyfile(args.state / 'pending-expected.json', folder / 'pending-expected.json')
        old = json.loads((args.legacy / 'pending-expected.json').read_text())
        historic = args.legacy / 'pending-save/map.wbox'
        raw = historic.read_bytes()
        actor = next(a for a in json.loads(zlib.decompress(raw))['actors_data'] if a['id'] == old['sourceId'])
        historic_section = json.loads(actor['custom_data_string']['cultiway.actor'])['sections']['ability_creation']
        if historic_section['version'] != 35 or not historic_section['data']['Pending'] or historic_section['data']['Inspiration']['Rules'] != 34:
            parser.error('历史档必须是规则34的真实等待世界')
        old['sha256'] = hashlib.sha256(raw).hexdigest()
        write_json(folder / 'legacy-expected.json', old)
        shutil.copytree(args.legacy / 'pending-save', folder / 'legacy-save', dirs_exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('async Task Fire(', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "{relative}")')
    helpers = helpers.replace(f'Path.Combine(root, "{fixture}")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))', '')
    script = folder / 'input.script'
    script.write_text('using Cultiway.Content.Const;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\n' + helpers +
                      (scripts / f'verify_martial_study_{args.stage}.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '165000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'近身战技复验未通过：{folder}')
    print(folder)


if __name__ == '__main__':
    main()
