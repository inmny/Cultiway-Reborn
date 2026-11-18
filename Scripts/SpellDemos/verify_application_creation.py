#!/usr/bin/env python3
"""5.17：从真实接触和库存创作，冷启动保留两代正式请求。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('section', choices=('creation', 'restart'))
    parser.add_argument('--state', type=Path)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请用 develop 的 dev.py exec 运行')
    scripts = Path(__file__).parent
    relative = 'application-creation/' + args.section
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / relative
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if args.section == 'creation' and args.state:
        shutil.copy2(args.state / 'created-definitions.json', folder / 'prior-definitions.json')
    if args.section != 'creation':
        if not args.state or not json.loads((args.state / 'result.json').read_text()).get('passed'):
            parser.error('需要上一进程通过的 creation --state')
        shutil.copytree(args.state / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
        expected = json.loads((args.state / 'pending-expected.json').read_text())
        old = json.loads((scripts / 'fixtures/application-creation-rule31.json').read_text())
        if old['source']['actorId'] != expected['legacyId']:
            raise RuntimeError('旧请求需要原始人物编号')
        write_json(folder / 'pending-expected.json', expected)
        write_json(folder / 'rule31.json', old)
        path = folder / 'pending-save/map.wbox'
        game = json.loads(zlib.decompress(path.read_bytes()))
        actor = next(row for row in game['actors_data'] if row['id'] == expected['legacyId'])
        slots = actor['custom_data_string']
        person = json.loads(slots['cultiway.actor'])
        person['sections']['ability_creation'] = old['section']
        slots['cultiway.actor'] = json.dumps(person, ensure_ascii=False, separators=(',', ':'))
        path.write_bytes(zlib.compress(json.dumps(game, ensure_ascii=False, separators=(',', ':')).encode()))
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "' + relative + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))', '')
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork."))', '')
    inventory = (scripts / 'inventory_case.script').read_text()
    material = 'Entity Material(' + inventory.split('Entity Material(', 1)[1].split('JObject Original(', 1)[0]
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\n' + helpers + '\n' + material +
                      (scripts / f'verify_application_creation_{args.section}.script').read_text())
    import baseline_scenarios as baseline
    write_json(folder / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'自主创作复验未通过：{folder}')
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
