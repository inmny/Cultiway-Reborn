#!/usr/bin/env python3
"""核对原骑士正式档及自然变式的写入、恢复和再次读取。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

from check_full_world_upgrade import actor_sections, read_world
from common import write_json


def require(condition, reason):
    if not condition:
        raise RuntimeError(reason)


def without_null_members(value):
    if isinstance(value, dict):
        return {key: without_null_members(item) for key, item in value.items() if item is not None}
    if isinstance(value, list):
        return [without_null_members(item) for item in value]
    return value


def actor(world, identity):
    return next(row for row in world['actors_data'] if row['id'] == identity)


def learned_executions(sections):
    records = {row['Id']: row for row in sections['objects']['data']['Objects']}
    def execution(item):
        record = records[item]
        return (record.get('Skill') or {}).get('EntityAssetId') or record.get('GrantedSkillAsset', '')

    return {key: sorted(name for name in map(execution, row.get('LearnedSkills', []))
                        if name.startswith('Cultiway.KnightTechnique.'))
            for key, row in records.items() if key.startswith('actor:')}


def check_existing(folder, all_known):
    index = json.loads((folder / 'delivery.json').read_text())
    legacy, study = index['legacy'], index['study']
    for row in (legacy, study):
        require(hashlib.sha256((folder / row['archive']).read_bytes()).hexdigest() == row['sha256'], '历史正式档发生变化')
    old = json.loads((folder / legacy['expected']).read_text())
    before, old_sections = read_world(folder / legacy['archive'])
    original = actor(before, old['source'])
    known = learned_executions(old_sections)[f"actor:{old['source']}"]
    require(known == old['executions'] and original['saved_items'] == [old['weapon']], '旧枪骑的原执行编号或铁枪记录改变')
    legacy_use = json.loads((folder / legacy['report']).read_text())
    require(legacy_use['passed'] and legacy_use['process'] != old['process'] and legacy_use['damage'] > 0, '缺少旧枪骑实机冷读命中')

    styles = json.loads(all_known.read_text())
    _, learned = read_world(Path(styles['archive']) / 'map.wbox')
    require(styles['passed'] and len(set(styles['executions'])) == 9 and
            styles['executions'] in learned_executions(learned).values(), '九项原战技未由同一正式世界保存')

    expected = json.loads((folder / study['expected']).read_text())
    waiting, _ = read_world(folder / study['archive'])
    request = actor_sections(actor(waiting, expected['sourceId']))['ability_creation']
    require(request == expected['section'] and request['data']['Pending'] and request['data']['Resources'],
            '等待档没有保留当时冻结的完整资料')
    receipt = json.loads((folder / study['report']).read_text())
    require(receipt['passed'] and receipt['process'] != expected['process'] and receipt['damage'] > 0,
            '缺少变式冷读后实际付费命中')
    completed_file = folder / study['completed']
    completed, sections = read_world(completed_file)
    hero = actor(completed, expected['sourceId'])
    owner = actor_sections(hero)
    definition = next(row for row in sections['ability_constructions']['data']['Definitions'] if row['Id'] == receipt['ability'])
    predicted = {**expected['expected'], 'Id': receipt['ability']}
    require(without_null_members(definition) == without_null_members(predicted), '正式成品的动作、身体要求、来历或费用与原结果不同')
    require(definition['Body']['Keys'] != request['data']['Source']['Body']['Keys'] and
            definition['Parent']['Id'] == request['data']['Source']['Id'], '变式缺少真实动作变化或来源')
    require(owner['ability_creation']['data']['LastAbility'] == receipt['ability'] and
            not owner['ability_creation']['data']['Pending'] and
            owner['ability_knowledge']['data']['Ids'].count(receipt['ability']) == 1 and
            hero['saved_items'] == [expected['weapon']], '成品和原铁枪未与同一修士保存')
    return completed_file, {'source': expected['sourceId'], 'weapon': expected['weapon'], 'known': expected['known'] + 1,
                            'definition': predicted, 'original': expected['section']['data']['Source']['Id'],
                            'legacyUse': legacy_use, 'studyUse': receipt, 'knownExecutions': styles['executions']}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archives', type=Path, required=True, help='10.03 骑士存读交付目录')
    parser.add_argument('--all-known', type=Path, required=True, help='已有全流派正式复验结果')
    parser.add_argument('--audit-only', action='store_true', help='只复核已有正式记录')
    args = parser.parse_args()
    completed, expected = check_existing(args.archives.resolve(), args.all_known.resolve())
    if args.audit_only:
        print(json.dumps({'passed': True, 'completed': str(completed), **{k: v for k, v in expected.items() if k != 'definition'}},
                         ensure_ascii=False))
        return
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请在独立游戏中使用 dev.py exec')
    import baseline_scenarios as baseline

    out = Path(tempfile.mkdtemp(prefix='martial-complete-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    raw = completed.read_bytes()
    world = out / 'world'
    world.mkdir()
    shutil.copyfile(completed, world / 'map.wbox')
    write_json(out / 'expected.json', expected)
    script = out / 'input.script'
    script.write_text(Path(__file__).with_suffix('.script').read_text().replace('"$OUTPUT"', json.dumps(str(out))))
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '70000')
        require(result.get('passed') and completed.read_bytes() == raw and (world / 'map.wbox').read_bytes() == raw,
                '成品档复验失败或原文件发生改写')
    except RuntimeError as error:
        write_json(out / 'failure.json', {'passed': False, 'error': str(error)})
        raise
    result['sourceSha256'] = hashlib.sha256(raw).hexdigest()
    write_json(out / 'result.json', result)
    print(out)


if __name__ == '__main__':
    main()
