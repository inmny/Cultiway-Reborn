#!/usr/bin/env python3
"""5.14 C1使用真实旧新等待世界和已完成世界，验证两次正式冷启动。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json

EVIDENCE = Path.home() / '.local/state/cultiway-dev/evidence'
OLD = EVIDENCE / 'erosion-creation/runs/20261117T164637.349221Z-6368e674/output/erosion-creation/save'
TIMING = EVIDENCE / 'correspondence-timing/runs/20261118T052945.864202Z-f0db9a43/output/correspondence/timing'


def copy_checked(source, expected, world, folder, label):
    result = json.loads((source / 'result.json').read_text())
    if not result.get('passed'):
        raise RuntimeError('需要已通过的原始世界：' + str(source))
    raw = (source / world / 'map.wbox').read_bytes()
    shutil.copytree(source / world, folder / (label + '-world'), dirs_exist_ok=True)
    payload = json.loads((source / expected).read_text())
    if label in ('old', 'new') and world == 'pending-save':
        saved = json.loads(zlib.decompress(raw))
        actor = next(value for value in saved['actors_data'] if value['id'] == payload['sourceId'])
        section = json.loads(actor['custom_data_string']['cultiway.actor'])['sections']['ability_creation']
        version, rules = (29, 28) if label == 'old' else (30, 29)
        if section['version'] != version or section['data']['Inspiration']['Rules'] != rules or not section['data']['Pending']:
            raise RuntimeError('原正式等待档版本或状态错误 ' + label)
        payload['frozenSection'] = section['data']
    write_json(folder / (label + '-expected.json'), payload)
    return dict(source=str(source / world), process=result['process'], sha256=hashlib.sha256(raw).hexdigest())


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', required=True, choices=('resume', 'completed', 'frozen'))
    parser.add_argument('--state', required=True, type=Path, help='resume和frozen读取B3的creation；completed读取上次resume')
    parser.add_argument('--legacy', type=Path, default=OLD)
    parser.add_argument('--timing', type=Path, default=TIMING)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop运行')
    import baseline_scenarios as baseline
    connected = baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'correspondence-cold' / args.section
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if args.section == 'resume':
        sources = {'old': (args.legacy, 'pending-expected.json', 'pending-save'),
                   'new': (args.state, 'pending-expected.json', 'pending-save'),
                   'timing': (args.timing, 'expected.json', 'completed-save')}
    elif args.section == 'frozen':
        sources = {'new': (args.state, 'pending-expected.json', 'pending-save')}
    else:
        sources = {name: (args.state, name + '-completed.json', name + '-completed') for name in ('old', 'new')}
    write_json(folder / 'sources.json', {name: copy_checked(*source, folder, name) for name, source in sources.items()})
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', 'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "correspondence-cold/' + args.section + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
    body = (scripts / 'verify_correspondence_delivery.script').read_text()
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\n' + helpers + '\n' +
                      (scripts / 'correspondence_case.script').read_text() + '\n' +
                      f'const bool resuming = {str(args.section == "resume").lower()};\n' +
                      f'const bool frozenOnly = {str(args.section == "frozen").lower()};\n' + body)
    write_json(folder / 'connection.json', connected)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('映像冷启动未通过 ' + str(folder))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
