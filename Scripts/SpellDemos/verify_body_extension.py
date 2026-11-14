#!/usr/bin/env python3
"""复用既有世界、人物与施放入口，验证4.31的延伸接触与法宝承载。"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time

from common import PROJECT, write_json


def main():
    """日常只运行延伸改造受影响路径，连续录像及正式恢复集中交付。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--carrier', action='store_true', help='使用同一世界入口验证真实法宝承载')
    parser.add_argument('--save-stage', choices=['all', 'prepare', 'restore'], help='正式保存、真正冷启动恢复与换世界归还')
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else
                         'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(path.stat().st_mtime > started for path in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('本轮代码晚于当前独立游戏，请先退出该进程')
    if args.save_stage:
        folder = PROJECT / 'artifacts/skill_rebuild/body-transform-save' / mode
        folder.mkdir(parents=True, exist_ok=True)

        def run(stage):
            """失败保留同一进程及实际输入，不重开准备场景。"""
            if stage == 'prepare':
                helpers = Path(__file__).with_name('verify_body_organs.script').read_text().split('var editor =', 1)[0]
                helpers = helpers.replace('artifacts/skill_rebuild/body-organs', str(folder.relative_to(PROJECT)))
                carrier = Path(__file__).with_name('verify_body_carrier.script').read_text()
                setup = 'Actor source =' + carrier.split('Actor source =', 1)[1].split('var artifactType =', 1)[0]
                setup = setup.replace('ArtifactEquipMode.ForcedOperating', 'ArtifactEquipMode.Standby')
                code = 'using Friflo.Engine.ECS;\nusing Cultiway.Core.Persistence;\n' + helpers + '\ntry\n{\n' + setup
                code += Path(__file__).with_name('verify_body_transform_save.script').read_text()
                code += '\n}\nfinally { ConstructedAbilityService.ContactConfirmed -= Hit; Config.paused = true; }\n'
            else:
                code = Path(__file__).with_name('verify_body_transform_restart.script').read_text()
                code = code.replace('BODY_TRANSFORM_FOLDER', str(folder.relative_to(PROJECT)))
            script = folder / (stage + '.script')
            script.write_text(code)
            write_json(folder / (stage + '-result.json'), dict(passed=False, status='running'))
            write_json(folder / (stage + '-metadata.json'), dict(connection=connection, view=mode))
            try:
                result = baseline.call('run', str(script), '180000')
            except RuntimeError as error:
                (folder / (stage + '-error.json')).write_text(str(error))
                write_json(folder / (stage + '-result.json'), dict(passed=False))
                raise RuntimeError('跨形态正式恢复未完成，见 ' + str(folder / (stage + '-error.json'))) from None
            write_json(folder / (stage + '-result.json'), result)
            (folder / (stage + '-error.json')).unlink(missing_ok=True)
            print(json.dumps(dict(stage=stage, result=result), ensure_ascii=False), flush=True)

        connection = connected
        if args.save_stage in ('all', 'prepare'):
            run('prepare')
        if args.save_stage == 'prepare':
            return
        expected = json.loads((folder / 'restart-expected.json').read_text())
        if connection['pid'] == expected['process']:
            subprocess.run([sys.executable, str(Path(__file__).with_name('baseline_scenarios.py')), 'quit'],
                           cwd=PROJECT, env=baseline.ENV, check=True)
            deadline = time.monotonic() + 15
            while baseline.running(connection['pid']):
                if time.monotonic() > deadline:
                    raise RuntimeError('已请求冷启动退出，原进程仍活着，保留等待状态')
                time.sleep(.1)
            print('正式准备通过，原进程已退出，正在真正重启', flush=True)
            connection = baseline.connect(True, validate_runtime=False)
        run('restore')
        return
    folder = PROJECT / ('artifacts/skill_rebuild/body-carrier' if args.carrier else
                         'artifacts/skill_rebuild/body-extension') / mode
    folder.mkdir(parents=True, exist_ok=True)
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    helpers = Path(__file__).with_name('verify_body_organs.script').read_text().split('var editor =', 1)[0]
    if args.carrier:
        helpers = 'using Friflo.Engine.ECS;\n' + helpers
    helpers = helpers.replace('artifacts/skill_rebuild/body-organs', str(folder.relative_to(PROJECT)))
    script = folder / 'input.script'
    fragment = Path(__file__).with_name('verify_body_carrier.script') if args.carrier else Path(__file__).with_suffix('.script')
    script.write_text(helpers + fragment.read_text())
    write_json(folder / 'metadata.json', dict(connection=connected, view=mode))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        write_json(folder / 'result.json', dict(passed=False))
        raise RuntimeError(('法宝承载' if args.carrier else '延伸接触') + '未完成，见 ' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
