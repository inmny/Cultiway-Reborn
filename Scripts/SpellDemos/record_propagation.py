#!/usr/bin/env python3
"""5.18：用已验收的地波连锁、正式创作的水路成品录制两个视角。"""
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
CLIPS = {'ground': '足底发力与地波连锁', 'water': '循水放电与到达换材'}


def record(case, mode, folder, installed, connection, baseline, args):
    if case == 'ground':
        origin = args.ground
        raw = origin.read_bytes()
        definition = json.loads(raw)
    else:
        state = args.state
        result = json.loads((state / 'result.json').read_text())
        if not result.get('passed') or not result.get('learnedId'):
            raise RuntimeError('请使用 D2 已通过的重存成品')
        origin = state / 'completed-save/map.wbox'
        raw = origin.read_bytes()
        world = json.loads(zlib.decompress(raw))
        saved = json.loads(world['mapStats']['custom_data']['custom_data_string']['cultiway.world'])
        definitions = saved['sections']['ability_constructions']['data']['Definitions']
        definition = next(entry for entry in definitions if entry['Id'] == result['learnedId'])
    (folder / 'frames').mkdir(parents=True, exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    write_json(folder / 'definition.json', definition)
    write_json(folder / 'metadata.json', dict(connection=connection, installed=installed, version=version_stamp()[0],
                                             source=str(origin), sourceSha256=hashlib.sha256(raw).hexdigest()))
    helpers = (SCRIPTS / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    target = f'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "propagation-films", "{mode}", "{case}")'
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', target)
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until',
                              '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))', '')
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork."))', '')
    film = film_helpers()
    film = film[:film.index('string Stage()')] + 'string Stage() => resultCaption();\n' + film[film.index('void Capture()'):]
    film = film.replace('4.6f', '5.8f' if case == 'ground' else '6.8f').replace('6.6f', '8.1f' if case == 'ground' else '9.1f')
    script = folder / 'input.script'
    script.write_text(helpers + f'\nstring clip = "{case}";\n' + film + '\n' + (SCRIPTS / 'record_propagation.script').read_text())
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)


def verify_frames(case, folder):
    frames = json.loads((folder / 'recording.json').read_text())['frames']
    labels = ('足底触地', '近段生长', '同刻换材', '远段到达', '自然结束', '主动取消') if case == 'ground' else (
        '等待触水', '首轮真实到达', '第二轮脉冲', '自然结束')
    samples = {}
    for label in labels:
        sample = next((frame for frame in frames if frame['stage'].startswith(label)), None)
        if sample is None:
            raise RuntimeError(f'录像缺少 {label}：{folder}')
        samples[label] = {key: sample[key] for key in ('time', 'frame', 'source', 'target', 'detail')}
    evidence = {label: frame['detail'] for label, frame in samples.items()}
    if case == 'ground':
        touch, near, converted, far, ended, cancelled = (evidence[label] for label in labels)
        valid = (touch['touched'] and touch['near'] is None and near['near'] is not None and
                 converted['restored'] == 20 and converted['material'] == 'iron' and far['far'] != far['near'] and
                 ended['ended'] and ended['near'] is None and cancelled['cancelled'] and cancelled['near'] is None and
                 cancelled['restored'] == 0)
    else:
        waiting, first, second, ended = (evidence[label] for label in labels)
        valid = (waiting['pulses'] == 0 and waiting['near'] is None and first['route'] > 1 and first['near'] is not None and
                 first['material'] == 'wood' and second['pulses'] == 2 and second['material'] == 'iron' and ended['ended'])
    write_json(folder / 'frame-comparison.json', samples)
    if not valid:
        raise RuntimeError(f'画面所在时点与实际作用记录不符：{folder}')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=(*CLIPS, 'all'), default='all')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--state', type=Path, help='D2通过后的正式重存目录')
    parser.add_argument('--ground', type=Path, default=Path.home() / '.local/state/cultiway-dev/evidence/propagation-chain-c/passed/chain-definition.json')
    parser.add_argument('--encode-only', type=Path, help='从指定视角的原始帧重编码')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    cases = CLIPS if args.case == 'all' else (args.case,)
    if not args.encode_only:
        if not os.environ.get('CULTIWAY_DEV_ACTIVE') or ('water' in cases and not args.state):
            parser.error('通过 dev.py exec 录制；水路需要 --state 指向 D2 正式重存目录')
        import baseline_scenarios as baseline
        connection = baseline.connect(False, validate_runtime=False)
        installed = baseline.call('eval', 'return new { worldfall=System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType("FirstPerson.WorldBoxMod",false)!=null), renderer=UnityEngine.SystemInfo.graphicsDeviceName };')
        if installed['worldfall'] != args.worldfall:
            raise RuntimeError('实际安装与指定视角不符')
    output = args.encode_only or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'propagation-films' / mode
    for case in cases:
        folder = output / case
        if not args.encode_only:
            record(case, mode, folder, installed, connection, baseline, args)
        result = json.loads((folder / 'result.json').read_text())
        if not result.get('passed') or result.get('view') != mode:
            raise RuntimeError(f'实机录像未通过：{folder}')
        verify_frames(case, folder)
        encode(case, folder, output, not args.worldfall, CLIPS[case], '5.18', crop_worldfall=False)


if __name__ == '__main__':
    main()
