#!/usr/bin/env python3
"""从一次自然修炼录到自动学会、自主施放，再从同一存档重启复放。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, encode, write_json
from verify_weapon_flight_archive import FIXTURE, SCRIPTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--stage', choices=('prepare', 'cold'), required=True)
    parser.add_argument('--archive', type=Path, help='prepare 输出目录，仅供冷启动阶段读取')
    args = parser.parse_args()
    if (args.stage == 'cold') != bool(args.archive):
        parser.error('只有 cold 需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立原版游戏 dev.py exec 运行')

    import baseline_scenarios as baseline

    output = Path(tempfile.mkdtemp(prefix=f'natural-creation-{args.stage}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    print(output, flush=True)
    archive = args.archive.resolve() if args.archive else None
    world = archive / 'archive' if archive else PROJECT / FIXTURE
    map_bytes = (world / 'map.wbox').read_bytes()
    if archive:
        previous = json.loads((archive / 'result.json').read_text(encoding='utf-8'))
        if not previous['passed'] or previous['savedMapSha256'] != hashlib.sha256(map_bytes).hexdigest():
            raise ValueError('冷启动需要原封未改的正式保存结果')

    helpers = world_helpers(output, world, restore=bool(archive))
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    if archive:
        # 读正式档前先登记录制中用于场地管理的状态，状态本身会随世界保存。
        load_at = helpers.index('Game.Resume();')
        tile_at = helpers.index('for (int x = 60;')
        hold_at = helpers.index('var holdStats')
        place_at = helpers.index('void Place(')
        helpers = helpers[:load_at] + helpers[hold_at:place_at] + helpers[load_at:tile_at] + helpers[place_at:]
    camera = (SCRIPTS / 'verify_wind_combat.script').read_text()
    camera = camera[camera.index('void HideCameraOverlays()'):camera.index('async Task<List<double>> Film')]
    header = '''
using Cultiway.Content.Libraries;
using Cultiway.Core.SkillLibV3;
using Cultiway.UI;
'''
    settings = '\nstring previousRecording = ' + json.dumps(str(archive) if archive else '') + ';\n'
    script = output / 'input.script'
    script.write_text(header + helpers + settings + camera + Path(__file__).with_suffix('.script').read_text(),
                      encoding='utf-8')

    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    result['inputMapSha256'] = hashlib.sha256(map_bytes).hexdigest()
    if not archive:
        result['savedMapSha256'] = hashlib.sha256((output / 'archive/map.wbox').read_bytes()).hexdigest()
    write_json(output / 'result.json', result)
    for clip in result['clips']:
        folder = output / clip['key']
        write_json(folder / 'result.json', clip)
        encode(output, dict(key=clip['key'], name=clip['title'], title=clip['title'],
                            label='原版', variant=0, trajectory='', modifiers=[]), keep_frames=True)


if __name__ == '__main__':
    main()
