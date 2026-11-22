#!/usr/bin/env python3
"""在独立游戏中复验建筑修缮及另一进程的正式读回。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

from common import write_json
from verify_weapon_flight_archive import world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('repair', 'cold'), required=True)
    parser.add_argument('--archive', type=Path, required=True, help='10.13 功能术准备目录，或 repair 的输出目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    archive = args.archive.resolve()
    original = archive / 'archive'
    with (original / 'map.wbox').open('rb') as save:
        digest = hashlib.file_digest(save, 'sha256').hexdigest()
    expected = json.loads((archive / 'result.json').read_text())
    if not expected['passed'] or digest != (archive / 'archive.sha256').read_text().strip():
        parser.error('正式档或结果未通过校验')

    folder = Path(tempfile.mkdtemp(prefix=f'building-repair-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = folder / 'input-world'
    shutil.copytree(original, world)
    helpers = world_helpers(folder, world, restore=True)
    before, _, rest = helpers.partition('for (int x = 60; x <= 75; x++)')
    helpers = (before + rest[rest.index('var holdStats'):]).replace('"test.body-footwork."', '"test.building-repair"')
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + f'string section = "{args.section}";\n'
                      + f'JObject expected = JObject.Parse({json.dumps(json.dumps(expected))});\n'
                      + Path(__file__).with_suffix('.script').read_text())

    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '135000')
    except RuntimeError as error:
        write_json(folder / 'failure.json', {'passed': False, 'error': str(error)})
        raise RuntimeError(f'修缮复验失败，见 {folder}') from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'修缮复验未通过，见 {folder}')
    if args.section == 'repair':
        with (folder / 'archive/map.wbox').open('rb') as save:
            (folder / 'archive.sha256').write_text(hashlib.file_digest(save, 'sha256').hexdigest() + '\n')
    print(folder)


if __name__ == '__main__':
    main()
