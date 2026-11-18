#!/usr/bin/env python3
"""录制实际修复、讯号送达、条件恢复及解除。"""
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
CASE = 'repair-and-warning'


def record(folder, worldfall, state, signal_definitions):
    import baseline_scenarios as baseline

    connection = baseline.connect(False, validate_runtime=False)
    installed = baseline.call('eval', '''return new {
        worldfall = System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(), a => a.GetType("FirstPerson.WorldBoxMod", false) != null),
        renderer = UnityEngine.SystemInfo.graphicsDeviceName };''')
    if installed['worldfall'] != worldfall:
        raise RuntimeError('实际加载的模组与指定视角不符')
    result = json.loads((state / 'result.json').read_text())
    if not result.get('passed') or not result.get('learnedId'):
        raise RuntimeError('需要 E2 已通过的实际学会并重存结果')
    raw = (state / 'completed-save/map.wbox').read_bytes()
    world = json.loads(zlib.decompress(raw))
    defs = json.loads(world['mapStats']['custom_data']['custom_data_string']['cultiway.world'])['sections']['ability_constructions']['data']
    definitions = {item['Id']: item for item in defs['Definitions']}
    learned = definitions[result['learnedId']]
    signals = json.loads(signal_definitions.read_text())
    folder.mkdir(parents=True, exist_ok=True)
    (folder / 'frames').mkdir(exist_ok=True)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    write_json(folder / 'definition.json', dict(learned=learned, signals=signals))
    write_json(folder / 'metadata.json', dict(connection=connection, installed=installed, version=version_stamp()[0],
                                             source=str(state), mapSha256=hashlib.sha256(raw).hexdigest(),
                                             signalSha256=hashlib.sha256(signal_definitions.read_bytes()).hexdigest()))
    helpers = (SCRIPTS / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "application-films", "' +
                              ('worldfall' if worldfall else 'native') + '", "' + CASE + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until',
                              '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    inventory = (SCRIPTS / 'inventory_case.script').read_text()
    material = 'Entity Material(' + inventory.split('Entity Material(', 1)[1].split('JObject Original(', 1)[0]
    imports = 'using Friflo.Engine.ECS;\nusing Cultiway.Const;\nusing Cultiway.Core.Components;\nusing Cultiway.Utils;\n'
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))', '')
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork."))', '')
    film = film_helpers()
    film = film[:film.index('string Stage()')] + 'string Stage() => resultCaption();\n' + film[film.index('void Capture()'):]
    script = folder / 'input.script'
    script.write_text(imports + helpers + '\n' + material + film + '\n' + (SCRIPTS / 'record_application.script').read_text())
    try:
        result = baseline.call('run', str(script), '300000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)


def verify_frames(folder):
    """逐帧核对画面中的真实状态与经过。"""
    frames = json.loads((folder / 'recording.json').read_text())['frames']
    labels = ('实际修复', '偏离未送达', '告警实际送达', '有告警', '指定解除', '恢复原规则')
    samples = {}
    for label in labels:
        sample = next((f for f in frames if f['stage'].startswith(label)), None)
        if sample is None:
            raise RuntimeError('原帧缺少阶段：' + label)
        samples[label] = sample
    repair, miss, notice, enhanced, cleared, normal = (samples[label]['detail'] for label in labels)
    if not (repair['repairCount'] == 1 and repair['durability'] > frames[0]['detail']['durability']
            and miss['received'] == 0 and not miss['active'] and notice['received'] == 1 and notice['active']
            and enhanced['active'] and not cleared['active'] and not normal['active']
            and enhanced['gained'] >= 20 and 5 <= normal['gained'] < enhanced['gained']):
        raise RuntimeError('逐帧记录不支持实际作用证据')
    write_json(folder / 'frame-comparison.json', samples)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--state', type=Path, help='E2 的实际学会并重存输出目录')
    parser.add_argument('--signal-definitions', type=Path, help='D 实机保存的作用定义')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--encode-only', type=Path, help='从已有视角目录重新编码')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    if not args.encode_only and (not args.state or not args.signal_definitions or not os.environ.get('CULTIWAY_DEV_ACTIVE')):
        parser.error('请通过 dev.py exec 录制，并提供 E2 的 --state 和 D 的 --signal-definitions')
    output = args.encode_only or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'application-films' / mode
    folder = output / CASE
    if not args.encode_only:
        record(folder, args.worldfall, args.state, args.signal_definitions)
    result = json.loads((folder / 'result.json').read_text())
    if not result.get('passed') or result.get('view') != mode:
        raise RuntimeError(f'未经通过的实机录像：{folder}')
    verify_frames(folder)
    evidence = encode(CASE, folder, output, not args.worldfall, '实物修复与战场告警', '5.17',
                      worldfall_crop='crop=iw*2/3:ih*2/3:iw*0.15:ih*0.10')
    if len(evidence['stages']) < 6 or evidence['maxWorldGap'] > .17:
        raise RuntimeError(f'录像缺少真实阶段或连续世界时间：{folder}')


if __name__ == '__main__':
    main()
