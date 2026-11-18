#!/usr/bin/env python3
"""在一份真实世界中录制两个独立侵蚀过程，复用正式学到的调整术。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import zlib

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode
from record_spatial_mapping import film_helpers

SCRIPTS = Path(__file__).parent
CASE = 'two-processes'


def record(folder, worldfall, state):
    import baseline_scenarios as baseline

    connection = baseline.connect(False, validate_runtime=False)
    installed = baseline.call('eval', '''return new {
        worldfall = System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(), a => a.GetType("FirstPerson.WorldBoxMod", false) != null),
        renderer = UnityEngine.SystemInfo.graphicsDeviceName };''')
    if installed['worldfall'] != worldfall:
        raise RuntimeError('实际加载的模组与指定视角不符')
    result = json.loads((state / 'result.json').read_text())
    if not result.get('passed') or not result.get('learnedId'):
        raise RuntimeError('需要 C2 已通过的实际学会并重存结果')
    raw = (state / 'completed-save/map.wbox').read_bytes()
    world = json.loads(zlib.decompress(raw))
    defs = json.loads(world['mapStats']['custom_data']['custom_data_string']['cultiway.world'])['sections']['ability_constructions']['data']
    definitions = {item['Id']: item for item in defs['Definitions']}
    learned = definitions[result['learnedId']]
    residue = definitions['test.process-time.creation-wood']
    folder.mkdir(parents=True, exist_ok=True)
    (folder / 'frames').mkdir(exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    write_json(folder / 'definition.json', dict(learned=learned, residue=residue))
    write_json(folder / 'metadata.json', dict(connection=connection, installed=installed, version=version_stamp()[0],
                                             source=str(state), mapSha256=hashlib.sha256(raw).hexdigest()))
    helpers = (SCRIPTS / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "process-time-films", "' +
                              ('worldfall' if worldfall else 'native') + '", "' + CASE + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until',
                              '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    film = film_helpers()
    film = film[:film.index('string Stage()')] + 'string Stage() => resultCaption();\n' + film[film.index('void Capture()'):]
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + film + '\n' + (SCRIPTS / 'record_process_time.script').read_text())
    try:
        result = baseline.call('run', str(script), '300000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)


def verify_frames(folder):
    """从录像自己的原帧核对两个时钟，避免拿单独运行的结果给画面背书。"""
    frames = json.loads((folder / 'recording.json').read_text())['frames']
    pauses = [f for f in frames if f['stage'].startswith('左侧暂停')]
    accelerated = [f for f in frames if f['stage'].startswith('左侧以两倍推进')]
    if len(pauses) < 2 or len(accelerated) < 2:
        raise RuntimeError('原帧未覆盖可对比的暂停、加速区间')
    a, b = (f['detail'] for f in (pauses[0], pauses[-1]))
    c, d = (f['detail'] for f in (accelerated[0], accelerated[-1]))
    last = frames[-1]['detail']
    if not (a['leftClock'] == b['leftClock'] and a['leftVolume'] == b['leftVolume']
            and a['rightClock'] < b['rightClock'] and a['rightVolume'] > b['rightVolume']
            and b['leftClock'] < c['leftClock'] < d['leftClock']
            and abs((d['leftClock'] - c['leftClock']) - 2 * (d['rightClock'] - c['rightClock'])) < .002
            and last['leftProgress'] == last['rightProgress'] == 1):
        raise RuntimeError('原帧记录不能证明两份材料各自推进并真实完成')
    write_json(folder / 'frame-comparison.json', dict(pauseFirst=pauses[0], pauseLast=pauses[-1],
               doubleSpeedFirst=accelerated[0], doubleSpeedLast=accelerated[-1], finished=frames[-1]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--state', type=Path, help='C2 的实际学会并重存输出目录（use 或完整 creation-restart）')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--encode-only', type=Path, help='从已有视角目录重新编码')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    if not args.encode_only and (not args.state or not os.environ.get('CULTIWAY_DEV_ACTIVE')):
        parser.error('请通过 dev.py exec 录制，并提供 C2 实机通过的 --state')
    output = args.encode_only or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'process-time-films' / mode
    folder = output / CASE
    if not args.encode_only:
        record(folder, args.worldfall, args.state)
    result = json.loads((folder / 'result.json').read_text())
    if not result.get('passed') or result.get('view') != mode:
        raise RuntimeError(f'未经通过的实机录像：{folder}')
    verify_frames(folder)
    evidence = encode(CASE, folder, output, not args.worldfall, '同场两块木料，各自计时', '5.16',
                      worldfall_crop='crop=iw*2/3:ih*2/3:iw*0.15:ih*0.10')
    if len(evidence['stages']) < 5 or evidence['maxWorldGap'] > .17:
        raise RuntimeError(f'录像缺少真实阶段或连续世界时间：{folder}')


if __name__ == '__main__':
    main()
