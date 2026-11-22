#!/usr/bin/env python3
"""复验设施来源的正式档、短时充能退出和重新通行。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    choice = parser.add_mutually_exclusive_group(required=True)
    choice.add_argument('--nodes-result', type=Path, help='同一原版进程中已通过的节点复验 result.json')
    choice.add_argument('--archive', type=Path, help='已完成的保存阶段输出目录')
    parser.add_argument('--recovery-checkpoint', type=Path, help='继续修复前保留的同一场景，仅用于准备阶段')
    parser.add_argument('--film', action='store_true', help='录制正式冷读后的原版重新通行')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请在独立游戏中使用 dev.py exec')
    cold = bool(args.archive)
    if cold and args.recovery_checkpoint:
        parser.error('冷读直接使用最终正式档')
    if args.film and not cold:
        parser.error('录像使用已通过的正式阵门档')
    section = 'cold' if cold else 'prepare'
    output = Path(tempfile.mkdtemp(prefix=f'facility-archive-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    prepared = args.archive.resolve() if cold else output
    archive = prepared / 'archive' / 'map.wbox'
    source = prepared / 'expected.json' if cold else args.nodes_result.resolve()
    if cold:
        expected = json.loads((prepared / 'result.json').read_text())
        if not expected['passed'] or hashlib.sha256(archive.read_bytes()).hexdigest() != expected['sha256']:
            parser.error('冷读须使用同一份未改动的正式档')
    elif not json.loads(source.read_text())['passed']:
        parser.error('须接续已通过的元神台场景')

    fixture = PROJECT / FIXTURE
    world = archive.parent if cold else (args.recovery_checkpoint or fixture).resolve()
    if args.film:
        world = output / 'input-world'
        shutil.copytree(archive.parent, world)
    loading = cold or bool(args.recovery_checkpoint)
    helpers = world_helpers(output, world, restore=True)
    first = helpers.index('for (int x = 60;' if loading else 'World.world.save_manager.loadWorld(fixture);')
    helpers = helpers[:first] + helpers[helpers.index('var holdStats =', first):]
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    camera = Path(__file__).with_name('record_independent_casting_camera.script').read_text()
    camera = camera.replace('    await Advance(.15f);\n', '')
    recording = Path(__file__).with_name('record_archive_stage.script').read_text()
    script = output / 'input.script'
    script.write_text('using Cultiway;\nusing Cultiway.Const;\nusing strings;\n' + IMPORTS +
                      'using Cultiway.Core.Pathfinding;\nusing Cultiway.Content.Systems.Logic;\n' +
                      helpers + f'\nconst bool cold = {str(cold).lower()};\n' +
                      f'const bool recoveredWorld = {str(bool(args.recovery_checkpoint)).lower()};\n' +
                      f'var saved = JObject.Parse(File.ReadAllText({json.dumps(str(source))}));\n' +
                      f'var originalFixture = {json.dumps(str(fixture))};\n' +
                      f'const bool recordArchive = {str(args.film).lower()};\n' + camera + recording +
                      Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'设施正式档复验见 {output}') from error
    result['sha256'] = hashlib.sha256(archive.read_bytes()).hexdigest()
    write_json(output / 'result.json', result)
    if args.film:
        if not result['passed'] or result['sha256'] != expected['sha256'] or (world / 'map.wbox').read_bytes() != archive.read_bytes():
            raise RuntimeError('录像检查未通过或输入世界发生改变')
        from record_body_connections import encode
        encode('facility', output, output, True, label='阵门读回与再次通行', milestone='12.15 实机存读')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
