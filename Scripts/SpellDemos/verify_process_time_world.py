#!/usr/bin/env python3
"""5.16 B：世界材料变速、编辑、自主创作及正式存档。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json
from verify_world_erosion import replace_section


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('pending', 'restart', 'retiming', 'editor', 'creation', 'creation-restart', 'creation-use'), required=True)
    parser.add_argument('--state', type=Path, help='上一进程的 pending 或 creation 目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过develop的dev.py exec运行')
    scripts = Path(__file__).parent
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'process-time-world' / args.section
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(fixture, folder / 'input-world', dirs_exist_ok=True)
    if args.section == 'restart':
        if not args.state or not json.loads((args.state / 'result.json').read_text()).get('passed'):
            parser.error('restart需要上一进程已通过的--state')
        shutil.copytree(args.state / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
        expected = json.loads((args.state / 'expected.json').read_text())
        write_json(folder / 'expected.json', expected)
        shutil.copytree(args.state / 'pending-save', folder / 'bad-world', dirs_exist_ok=True)
        broken = json.loads(json.dumps(expected['section']))
        next(row for row in broken['data']['Records'] if row['Id'] == expected['id'])['Material']['Erosion']['WorldElapsed'] += 20
        replace_section(folder / 'bad-world', broken)
    if args.section in ('creation-restart', 'creation-use'):
        if not args.state or not json.loads((args.state / 'result.json').read_text()).get('passed'):
            parser.error('creation-restart 需要上一进程已通过的 creation --state')
        shutil.copytree(args.state / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
        expected = json.loads((args.state / 'pending-expected.json').read_text())
        write_json(folder / 'pending-expected.json', expected)
        old = json.loads((scripts / 'fixtures/process-time-creation-rule30.json').read_text())
        if old['source']['actorId'] != expected['legacyId']:
            raise RuntimeError('旧请求需要保留实际人物编号')
        write_json(folder / 'rule30.json', old)
        path = folder / 'pending-save/map.wbox'
        game = json.loads(zlib.decompress(path.read_bytes()))
        actor = next(row for row in game['actors_data'] if row['id'] == expected['legacyId'])
        slots = actor['custom_data_string']
        person = json.loads(slots['cultiway.actor'])
        person['sections']['ability_creation'] = old['section']
        slots['cultiway.actor'] = json.dumps(person, ensure_ascii=False, separators=(',', ':'))
        path.write_bytes(zlib.compress(json.dumps(game, ensure_ascii=False, separators=(',', ':')).encode()))
    if args.section in ('restart', 'retiming'):
        version = 8 if args.section == 'restart' else 9
        shutil.copytree(fixture, folder / 'legacy-world', dirs_exist_ok=True)
        legacy = json.loads((scripts / f'fixtures/process-time-world-v{version}.json').read_text())
        replace_section(folder / 'legacy-world', legacy['section'])
        write_json(folder / 'legacy.json', legacy)
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "process-time-world/' + args.section + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    script_section = 'creation-restart' if args.section == 'creation-use' else args.section
    body = (scripts / ('verify_process_time_world_' + script_section + '.script')).read_text()
    shared = (scripts / 'erosion_case.script').read_text() + '\n' + (scripts / 'world_erosion_request_case.script').read_text()
    if args.section in ('pending', 'retiming'):
        shared += '\n' + (scripts / 'process_time_world_case.script').read_text()
    if args.section == 'editor':
        helpers = 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers
        shared = ''
    if args.section in ('creation', 'creation-restart', 'creation-use'):
        helpers = 'using Friflo.Engine.ECS;\n' + helpers
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))', '')
        shared = ''
    if script_section == 'creation-restart':
        shared = 'bool useOnly = ' + ('true' if args.section == 'creation-use' else 'false') + ';'
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + shared + '\n' + body)
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('世界材料变速复验未通过 ' + str(folder))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
