#!/usr/bin/env python3
"""5.11 C3b生成材料交出、独立余量与回收的连续交付画面。"""
import argparse
import json
import os
from pathlib import Path
import shutil
import subprocess
from common import PROJECT, version_stamp, write_json
from record_body_connections import encode

CASES = dict(solid='实体交出与余量留置', water='水层交出与余量回收')


def record(case, state, folder, baseline, connected):
    if not json.loads((state / 'result.json').read_text()).get('passed'):
        raise RuntimeError('需要C2已通过的创作输入')
    shutil.copy2(state / 'derived.json', folder / 'input-definition.json')
    shutil.copytree(PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1', folder / 'input-world', dirs_exist_ok=True)
    (folder / 'frames').mkdir(exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")',
                              'Path.Combine(Environment.GetEnvironmentVariable("CULTIWAY_DEV_OUTPUT"), "ending-films/' + folder.parent.name + '/' + case + '")')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', 'Path.Combine(folder, "input-world")')
    helpers = helpers.replace('async Task Advance(', 'bool filmCapturing = false;\nasync Task Advance(', 1)
    helpers = helpers.replace('    Config.paused = false;\n    double until', '    await Game.WaitUntil(() => !filmCapturing, 1800);\n    Config.paused = false;\n    double until', 1)
    helpers = helpers.replace('service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until,',
                              'service.GetProperty("ConfirmedWorldTime", F).GetValue(null) >= until && !filmCapturing,', 1)
    fragment = Path(__file__).with_name('record_delivery.script').read_text()
    fragment = fragment.replace('const string scenario = "DELIVERY_CASE";', 'const string scenario = "' + case + '";')
    fragment = fragment.replace('var definition = Delivery(scenario);', Path(__file__).with_name('record_ending_setup.script').read_text())
    fragment = fragment.replace('Actor source = Make(64), target = Make(68), foe = Make(12, "wolf"); target.setKingdom(foe.kingdom);',
                                'Actor source = Make(64), target = Make(72, "wolf");')
    fragment = fragment.replace('DeliveryPositions(source, target, scenario);', 'Place(source, 64); Place(target, 72);')
    fragment = fragment.replace('Vector2 centre = scenario == "orbit" ? source.current_position : (source.current_position + target.current_position) * .5f;',
                                'Vector2 centre = new Vector2(64.8f, 72.5f);')
    fragment = fragment.replace('camera.orthographicSize = scenario == "contact" ? 1.5f : scenario == "orbit" ? 5.2f : scenario == "ground" ? 9f : scenario == "flight" ? 5.5f : 3.5f;',
                                'camera.orthographicSize = ' + ('2.2f' if case == 'solid' else '3.2f') + ';')
    fragment = fragment.replace('Set("_godLock", scenario == "orbit" ? source : target);', 'Set("_godLock", source);')
    fragment = fragment.replace('Set("_godFollow", scenario == "orbit" ? source.current_position : target.current_position);', 'Set("_godFollow", centre);')
    for name in ('_godClose', '_godCloseTarget'):
        fragment = fragment.replace(f'Set("{name}", scenario == "contact" ? 3.5f : scenario == "orbit" ? 8.5f : scenario == "ground" ? 15f : scenario == "flight" ? 11f : 6.5f);', f'Set("{name}", ' + ('3.2f' if case == 'solid' else '5.2f') + ');')
    fragment = fragment.replace('1d / 30d', '1d / 12d')
    fragment = fragment.replace('await Fire(definition, source, target, aim: DeliveryAim(target, scenario), aggro: false);',
                                'await Fire(definition, source, target, aim: new Vector3(64.5f, 72.5f, 0), aggro: false);')
    fragment = fragment.replace('Task recording = null; double started = -1;',
                                Path(__file__).with_name('record_ending_events.script').read_text() + '\nTask recording = null; double started = -1;')
    fragment = fragment.replace('else await Advance(definition.Lifetime + .35f);',
                                Path(__file__).with_name('record_ending_timeline.script').read_text())
    fragment = fragment.replace('stage = castId == 0 ? "正式施放前" : finished ? "全部实例自然结束" : "同一次真实投送",',
                                'stage = castId == 0 ? "正式施放前" : recalling ? "实际余量回收" : finished ? "原施放结束，余量独立留置" : live?.Age < .6f ? "真实生成主体" : "生成材料交给原版地块",')
    fragment = fragment.replace('Check(paid && finished && !cancelled && hitCount > 0 && target.data.health < hp && Live() == null && !BodyControlState.IsBusy(source.id),\n        "同次正式受理、真实投送接触及全部自然结束", new { paid, hitCount, hp, health = target.data.health });',
                                Path(__file__).with_name('record_ending_checks.script').read_text())
    fragment = fragment.replace('ConstructedAbilityService.CastStarted -= Started; ConstructedAbilityService.ContactConfirmed -= Contact; ConstructedAbilityService.CastEnded -= Finished;',
                                'ConstructedAbilityService.CastStarted -= Started; ConstructedAbilityService.ContactConfirmed -= Contact; ConstructedAbilityService.CastEnded -= Finished;\n    ConstructedAbilityService.WorldContactConfirmed -= WorldHit; WorldMaterialService.Changed -= Material;')
    if any(name in fragment for name in ('Delivery(scenario)', 'DeliveryPositions(', 'DeliveryAim(')):
        raise RuntimeError('共同录像入口已改变，需要更新5.11接入')
    script = folder / 'input.script'; script.write_text(helpers + '\n' + fragment)
    write_json(folder / 'metadata.json', dict(connection=connected, version=version_stamp()[0], input=str(state / 'derived.json'),
        demonstrationEdits='读取已通过的原术衍生成品副本，放慢存在窗口，扩大可读交出比例；水层组改用既有浪体和流散，回收沿共同入口'))
    try:
        result = baseline.call('run', str(script), '240000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error))); raise
    write_json(folder / 'result.json', result)
    if not result.get('passed') or result.get('view') != folder.parent.name:
        raise RuntimeError('实际录像未通过 ' + str(folder))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--state', type=Path, help='C2已通过的creation输出目录')
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--case', nargs='+', choices=list(CASES))
    parser.add_argument('--encode-only', action='store_true')
    parser.add_argument('--output', type=Path, help='仅编码已有帧时指定目录')
    args = parser.parse_args()
    if args.output and not args.encode_only: parser.error('--output只用于已有帧编码')
    if args.encode_only and not args.output and not os.environ.get('CULTIWAY_DEV_OUTPUT'): parser.error('仅编码需要--output')
    mode = 'worldfall' if args.worldfall else 'native'; connected = None
    if not args.encode_only:
        if not os.environ.get('CULTIWAY_DEV_ACTIVE') or not args.state: parser.error('请通过develop运行并指定--state')
        import baseline_scenarios as baseline
        connected = baseline.connect(False, validate_runtime=False)
        installed = baseline.call('eval', 'return new { worldfall=System.Linq.Enumerable.Any(System.AppDomain.CurrentDomain.GetAssemblies(),a=>a.GetType("FirstPerson.WorldBoxMod",false)!=null), renderer=UnityEngine.SystemInfo.graphicsDeviceName };')
        if installed['worldfall'] != args.worldfall: raise RuntimeError('实际加载模式与所选模式不同')
        connected['renderer'] = installed['renderer']
    output = args.output or Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'ending-films' / mode; output.mkdir(parents=True, exist_ok=True)
    rows = []
    for case in args.case or CASES:
        folder = output / case; folder.mkdir(exist_ok=True)
        if not args.encode_only: record(case, args.state, folder, baseline, connected)
        result = json.loads((folder / 'result.json').read_text())
        if not result.get('passed') or result.get('view') != mode: raise RuntimeError('缺少对应视角的真实通过结果')
        rows.append(encode(case, folder, output, not args.worldfall, CASES[case], '5.11 材料交出与余量', crop_worldfall=False))
    if not args.case:
        (output / 'movies.ffconcat').write_text('ffconcat version 1.0\n' + ''.join(f"file '{case}.mp4'\n" for case in CASES))
        subprocess.run(['ffmpeg','-y','-v','error','-safe','0','-f','concat','-i','movies.ffconcat','-c','copy','-movflags','+faststart','all.mp4'],cwd=output,check=True)
    write_json(output / 'manifest.json', dict(passed=True, view=mode, cases=rows,
        renderer=json.loads((folder / 'metadata.json').read_text())['connection']['renderer'],
        timeBasis='真实世界时间常速，取景等待单独暂停并保留原始时刻'))


if __name__ == '__main__': main()
