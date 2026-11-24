#!/usr/bin/env python3
"""5.05 C3：两份构造遍历、目录单项选择与失败后恢复，复用正式演示命令。"""
import json
import os
from pathlib import Path
import sys
import time
from unittest.mock import patch

from common import PROJECT, write_json


def main():
    """使用独立安装和已有世界，保留命令输出、原始结果及恢复状态。"""
    folder = PROJECT / 'artifacts/skill_rebuild/trajectories/construction-batch'
    folder.mkdir(parents=True, exist_ok=True)
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    import common
    baseline.connect(True, validate_runtime=False)

    def connect(_launch):
        baseline.call('hello')
        return common.install_recorder(baseline.call, baseline.PROFILE)

    def run(name, arguments, all_spells=True, fails=False):
        output = folder / name
        command = ['demo_all.py' if all_spells else 'demo_spell.py', *arguments,
                   '--duration', '1.5', '--fps', '8', '--target-offset', '8', '0', '--output', str(output)]
        (folder / f'{name}.command.json').write_text(json.dumps(command, ensure_ascii=False, indent=2))
        exit_code = 0
        with patch.object(sys, 'argv', command), patch.object(common, 'rpc', baseline.call), patch.object(common, 'connect', connect):
            try:
                common.main(all_spells)
            except SystemExit as error:
                exit_code = error.code
        # 命令拒绝时游戏仍在执行既定恢复；只观察同一进程，完成后才发送下一批。
        deadline = time.monotonic() + 90
        while time.monotonic() < deadline:
            status_path = output / 'status.json'
            if status_path.exists():
                status = json.loads(status_path.read_text())
                if status.get('done'):
                    break
            time.sleep(.2)
        else:
            raise RuntimeError(f'{name}没有完成收尾')
        state = baseline.call('eval', '''var runner = UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.MonoBehaviour>()
            .Single(value => value != null && value.GetType().Name == "CultiwaySpellDemoRunner");
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            return new { busy = (bool)runner.GetType().GetField("busy", flags).GetValue(runner),
                paused = Config.paused, hidden = Config.ui_main_hidden,
                controlled = ControllableUnit.getControllableUnit() != null,
                forms = Cultiway.Core.Visuals.Forms.FormVisualScene.Instances.Count,
                temporaryActors = World.world.units.getSimpleList().Count(actor => actor.data.name.StartsWith("法术演示·")),
                materials = Cultiway.Content.Skills.WorldMaterialService.Count };''')
        write_json(output / 'restored.json', state)
        if not status.get('sceneRestored') or state['busy'] or not state['paused'] or state['hidden'] or state['controlled'] or state['forms'] or state['temporaryActors'] or state['materials']:
            raise RuntimeError(f'{name}恢复不完整：{state}')
        if bool(exit_code) != fails:
            raise RuntimeError(f'{name}退出状态不符：{exit_code}')
        return dict(exit=exit_code, status=status, restored=state)

    inputs = Path(__file__).with_name('fixtures') / 'construction-batch'
    evidence = {}
    evidence['batch'] = run('batch', ['--construction', str(inputs)])
    invalid = folder / 'invalid.json'
    definition = json.loads((inputs / '01-charge.json').read_text())
    definition['Id'] = 'test.construction-batch.invalid'
    definition['Root']['Form'] = 'missing.c3.form'
    write_json(invalid, definition)
    evidence['invalid'] = run('invalid', ['--construction', str(invalid)], fails=True)
    evidence['single'] = run('single', ['批量曲线', '--construction', str(inputs)], all_spells=False)
    evidence['resume'] = run('batch', ['--construction', str(inputs), '--resume'])
    manifest = json.loads((folder / 'batch/manifest.json').read_text())
    reports = [json.loads((folder / 'batch' / case['key'] / 'result.json').read_text()) for case in manifest['cases']]
    single = json.loads((folder / 'single/manifest.json').read_text())
    if len(reports) != 2 or [report['resource'] for report in reports] != ['vigor', 'wakan'] or any(not report['passed'] or report['scriptCasts'] != 1 or report['actualContacts'] < 1 or report['paid'] <= 0 for report in reports):
        raise RuntimeError('两份输入未各自正式支付和命中')
    if len(single['cases']) != 1 or single['cases'][0]['id'] != manifest['cases'][1]['id']:
        raise RuntimeError('单项选择没有指向第二份资料')
    write_json(folder / 'delivery.json', dict(passed=True, scope='5.05 C3 统一构造演示', evidence=evidence,
               batch='batch/全部法术-总览.mp4', single='single/construction-0001/demo.mp4',
               limitation='功能录像；软件渲染，不作为最终流派美术或性能结论'))
    print(f'C3交付：{folder / "delivery.json"}', flush=True)


if __name__ == '__main__':
    main()
