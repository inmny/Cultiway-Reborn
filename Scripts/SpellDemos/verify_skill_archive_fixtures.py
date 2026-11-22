#!/usr/bin/env python3
"""用原版游戏读回固定的旧档、当前档和部分失败档。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import zlib

from common import PROJECT, write_json


FIXTURES = PROJECT / 'Scripts/WorldSaveTests/Fixtures'


def sections(path):
    world = json.loads(zlib.decompress(path.read_bytes()))
    return json.loads(world['mapStats']['custom_data']['custom_data_string']['cultiway.world'])['sections']


def check_saved(case, expected, source, saved):
    before = sections(source)
    after = sections(saved)
    if case == 'current':
        original = before['ability_constructions']
        current = after['ability_constructions']
        ids = {definition['Id'] for definition in expected['definitions']}

        def chosen(section):
            return {item['Id']: item for item in section['data']['Definitions'] if item['Id'] in ids}

        if current['version'] != expected['constructionsVersion'] or chosen(original) != chosen(current):
            raise RuntimeError('当前档写回后的成品发生变化')
        return

    old_objects = before['objects']
    new_objects = after['objects']
    identities = {record['Id']: record for record in new_objects['data']['Objects']}
    if case == 'partial':
        old_actor = next(record for record in old_objects['data']['Objects'] if record['Id'] == 'actor:1')
        learned = [item for item in old_actor['LearnedSkills'] if item != expected['rejected']]
        if expected['rejected'] in identities or identities['actor:1']['LearnedSkills'] != learned:
            raise RuntimeError('坏记录仍存在，或其他学习关系未保存')
        return

    def skills(section):
        return {record['Id']: (record['Skill']['EntityAssetId'], record['Skill']['AnimationIndex'],
                               record['Skill']['TrajectoryAssetId'])
                for record in section['data']['Objects'] if record.get('Skill')}

    if (old_objects['version'] != expected['oldObjectsVersion'] or new_objects['version'] < 3 or
            len(skills(new_objects)) != expected['skills'] or skills(old_objects) != skills(new_objects) or
            identities['actor:1']['LearnedSkills'] != expected['learned']):
        raise RuntimeError('升级未保留原技能、轨迹或学习关系')


def main():
    manifest = json.loads((FIXTURES / 'skill-archive-expectations.json').read_text())
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=manifest, required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请在独立游戏中使用 dev.py exec')
    import baseline_scenarios as baseline

    expected = manifest[args.case]
    original = FIXTURES / expected['folder'] / 'map.wbox'
    raw = original.read_bytes()
    if hashlib.sha256(raw).hexdigest() != expected['sha256']:
        raise RuntimeError('样板已经改变')
    output = Path(tempfile.mkdtemp(prefix=f'skill-archive-{args.case}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = output / 'input-world'
    world.mkdir()
    copy = world / 'map.wbox'
    shutil.copyfile(original, copy)
    write_json(output / 'expected.json', expected)
    script = output / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text()
    script.write_text(body.replace('"$CASE"', json.dumps(args.case)).replace('"$OUTPUT"', json.dumps(str(output))))
    write_json(output / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '85000')
        if not result.get('passed') or original.read_bytes() != raw or copy.read_bytes() != raw:
            raise RuntimeError('读档失败或原文件发生改变')
        if result.get('backup') and (Path(result['backup']) / 'map.wbox').read_bytes() != raw:
            raise RuntimeError('游戏生成的原档备份与输入不同')
        check_saved(args.case, expected, copy, output / 'archive/map.wbox')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise
    result['archiveSha256'] = expected['sha256']
    write_json(output / 'result.json', result)
    print(output)


if __name__ == '__main__':
    main()
