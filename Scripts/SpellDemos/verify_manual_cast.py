#!/usr/bin/env python3
"""从附体能力栏和施放热键复验当前构造版本。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import PROJECT, version_stamp, write_json
from verify_weapon_flight_archive import FIXTURE, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true', help='从实际第一人称准星释放')
    parser.add_argument('--record', action='store_true', help='按真实世界时间录下受理和结束')
    parser.add_argument('--archive', type=Path, help='同一原版录像保存的正式世界输出目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏 dev.py exec 运行')
    if args.archive and not args.worldfall:
        parser.error('在第一人称中读取原版的正式世界')

    import baseline_scenarios as baseline

    sample = PROJECT / 'Doc/技能系统全面重构/构造输入/命名步骤行剑.json'
    previous = args.archive.resolve() if args.archive else None
    if previous and not all((previous / name).is_file() for name in ('archive-expected.json', 'archive/map.wbox')):
        parser.error('缺少原版录像的正式世界或预期资料')

    output = Path(tempfile.mkdtemp(prefix='manual-cast-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    world = PROJECT / FIXTURE
    expected = 'null'
    if previous:
        world = output / 'input-world'
        shutil.copytree(previous / 'archive', world)
        expected_path = json.dumps(str(previous / 'archive-expected.json'))
        expected = f'JObject.Parse(File.ReadAllText({expected_path}))'
    script.write_text('using HarmonyLib;\nusing UnityEngine.UI;\nusing Cultiway.Core.SkillLibV3;\n' +
                      world_helpers(output, world, restore=bool(previous)) +
                      f'\nbool firstPerson = {str(args.worldfall).lower()};\n' +
                      f'bool record = {str(args.record).lower()};\n' +
                      f'JObject archived = {expected};\n' +
                      f'string samplePath = {json.dumps(str(sample), ensure_ascii=False)};\n' +
                      Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '110000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'附体手动施放复验失败，见 {output}') from None
    write_json(output / 'result.json', result)
    if args.record:
        from record_body_connections import encode

        version, _ = version_stamp()
        write_json(output / 'metadata.json', {'version': version})
        view = 'firstperson' if args.worldfall else 'native'
        encode(f'manual-cast-{view}', output, output, not args.worldfall,
               label='成锋行剑手动施放', milestone='10.14 实机记录', crop_worldfall=False)
    print(output)


if __name__ == '__main__':
    main()
