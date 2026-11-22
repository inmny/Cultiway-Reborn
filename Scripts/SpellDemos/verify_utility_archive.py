#!/usr/bin/env python3
"""复验旧功能术的正式存读、万法阁和书籍。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import zlib

from common import write_json
from verify_weapon_flight_archive import world_helpers


def removed_test_statuses(world):
    saved = json.loads(zlib.decompress((world / 'map.wbox').read_bytes()))
    pack = json.loads(saved['mapStats']['custom_data']['custom_data_string']['cultiway.world'])
    objects = pack['sections']['objects']['data']['Objects']
    removed = []
    for record in objects:
        parts = json.loads(record.get('Parts') or '{}').get('sections', {})
        value = parts.get('status', {}).get('data', {}).get('Value', {})
        if value.get('id') in ('Cultiway.UtilityMigrationHold', 'Cultiway.Verify.SourceLoss.Hold'):
            removed.extend((f"{record['Id']} / status：状态定义已移除",
                            f"status / 世界物件：引用对象未能恢复：{record['Id']}"))
    return removed


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('prepare', 'cold'), required=True)
    origin = parser.add_mutually_exclusive_group(required=True)
    origin.add_argument('--legacy', type=Path, help='原功能法术交付目录')
    origin.add_argument('--archive', type=Path, help='本入口准备阶段的输出目录')
    args = parser.parse_args()
    if (args.case == 'prepare') != bool(args.legacy):
        parser.error('prepare 使用 --legacy，cold 使用 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    source = (args.legacy or args.archive).resolve()
    world = source / ('save' if args.legacy else 'archive')
    record = source / ('restart-expected.json' if args.legacy else 'result.json')
    previous = json.loads(record.read_text())
    with (world / 'map.wbox').open('rb') as save:
        digest = hashlib.file_digest(save, 'sha256').hexdigest()
    if args.archive:
        if not previous['passed'] or digest != (source / 'archive.sha256').read_text().strip():
            parser.error('准备阶段的正式世界校验未通过')
    output = Path(tempfile.mkdtemp(prefix=f'utility-archive-{args.case}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    input_world = output / 'input-world'
    shutil.copytree(world, input_world)
    imports = ('using Friflo.Engine.ECS;\nusing Cultiway.Core.SkillLibV3;\n'
               'using Cultiway.Core.SkillLibV3.Components;\nusing Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Blueprints;\n'
               'using Cultiway.Core.SkillLibV3.Wanfa;\nusing Cultiway.Content.Components.Skill;\n'
               'using Cultiway.Content.Extensions;\n')
    script = output / 'input.script'
    setup = f'\nstring stage = "{args.case}";\nJObject expected = JObject.Parse(File.ReadAllText({json.dumps(str(record))}));\n'
    known_skips = removed_test_statuses(input_world)
    setup += f'JArray allowedSkips = JArray.Parse({json.dumps(json.dumps(known_skips, ensure_ascii=False), ensure_ascii=False)});\n'
    script.write_text(imports + world_helpers(output, input_world, restore=True) + setup +
                      Path(__file__).with_suffix('.script').read_text())

    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'功能术存读复验失败，见 {output}') from None
    if args.legacy:
        with (output / 'archive' / 'map.wbox').open('rb') as save:
            (output / 'archive.sha256').write_text(hashlib.file_digest(save, 'sha256').hexdigest() + '\n')
    result['inputSha256'] = digest
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'功能术存读未通过，见 {output}')
    print(output)


if __name__ == '__main__':
    main()
