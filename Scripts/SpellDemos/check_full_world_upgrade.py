"""对照完整世界，检查技能升级没有改变其他玩法事实。"""
import json
import zlib


def read_world(path):
    data = json.loads(zlib.decompress(path.read_bytes()))
    stored = data['mapStats']['custom_data']['custom_data_string']['cultiway.world']
    return data, json.loads(stored)['sections']


def check_same(before, after, label):
    if before != after:
        raise RuntimeError(f'技能升级改变了 {label}')


def actor_sections(actor):
    saved = actor.get('custom_data_string', {}).get('cultiway.actor', '{}')
    return json.loads(saved).get('sections', {})


def object_parts(record, retired):
    parts = json.loads(record['Parts'])['sections']
    parts.pop('status.particle', None)
    if 'cultibook.practice' in parts:
        parts['cultibook.practice']['data']['Value'].setdefault('Constructions', {})
    if 'status' in parts:
        status = parts['status']['data']['Value']
        source = status.get('Source') or {}
        if source.get('Kind') == 'actor':
            status.setdefault('SourceActorId', source['Id'])
    if retired and 'link.spirit-avatar' in parts:
        values = parts['link.spirit-avatar']['data']['Values']
        values[:] = [link for link in values if link['avatar'] != retired]
        if not values:
            parts.pop('link.spirit-avatar')
    return parts


def definitions(records):
    sections = json.loads(records['Definitions'])['sections']
    for book in sections['dynamic']['data']['Cultibooks']:
        book.setdefault('ConstructionPool', [])
    return sections


def check_full_world_upgrade(source, saved, retired_actor=None):
    original, old_sections = read_world(source)
    restored, current_sections = read_world(saved)
    before = {row['id']: row for row in original['actors_data']}
    after = {row['id']: row for row in restored['actors_data']}
    retired = f'actor:{retired_actor}' if retired_actor is not None else None
    if retired_actor is not None:
        before.pop(retired_actor)
    check_same(before.keys(), after.keys(), '原版人物编号')
    native_fields = ('asset_id', 'subspecies', 'x', 'y', 'health', 'mana', 'stamina', 'nutrition', 'saved_traits')
    for identity, actor in before.items():
        check_same([actor.get(key) for key in native_fields], [after[identity].get(key) for key in native_fields],
                   f'人物 {identity} 的属性与资源')
        check_same(actor_sections(actor), actor_sections(after[identity]), f'人物 {identity} 的模组进度')
    for key in ('tileMap', 'tileArray', 'tileAmounts', 'tiles', 'buildings', 'cities', 'books', 'items'):
        check_same(original[key], restored[key], f'原版世界资料 {key}')

    for key, section in old_sections.items():
        if key != 'objects':
            check_same(section, current_sections.get(key), f'世界区段 {key}')
    original_objects = old_sections['objects']['data']
    restored_objects = current_sections['objects']['data']
    old_objects = {item['Id']: item for item in original_objects['Objects'] if item['Id'] != retired}
    new_objects = {item['Id']: item for item in restored_objects['Objects']}
    check_same(old_objects.keys(), new_objects.keys(), '模组对象编号')
    for identity, record in old_objects.items():
        current = new_objects[identity]
        check_same(object_parts(record, retired), object_parts(current, None), f'物件 {identity} 的组件与关系')
        for key, value in record.items():
            if key not in ('Parts', 'Skill'):
                check_same(value, current.get(key), f'物件 {identity} 的 {key}')
    check_same(definitions(original_objects), definitions(restored_objects), '动态功法、丹方及血脉')
    check_same(original_objects['MagicWeb'], restored_objects['MagicWeb'], '原技能池关系')
    return {'actors': len(after), 'objects': len(new_objects), 'worldSections': len(old_sections),
            'subworlds': len(current_sections['subworlds']['data']['Instances']), 'retiredActor': retired_actor}
