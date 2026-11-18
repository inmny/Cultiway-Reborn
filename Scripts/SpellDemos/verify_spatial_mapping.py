#!/usr/bin/env python3
"""复验生成形体、生灵和原法宝的双门转移。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('core', 'runtime', 'all', 'actor', 'actor-clear', 'artifact', 'artifact-clear', 'artifact-varied', 'capacity', 'staging', 'editor', 'creation', 'restart'), default='all')
    parser.add_argument('--pending', type=Path, help='creation 输出目录，仅 restart 使用')
    args = parser.parse_args()
    section = args.section
    if (section == 'restart') != (args.pending is not None):
        parser.error('restart 必须提供 --pending 指向 creation 输出，其他段不使用')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'spatial-mapping' / section
    folder.mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    if args.pending:
        shutil.copy2(args.pending / 'pending-expected.json', folder / 'pending-expected.json')
        fixture = Path(__file__).with_name('fixtures') / 'spatial-mapping-creation-rule29.json'
        shutil.copy2(fixture, folder / 'rule29.json')
        previous = json.loads(fixture.read_text())
        expected = json.loads((folder / 'pending-expected.json').read_text())
        if previous['source']['actorId'] != expected['legacyId']:
            raise RuntimeError('旧请求必须放回相同的人物编号')
        shutil.copytree(args.pending / 'pending-save', folder / 'pending-save', dirs_exist_ok=True)
        path = folder / 'pending-save/map.wbox'
        game = json.loads(zlib.decompress(path.read_bytes()))
        legacy = next(actor for actor in game['actors_data'] if actor['id'] == expected['legacyId'])
        person_key = 'cultiway.actor'
        slots = legacy.setdefault('custom_data_string', {})
        person = json.loads(slots[person_key])
        person['sections']['ability_creation'] = previous['section']
        slots[person_key] = json.dumps(person, ensure_ascii=False, separators=(',', ':'))
        path.write_bytes(zlib.compress(json.dumps(game, ensure_ascii=False, separators=(',', ':')).encode()))
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "spatial-mapping", "' + section + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('test.body-footwork.', 'test.spatial-mapping.')
    variant = section.split('-', 1)[0]
    name = f'verify_spatial_mapping_{variant}.script' if variant in ('actor', 'artifact', 'capacity', 'staging', 'editor', 'creation', 'restart') else 'verify_spatial_mapping.script'
    body = Path(__file__).with_name(name).read_text()
    if variant in ('creation', 'restart'):
        helpers = 'using Cultiway.Core.Persistence;\n' + helpers
    if variant in ('artifact', 'creation', 'restart'):
        helpers = 'using Friflo.Engine.ECS;\n' + helpers
    if variant == 'editor':
        helpers = 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n' + helpers
    if variant in ('creation', 'restart'):
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.spatial-mapping."))\n        endings.Add', 'endings.Add')
    script = folder / 'input.script'
    script.write_text(helpers + '\nconst string section = "' + section + '";\n' + body)
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'空间转移未通过：{folder}')
    print(result)


if __name__ == '__main__':
    main()
