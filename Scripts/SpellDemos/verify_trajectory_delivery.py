#!/usr/bin/env python3
"""5.05交付，真实跨进程存读及统一入口的集中连续录像。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import sys
import time
import zlib
from unittest.mock import patch

from common import PROJECT, version_stamp, write_json


def stop(baseline):
    """只退出本独立安装，等待同一进程真正终止。"""
    state = baseline.ROOT / 'process.json'
    if state.exists():
        pid = json.loads(state.read_text())['pid']
        if baseline.running(pid):
            baseline.connect(False, validate_runtime=False)
            quit_error = None
            try:
                baseline.call('eval', 'UnityEngine.Application.Quit(); return true;')
            except RuntimeError as error:
                quit_error = error
            deadline = time.monotonic() + 60
            while baseline.running(pid) and time.monotonic() < deadline:
                time.sleep(.2)
            if baseline.running(pid):
                raise RuntimeError('原游戏仍在退出中，保留进程并停止重启') from quit_error


def restart(baseline):
    """原进程完全退出后才启动下一次。"""
    stop(baseline)
    return baseline.connect(True, validate_runtime=False)


def cold(baseline, base, section):
    """复用共同助手，读取现成正式世界，保留独立分段结果。"""
    connected = (baseline.call('eval', 'return new { pid=System.Diagnostics.Process.GetCurrentProcess().Id };')
                 if section.endswith('-tail') else restart(baseline))
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(path.stat().st_mtime > started for path in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再验证')
    folder = base / section
    folder.mkdir(parents=True, exist_ok=True)
    if (folder / 'result.json').exists():
        failures = base / 'previous-runs'
        failures.mkdir(exist_ok=True)
        index = 1
        while (failures / f'{section}-{index}').exists():
            index += 1
        shutil.copytree(folder, failures / f'{section}-{index}')
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    setup = 'var holdStats =' + helpers.split('var holdStats =', 1)[1].split('foreach (Actor a', 1)[0]
    if section.endswith('-tail'):
        setup = ('var hold=Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold");\nvoid Place(' +
                 setup.split('void Place(', 1)[1])
    helpers = (helpers.split('Game.Resume();', 1)[0] + setup +
               'async Task Fire(' + helpers.split('async Task Fire(', 1)[1])
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    mode = section.removesuffix('-tail')
    body = Path(__file__).with_suffix('.script').read_text().replace('const string section="pending";', f'const string section="{mode}";')
    if section == 'pending-tail':
        body = body.replace('await Load(Path.Combine(previous,"pending-save"));',
                            'Check(process==(int)JObject.Parse(File.ReadAllText(Path.Combine(delivery,"pending/metadata.json")))["connection"]["pid"],"只续接原进程的同一已恢复世界");')
    if section == 'completed-tail':
        body = body.replace('await Load(Path.Combine(delivery,"completed-save"));',
                            'Check(process==(int)JObject.Parse(File.ReadAllText(Path.Combine(delivery,"completed/metadata.json")))["connection"]["pid"],"只续接同一完成世界的状态与去重核对");')
    script = folder / 'input.script'
    script.write_text('using Cultiway.Core.Persistence;\n' + helpers + '\n' + body)
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('冷启动未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(dict(section=section, passed=True, process=result['process']), ensure_ascii=False), flush=True)


def videos(baseline, base, selected):
    """所有画面共用正式演示命令，各自保留原片和实际结果。"""
    import common

    def connect(_launch):
        baseline.connect(True, validate_runtime=False)
        started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
        if any(path.stat().st_mtime > started for path in (PROJECT / 'Source').rglob('*.cs')):
            raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再录制')
        baseline.call('run', str(Path(__file__).with_name('runtime.script')), '60000')
        return json.loads((common.CONTROL / 'catalog.json').read_text())

    plans = [
        ('principles', ['--scope', 'base', '--filter', 'FlyingSword,FallingTimber,GroundSpike,PoisonMist,HealingLight,SpiritBeam,ChainLightning', '--cases', 'base'], 'native', 3),
        ('curve', ['--scope', 'base', '--filter', 'FlyingSword', '--cases', 'trajectory', '--trajectory', 'SineWave'], 'native', 3),
        ('shield', ['--scope', 'base', '--filter', 'EarthWall', '--cases', 'base', '--scene', 'shield'], 'native', 4),
        ('palm', ['--filter', '原地掌击', '--cases', 'base', '--zoom', '2.2'], 'native', 2),
        ('worldfall', ['--scope', 'base', '--filter', 'FlyingSword,GroundSpike,PoisonMist', '--cases', 'base'], 'worldfall', 3),
    ]
    groups = {name for name, *_ in plans} if selected == 'all' else set(selected.split(','))
    if groups - {name for name, *_ in plans}:
        raise ValueError('未知录像组 ' + selected)
    combined, results, versions = [], {}, {}
    for name, arguments, view, duration in plans:
        output = base / 'videos' / name
        if name in groups:
            if view == 'worldfall' and not (baseline.GAME.parent / 'Mods/Worldfall/Worldfall.dll').exists():
                compatible = PROJECT / 'artifacts/skill_rebuild/view-switch/environment/cultiway'
                if not (compatible / 'game/Mods/Worldfall/Worldfall.dll').exists():
                    raise RuntimeError('已有独立安装缺少Worldfall，请用--installation指定兼容安装')
                stop(baseline)
                subprocess.run([sys.executable, str(Path(__file__)), '--section', 'videos', '--video-groups', 'worldfall',
                                '--installation', str(compatible)], cwd=PROJECT, check=True)
                return
            command = ['demo_all.py', *arguments, '--view', view, '--duration', str(duration), '--fps', '8', '--output', str(output)]
            if (output / 'manifest.json').exists():
                command.append('--resume')
            write_json(base / f'{name}.command.json', command)
            with patch.object(sys, 'argv', command), patch.object(common, 'rpc', baseline.call), patch.object(common, 'connect', connect):
                common.main(True)
            restored = baseline.call('eval', '''var runner=UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.MonoBehaviour>()
                .Single(value=>value!=null && value.GetType().Name=="CultiwaySpellDemoRunner");
                return new{busy=(bool)runner.GetType().GetField("busy",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).GetValue(runner),
                    paused=Config.paused,hidden=Config.ui_main_hidden,controlled=ControllableUnit.getControllableUnit()!=null,
                    forms=Cultiway.Core.Visuals.Forms.FormVisualScene.Instances.Count};''')
            write_json(output / 'restored.json', restored)
        if not (output / 'manifest.json').exists():
            continue
        status = json.loads((output / 'status.json').read_text())
        restored = json.loads((output / 'restored.json').read_text())
        if not status.get('done') or status.get('error') or restored['busy'] or not restored['paused'] or restored['hidden'] or restored['controlled'] or restored['forms']:
            raise RuntimeError('录像没有完整收尾 ' + name)
        manifest = json.loads((output / 'manifest.json').read_text())
        versions[name] = manifest['version']
        for case in manifest['cases']:
            key = name + '/' + case['key']
            combined.append(dict(case, key=key, group='原版视角' if view == 'native' else 'Worldfall视角', label=case['label'] + ' · ' + view))
            results[key] = json.loads((output / case['key'] / 'result.json').read_text())
        print(json.dumps(dict(section='videos', completed=name), ensure_ascii=False), flush=True)
    if len(versions) == len(plans):
        common.publish(base / 'videos', combined, results, [])
        write_json(base / 'videos/manifest.json', dict(version=version_stamp()[0], groupVersions=versions,
                   assemblies=sorted({value['assembly'] for value in results.values() if value.get('assembly')}), cases=combined))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'pending', 'pending-tail', 'completed', 'completed-tail', 'videos'), default='all')
    parser.add_argument('--video-groups', default='all', help='all或principles,curve,shield,palm,worldfall中的分组')
    parser.add_argument('--installation', type=Path, help='复用的独立测试安装目录，Worldfall组会转到已有兼容安装')
    args = parser.parse_args()
    section = args.section
    install = args.installation or PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    install = install.resolve()
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    base = PROJECT / 'artifacts/skill_rebuild/trajectories/trajectory-delivery'
    base.mkdir(parents=True, exist_ok=True)
    previous = PROJECT / 'artifacts/skill_rebuild/trajectories/placement-starts/save'
    if section in ('all', 'pending'):
        sources = [previous / 'pending-save/map.wbox', previous / 'pending-expected.json', previous / 'metadata.json']
        write_json(base / 'source.json', [dict(path=str(p.relative_to(PROJECT)), sha256=hashlib.sha256(p.read_bytes()).hexdigest()) for p in sources])
    for part in (('pending', 'completed', 'videos') if section == 'all' else (section,)):
        if part == 'videos':
            videos(baseline, base, args.video_groups)
        else:
            if part.startswith('completed'):
                expected = json.loads((base / 'completed-expected.json').read_text())
                world = json.loads(zlib.decompress((base / 'completed-save/map.wbox').read_bytes()))
                actors = {actor['id']: actor for actor in world['actors_data']}
                write_json(base / 'completed-sections.json', {
                    name: json.loads(actors[expected[key]]['custom_data_string']['cultiway.actor'])['sections']['ability_creation']['data']
                    for name, key in [('source', 'sourceId'), ('legacy', 'legacyId')]})
            cold(baseline, base, part)


if __name__ == '__main__':
    main()
