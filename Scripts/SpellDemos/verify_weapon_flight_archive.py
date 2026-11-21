#!/usr/bin/env python3
"""复验御器活动中保存、换世界、冷启动读回和迁移前飞剑档。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import zlib

from common import PROJECT, write_json

SCRIPTS = Path(__file__).parent
FIXTURE = 'Scripts/WorldSaveTests/Fixtures/world-format-1'
IMPORTS = ('using Friflo.Engine.ECS;\nusing Cultiway.Content.Artifacts;\nusing Cultiway.Content.Artifacts.Baibao;\n'
           'using Cultiway.Content.Extensions;\nusing Cultiway.Content.Libraries;\n'
           'using Cultiway.Core.Components;\nusing Cultiway.Core.SkillLibV3;\n'
           'using Cultiway.Core.SkillLibV3.Components;\n')


def world_helpers(output, world, restore=False):
    helpers = (SCRIPTS / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    helpers = helpers.replace(f'Path.Combine(root, "{FIXTURE}")', json.dumps(str(world)))
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    if restore:
        # 读回时先核对恢复报告与原件，随后再冻结对练对象。
        begin = helpers.index('foreach (Actor a in World.world.units.getSimpleList().ToArray())')
        helpers = helpers[:begin] + helpers[helpers.index('Actor Make(', begin):]
    return helpers


def legacy_record(archive):
    result = json.loads((archive.parent / 'result.json').read_text())
    if not result['passed']:
        raise ValueError('旧档应来自已通过的正式法宝搬运验证')
    map_data = (archive / 'map.wbox').read_bytes()
    saved = json.loads(zlib.decompress(map_data))
    sections = json.loads(saved['mapStats']['custom_data']['custom_data_string']['cultiway.world'])
    objects = {row['Id']: json.loads(row['Parts'])['sections']
               for row in sections['sections']['objects']['data']['Objects']}
    identity = 'object:' + result['artifact']
    owners = [(int(key.split(':')[1]), row) for key, parts in objects.items() if key.startswith('actor:')
              for row in parts.get('link.equipped', {}).get('data', {}).get('Values', [])
              if row['artifact'] == identity]
    (source, relation), = owners
    inventory = objects[f'actor:{source}']['link.inventory']['data']['Values']
    if not any(row['item'] == identity for row in inventory):
        raise ValueError('旧档持有人没有原件')
    # 旧搬运场景保留的对练对象。
    target = 57
    actors = {row['id']: row['asset_id'] for row in saved['actors_data']}
    if actors.get(source) != 'human' or actors.get(target) != 'wolf':
        raise ValueError('旧档缺少原对练人物')
    item = objects[identity]
    return dict(process=result['process'], source=source, target=target, mapSha256=hashlib.sha256(map_data).hexdigest(),
                artifacts=[dict(identity=identity.removeprefix('object:'), settings=item['artifact.abilities']['data']['Value'],
                                appearance=item['artifact.appearance']['data']['Value'],
                                runtime=item['artifact.runtime']['data']['Value'], relation=relation)])


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('prepare', 'cold', 'legacy'), required=True)
    parser.add_argument('--archive', type=Path, help='prepare 的输出目录，或旧搬运验证的 variant-save 目录')
    args = parser.parse_args()
    if (args.section == 'prepare') == bool(args.archive):
        parser.error('仅在 cold 和 legacy 阶段提供 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    output = Path(tempfile.mkdtemp(prefix=f'weapon-flight-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    if args.section == 'prepare':
        world = PROJECT / FIXTURE
        record = None
    elif args.section == 'cold':
        world = args.archive.resolve() / 'archive'
        record = args.archive.resolve() / 'expected.json'
    else:
        world = args.archive.resolve()
        record = output / 'legacy-expected.json'
        write_json(record, legacy_record(world))

    if not (world / 'map.wbox').is_file() or (record and not record.is_file()):
        parser.error(f'找不到正式世界或预期资料：{world}')

    settings = f'\nconst string section = "{args.section}";\n'
    settings += f'JObject saved = JObject.Parse(File.ReadAllText({json.dumps(str(record))}));\n' if record else 'JObject saved = null;\n'
    script = output / 'input.script'
    shared = (SCRIPTS / 'weapon_flight_scene.script').read_text()
    script.write_text(IMPORTS + world_helpers(output, world, restore=bool(record)) + shared + settings
                      + Path(__file__).with_suffix('.script').read_text())

    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'御器存档复验记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'御器存档复验未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
