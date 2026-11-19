#!/usr/bin/env python3
"""在一份真实世界中录制两个独立侵蚀过程，复用正式学到的调整术。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import tempfile
import zlib

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode
from record_spatial_mapping import film_helpers

SCRIPTS = Path(__file__).parent
CASE = 'two-processes'


def record(folder, worldfall, state, imprint_check=False, imprint_film=False):
    import baseline_scenarios as baseline

    connection = baseline.connect(False, validate_runtime=False)
    installed = baseline.call('eval', '''return new {
        worldfall = System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(), a => a.GetType("FirstPerson.WorldBoxMod", false) != null),
        renderer = UnityEngine.SystemInfo.graphicsDeviceName };''')
    if not imprint_check and (worldfall or not imprint_film) and installed['worldfall'] != worldfall:
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
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until',
                              '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    film = film_helpers()
    if imprint_film and not worldfall and installed['worldfall']:
        film = film.replace('string view =', """if (mod != null)
{
    object GetView(string key) => external.GetProperty(key, F)?.GetValue(mod) ?? external.GetField(key, F)?.GetValue(mod);
    external.GetMethod("ViewForTest", F).Invoke(mod, new object[] { false });
    external.GetMethod("SetGodView", F).Invoke(mod, new object[] { false });
    await Game.WaitUntil(() => !(bool)GetView("IsFirstPerson") && !(bool)GetView("GodShown") &&
        (bool)GetView("DiveDone") && !(bool)GetView("Flying"), 3600);
    mod = null;
}
string view =""", 1)
    film = film[:film.index('string Stage()')] + 'string Stage() => resultCaption();\n' + film[film.index('void Capture()'):]
    script = folder / 'input.script'
    body = (SCRIPTS / 'record_process_time.script').read_text()
    if imprint_check:
        body = body.replace('const bool imprintCheck = false;', 'const bool imprintCheck = true;')
    script.write_text(helpers + '\n' + film + '\n' + body)
    try:
        result = baseline.call('run', str(script), '300000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)


def verify_frames(folder, imprint_film=False):
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
    if imprint_film and not (a['imprints'] == b['imprints'] and 1 <= a['imprints'] <= 3
            and 1 <= c['imprints'] <= 3 and 1 <= d['imprints'] <= 3 and last['imprints'] == 0
            and any(f['stage'].startswith('两侧分别') and f['detail']['imprints'] == 0 for f in frames)):
        raise RuntimeError('录像没有覆盖暂停、加速和恢复时对应的实际印片')
    write_json(folder / 'frame-comparison.json', dict(pauseFirst=pauses[0], pauseLast=pauses[-1],
               doubleSpeedFirst=accelerated[0], doubleSpeedLast=accelerated[-1], finished=frames[-1]))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--state', type=Path, help='C2 的实际学会并重存输出目录（use 或完整 creation-restart）')
    parser.add_argument('--imprint-check', action='store_true', help='只核对被选材料的时序印片，不录制视频')
    parser.add_argument('--imprint-film', action='store_true', help='录制带时序印片的 9.26 演示，可在已加载 Worldfall 时切回原版')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--encode-only', type=Path, help='从已有视角目录重新编码')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    if not args.encode_only and (not args.state or not os.environ.get('CULTIWAY_DEV_ACTIVE')):
        parser.error('请通过 dev.py exec 录制，并提供 C2 实机通过的 --state')
    if args.imprint_check:
        if args.encode_only or args.worldfall or args.imprint_film:
            parser.error('--imprint-check 复用当前游戏，不使用录像参数')
        folder = Path(tempfile.mkdtemp(prefix='time-imprint-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
        record(folder, False, args.state, imprint_check=True)
        result = json.loads((folder / 'result.json').read_text())
        if not result.get('passed'):
            raise RuntimeError(f'时序印片未通过：{folder}')
        print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))
        return
    output = args.encode_only or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / (
        'time-imprint-films-926' if args.imprint_film else 'process-time-films') / mode
    folder = output / CASE
    if not args.encode_only:
        record(folder, args.worldfall, args.state, imprint_film=args.imprint_film)
    result = json.loads((folder / 'result.json').read_text())
    if not result.get('passed') or result.get('view') != mode:
        raise RuntimeError(f'未经通过的实机录像：{folder}')
    verify_frames(folder, args.imprint_film)
    evidence = encode(CASE, folder, output, not args.worldfall, '借时催枯：两块木料各自计时' if args.imprint_film else '同场两块木料，各自计时',
                      '9.26' if args.imprint_film else '5.16',
                      worldfall_crop='crop=iw*2/3:ih*2/3:iw*0.15:ih*0.10')
    if len(evidence['stages']) < 5 or evidence['maxWorldGap'] > .17:
        raise RuntimeError(f'录像缺少真实阶段或连续世界时间：{folder}')


if __name__ == '__main__':
    main()
