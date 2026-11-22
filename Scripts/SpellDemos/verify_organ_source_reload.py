#!/usr/bin/env python3
"""重复读取正式旧世界，核对器官授予的实际容器和原保存引用。"""
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import zlib

from common import write_json
from verify_weapon_flight_archive import world_helpers


def source_records(map_file):
    world = json.loads(zlib.decompress(map_file.read_bytes()))
    custom = world['mapStats']['custom_data']['custom_data_string']
    objects = json.loads(custom['cultiway.world'])['sections']['objects']['data']['Objects']
    return [(record['Id'], record['GrantedSkillAsset'], record['GrantedSkillAnimation'],
             record['OrganGrantedSkill']) for record in objects if record.get('GrantedSkillAsset')]


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')
    import baseline_scenarios as baseline

    out = Path(tempfile.mkdtemp(prefix='organ-source-reload-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    source = Path('Scripts/WorldSaveTests/Fixtures/world-format-1').resolve()
    world = out / 'input-world'
    shutil.copytree(source, world)
    helpers = world_helpers(out, world, restore=True)
    helpers = helpers[:helpers.index('for (int x = 60; x <= 75; x++)')]
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\n'
               'using Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Content.CreatureCompositions.ActiveAbilities;\n')
    script = out / 'input.script'
    script.write_text(imports + helpers + Path(__file__).with_suffix('.script').read_text())
    write_json(out / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '115000')
        original = source_records(source / 'map.wbox')
        restored = source_records(out / 'archive/map.wbox')
        if not result.get('passed') or len(original) != 1 or restored != original:
            raise RuntimeError('器官来源和正式存档引用不一致')
        raw = (source / 'map.wbox').read_bytes()
        if (world / 'map.wbox').read_bytes() != raw:
            raise RuntimeError('读取改写了输入世界')
    except RuntimeError as error:
        write_json(out / 'failure.json', {'passed': False, 'error': str(error)})
        raise

    result['sourceRecords'] = restored
    result['originalSha256'] = hashlib.sha256(raw).hexdigest()
    write_json(out / 'result.json', result)
    print(out)


if __name__ == '__main__':
    main()
