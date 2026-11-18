#!/usr/bin/env python3
"""5.14 C2原版及Worldfall录制同一正式映像过程，按实际世界时间编码。"""
import argparse
import json
import os
from pathlib import Path
import shutil

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode


def record(state, folder, mode):
    import baseline_scenarios as baseline
    connected = baseline.connect(False, validate_runtime=False)
    installed = baseline.call('eval', 'return new { worldfall=System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType("FirstPerson.WorldBoxMod",false)!=null), renderer=UnityEngine.SystemInfo.graphicsDeviceName };')
    if installed['worldfall'] != (mode == 'worldfall'):
        raise RuntimeError('实际加载视角与录制参数不同')
    if not json.loads((state / 'result.json').read_text()).get('passed'):
        raise RuntimeError('需要真实通过的B3创作成品')
    connected['renderer'] = installed['renderer']
    for name in ('created-definition.json', 'attack-definition.json'):
        shutil.copy2(state / name, folder / name)
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    (folder / 'frames').mkdir(exist_ok=True)
    scripts = Path(__file__).parent
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "correspondence-films/' + mode + '/image")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until', '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    common = (scripts / 'record_delivery.script').read_text()
    fragment = (scripts / 'record_correspondence.script').read_text()
    capture = common[common.index('void Capture()'):common.index('/// <summary>暂停只等待')]
    capture = capture.replace('camera.orthographicSize = scenario == "contact" ? 1.5f : scenario == "orbit" ? 5.2f : scenario == "ground" ? 9f : scenario == "flight" ? 5.5f : 3.5f;', 'camera.orthographicSize = 3.3f;')
    capture = capture.replace('stage = castId == 0 ? "正式施放前" : finished ? "全部实例自然结束" : "同一次真实投送",', 'stage = Stage(),')
    capture = capture.replace('contacts = hitCount,', 'contacts = hits,')
    segment = common[common.index('async Task Record()'):common.index('try\n{\n    World.world.console')]
    camera = common[common.index('    World.world.console.Hide()'):common.index('    ConstructedAbilityService.CastStarted += Started;')]
    camera = camera.replace('scenario == "orbit" ? source : target', 'source').replace('scenario == "orbit" ? source.current_position : target.current_position', 'centre')
    camera = camera.replace('scenario == "contact" ? 3.5f : scenario == "orbit" ? 8.5f : scenario == "ground" ? 15f : scenario == "flight" ? 11f : 6.5f', '4.8f')
    camera = camera.replace('scenario == "ground" ? .65f : .35f', '.35f').replace('"_godTurn", -.25f', '"_godTurn", .1f')
    fragment = fragment.replace('// RECORD_SHARED', capture + segment.replace('1d / 30d', '1d / 15d')).replace('// CAMERA_SHARED', camera)
    script = folder / 'input.script'
    script.write_text('using Friflo.Engine.ECS;\nusing Cultiway.Core.Components;\n' + helpers + '\n' + fragment)
    write_json(folder / 'metadata.json', dict(connection=connected, version=version_stamp()[0], source=str(state),
        adjustments='复用真实衍生成品与攻击；两者颜色区分，映像延长且提高承载力以展示形成和等待，固定原场地近、中、远三个位置。原作用份额不变，按规则29重新报价。'))
    try:
        result = baseline.call('run', str(script), '240000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed') or result.get('view') != mode:
        raise RuntimeError('录制用实际过程未通过')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--state', type=Path, help='B3已通过的creation目录')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--encode-only', type=Path, help='仅编码已有视角输出目录')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    if not args.encode_only and (not args.state or not os.environ.get('CULTIWAY_DEV_ACTIVE')):
        parser.error('请通过develop运行并指定--state')
    output = args.encode_only or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'correspondence-films' / mode
    folder = output / 'image'
    folder.mkdir(parents=True, exist_ok=True)
    if not args.encode_only:
        record(args.state, folder, mode)
    result = json.loads((folder / 'result.json').read_text())
    if not result.get('passed') or result['view'] != mode:
        raise RuntimeError('只有真实通过的原始取景可编码')
    encode('image', folder, output, not args.worldfall, '部位映像与原人物', '5.14', crop_worldfall=False)


if __name__ == '__main__':
    main()
