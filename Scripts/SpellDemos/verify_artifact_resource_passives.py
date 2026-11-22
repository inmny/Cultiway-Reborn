#!/usr/bin/env python3
"""复验持续疗伤实际支付和灵葫账本收放。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, SCRIPTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--skip-views', action='store_true', help='只检查正式行为')
    parser.add_argument('--section', choices=('all', 'healing', 'gourd', 'gourd-save', 'gourd-load'), default='all')
    parser.add_argument('--archive', type=Path, help='gourd-save 的输出目录')
    args = parser.parse_args()
    if (args.section == 'gourd-load') != bool(args.archive):
        parser.error('仅 gourd-load 需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(tempfile.mkdtemp(prefix='artifact-resource-passives-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = PROJECT / FIXTURE
    expected = 'null'
    if args.archive:
        archive = args.archive.resolve()
        world = archive / 'archive'
        with (world / 'map.wbox').open('rb') as save:
            digest = hashlib.file_digest(save, 'sha256').hexdigest()
        saved = json.loads((archive / 'result.json').read_text())
        if not saved['passed'] or digest != (archive / 'archive.sha256').read_text().strip():
            parser.error('储元正式档校验失败')
        expected = f'JObject.Parse({json.dumps(json.dumps(saved))})'
    program = output / 'input.script'
    program.write_text('using strings;\n' + IMPORTS + world_helpers(output, world, restore=bool(args.archive))
                       + (SCRIPTS / 'weapon_flight_scene.script').read_text()
                       + (SCRIPTS / 'verify_artifact_views.script').read_text()
                       + f'const string section = {json.dumps(args.section)};\n'
                       + f'const bool shots = {str(not args.skip_views).lower()};\n'
                       + f'JObject expectedReserve = {expected};\n'
                       + Path(__file__).with_suffix('.script').read_text())
    try:
        result = baseline.call('run', str(program), '170000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'法宝恢复与储元复验记录位于 {output}') from error
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'法宝恢复与储元复验未通过，见 {output}')
    if args.section == 'gourd-save':
        with (output / 'archive/map.wbox').open('rb') as save:
            (output / 'archive.sha256').write_text(hashlib.file_digest(save, 'sha256').hexdigest() + '\n')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
