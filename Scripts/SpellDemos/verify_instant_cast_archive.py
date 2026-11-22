#!/usr/bin/env python3
"""用原铁锤对练档核对动作尚未结束时的支付及读档归还。"""
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
    parser.add_argument('--section', required=True, choices=('prepare', 'cold'))
    parser.add_argument('--archive', required=True, type=Path, help='9.45 对练档或本次蓄势存档输出目录')
    parser.add_argument('--film', action='store_true', help='按实际世界时间录制本段原版画面')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')
    import baseline_scenarios as baseline

    original = args.archive.resolve()
    out = Path(tempfile.mkdtemp(prefix=f'instant-cast-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    world = out / 'input-world'
    shutil.copytree(original / 'archive', world)
    helpers = world_helpers(out, world, restore=True)
    placement = helpers[helpers.index('void Place(Actor'):helpers.index('Actor Make(')]
    helpers = helpers[:helpers.index('for (int x = 60; x <= 75; x++)')] + placement
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    script = out / 'input.script'
    camera = Path(__file__).with_name('record_independent_casting_camera.script').read_text()
    camera = camera.replace('    await Advance(.15f);\n', '')
    recording = Path(__file__).with_name('record_archive_stage.script').read_text()
    script.write_text(helpers + f'const bool recordArchive = {str(args.film).lower()};\n' + camera + recording
                      + f'const string section = "{args.section}";\n'
                      + f'string original = {json.dumps(str(original))};\n'
                      + Path(__file__).with_suffix('.script').read_text())
    write_json(out / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '120000')
        source_bytes = (original / 'archive/map.wbox').read_bytes()
        if not result.get('passed') or (world / 'map.wbox').read_bytes() != source_bytes:
            raise RuntimeError('原档被改写或游戏检查失败')
    except RuntimeError as error:
        write_json(out / 'failure.json', {'passed': False, 'error': str(error)})
        raise
    result['originalSha256'] = hashlib.sha256(source_bytes).hexdigest()
    write_json(out / 'result.json', result)
    if args.film:
        from record_body_connections import encode
        encode('battle-' + args.section, out, out, True, label='铁锤存档与冷读', milestone='12.15 实机存读')
    print(out)


if __name__ == '__main__':
    main()
