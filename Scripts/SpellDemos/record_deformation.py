#!/usr/bin/env python3
"""5.09交付录像，复用已经通过的形变构造及共同连续录像。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess

from common import PROJECT, version_stamp, write_json
from record_body_connections import encode

CASES = dict(branch='枝体收拢再展开', surface='曲面弯折与附属同步', stopped='接触中止形变')


def record(case, state, folder, baseline, connected):
    """沿已验证的正式施放与相机录像入口，不建立另一套动作或作用范围。"""
    original = state / 'definition.json'
    if not json.loads((state / 'result.json').read_text()).get('passed'):
        raise RuntimeError('需要已经通过的正式形变场景')
    shutil.copy2(original, folder / 'input-definition.json')
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    (folder / 'frames').mkdir(exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "deformation-films/' + folder.parent.name + '/' + case + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until',
                              '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    fragment = Path(__file__).with_name('record_delivery.script').read_text()
    fragment = fragment.replace('const string scenario = "DELIVERY_CASE";', 'const string scenario = "' + case + '";')
    fragment = fragment.replace('var definition = Delivery(scenario);',
                                'var definition = JsonConvert.DeserializeObject<AbilityDefinition>(File.ReadAllText(Path.Combine(folder, "input-definition.json")));')
    fragment = fragment.replace('Actor source = Make(64), target = Make(68), foe = Make(12, "wolf"); target.setKingdom(foe.kingdom);',
                                'Actor source = Make(64), target = Make(72, "wolf"), aimActor = Make(72, "wolf");\nsource.addAggro(target); target.addAggro(source);')
    placement = 'Place(source, 64); Place(target, 72);'
    fragment = fragment.replace('DeliveryPositions(source, target, scenario);', placement)
    # Worldfall的close小于10才进入近景，其数值与地图镜头距离分别维护。
    centre, zoom, close = dict(branch=(66.8, 4.2, 5.0), surface=(65.0, 1.6, 2.5), stopped=(66.8, 4.2, 5.0))[case]
    fragment = fragment.replace('Vector2 centre = scenario == "orbit" ? source.current_position : (source.current_position + target.current_position) * .5f;',
                                f'Vector2 centre = new Vector2({centre}f, 72f);')
    fragment = fragment.replace('camera.orthographicSize = scenario == "contact" ? 1.5f : scenario == "orbit" ? 5.2f : scenario == "ground" ? 9f : scenario == "flight" ? 5.5f : 3.5f;',
                                f'camera.orthographicSize = {zoom}f;')
    fragment = fragment.replace('Set("_godLock", scenario == "orbit" ? source : target);', 'Set("_godLock", source);')
    fragment = fragment.replace('Set("_godFollow", scenario == "orbit" ? source.current_position : target.current_position);', 'Set("_godFollow", centre);')
    for name in ('_godClose', '_godCloseTarget'):
        start = f'Set("{name}", scenario == "contact" ? 3.5f : scenario == "orbit" ? 8.5f : scenario == "ground" ? 15f : scenario == "flight" ? 11f : 6.5f);'
        fragment = fragment.replace(start, f'Set("{name}", {close}f);')
    fragment = fragment.replace('1d / 30d', '1d / 12d')
    fragment = fragment.replace('await Fire(definition, source, target, aim: DeliveryAim(target, scenario), aggro: false);',
                                'await Fire(definition, source, aimActor);')
    setup = Path(__file__).with_name('record_deformation.script').read_text()
    fragment = fragment.replace('// 复用身体演示的场地清理', setup + '\n// 复用身体演示的场地清理', 1)
    fragment = fragment.replace('else await Advance(definition.Lifetime + .35f);',
                                Path(__file__).with_name('record_deformation_timeline.script').read_text())
    fragment = fragment.replace('stage = castId == 0 ? "正式施放前" : finished ? "全部实例自然结束" : "同一次真实投送",',
                                'stage = castId == 0 ? "正式施放前" : finished ? "自然结束" : live == null ? "等待发布" :\n'
                                '    live.StepStateOf("unfold") == ConstructionStepState.Interrupted ? "实际接触中止形变" :\n'
                                '    live.StepStateOf("unfold") == ConstructionStepState.Completed ? "完成展开还原" :\n'
                                '    live.StepStateOf("unfold") == ConstructionStepState.Running ? "正在展开还原" :\n'
                                '    live.Age < 1f ? "实际控制形变中" : "收拢保持，等待还原",')
    if any(name in fragment for name in ('Delivery(scenario)', 'DeliveryPositions(', 'DeliveryAim(')):
        raise RuntimeError('共同录像入口已变更，需要更新5.09接入')
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + fragment)
    write_json(folder / 'metadata.json', dict(connection=connected, version=version_stamp()[0], input=str(original)))
    try:
        result = baseline.call('run', str(script), '240000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed') or result.get('view') != folder.parent.name:
        raise RuntimeError('实际录像未通过 ' + str(folder))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--state', type=Path, help='已通过的cast输出目录')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--case', nargs='+', choices=list(CASES))
    parser.add_argument('--encode-only', action='store_true')
    parser.add_argument('--output', type=Path, help='仅编码时指定已有deformation-films/native或worldfall目录')
    args = parser.parse_args()
    if args.output and not args.encode_only:
        parser.error('--output只用于已有帧编码')
    if args.encode_only and not args.output and not os.environ.get('CULTIWAY_DEV_OUTPUT'):
        parser.error('仅编码时需要--output指定已有录像目录')
    mode = 'worldfall' if args.worldfall else 'native'
    connected = None
    if not args.encode_only:
        if not os.environ.get('CULTIWAY_DEV_ACTIVE') or not args.state:
            parser.error('录像需要通过develop的dev.py exec运行，并指定--state')
        import baseline_scenarios as baseline
        connected = baseline.connect(False, validate_runtime=False)
        installed = baseline.call('eval', 'return new { worldfall=System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType("FirstPerson.WorldBoxMod",false)!=null), renderer=UnityEngine.SystemInfo.graphicsDeviceName };')
        if installed['worldfall'] != args.worldfall:
            raise RuntimeError('当前独立安装与请求的显示模式不同')
        connected['renderer'] = installed['renderer']
    output = args.output or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'deformation-films' / mode
    output.mkdir(parents=True, exist_ok=True)
    rows = []
    for case in args.case or CASES:
        folder = output / case
        folder.mkdir(exist_ok=True)
        if not args.encode_only:
            record(case, args.state, folder, baseline, connected)
        result = json.loads((folder / 'result.json').read_text())
        if not result.get('passed') or result.get('view') != mode:
            raise RuntimeError('实际场景未通过或与所选视角不同 ' + str(folder))
        rows.append(encode(case, folder, output, not args.worldfall, CASES[case], '5.09 结构形变', crop_worldfall=False))
    if not args.case:
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' + ''.join(f"file '{case}.mp4'\n" for case in CASES))
        subprocess.run(['ffmpeg', '-y', '-v', 'error', '-safe', '0', '-f', 'concat', '-i', 'movies.ffconcat',
                        '-c', 'copy', '-movflags', '+faststart', 'all.mp4'], cwd=output, check=True)
    renderer = json.loads((folder / 'metadata.json').read_text())['connection']['renderer']
    write_json(output / 'manifest.json', dict(passed=True, view=mode, cases=rows, renderer=renderer,
                                             timeBasis='真实世界时间常速，取景等待单独暂停并保留原始时刻'))


if __name__ == '__main__':
    main()
