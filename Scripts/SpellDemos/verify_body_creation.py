#!/usr/bin/env python3
"""实际身体自然创法、旧格式恢复、真正冷启动与世界归还。"""
import argparse
import json
import os
from pathlib import Path
import subprocess
import sys
import time
from common import PROJECT, write_json


def main():
    """复用现有独立安装；只有准备成功后才有意退出并真正重启。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--stage', choices=['all', 'prepare', 'restore'], default='all')
    args = parser.parse_args()
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else 'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    folder = PROJECT / 'artifacts/skill_rebuild/body-creation' / ('worldfall' if args.worldfall else 'native')
    folder.mkdir(parents=True, exist_ok=True)
    connection = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(path.stat().st_mtime > started for path in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('代码晚于当前独立游戏，请先退出该进程')

    def run(stage):
        """运行有限脚本，失败保留原进程和完整诊断。"""
        if stage == 'prepare':
            helpers = Path(__file__).with_name('verify_body_organs.script').read_text().split('var editor =', 1)[0]
            helpers = helpers.replace('artifacts/skill_rebuild/body-organs', str(folder.relative_to(PROJECT)))
            code = 'using Cultiway.Core.Persistence;\n' + helpers + '\ntry\n{\n' + Path(__file__).with_name('verify_body_creation.script').read_text() + '\n}\nfinally\n{\nConstructedAbilityService.ContactConfirmed -= Hit; Config.paused = true;\n}\n'
        else:
            code = Path(__file__).with_name('verify_body_creation_restart.script').read_text().replace('BODY_CREATION_FOLDER', str(folder.relative_to(PROJECT)))
        script = folder / (stage + '.script')
        script.write_text(code)
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            (folder / (stage + '-error.json')).write_text(str(error))
            raise RuntimeError('实际身体创法未完成，详见 ' + str(folder / (stage + '-error.json'))) from None
        write_json(folder / (stage + '-result.json'), result)
        (folder / (stage + '-error.json')).unlink(missing_ok=True)
        print(json.dumps(dict(stage=stage, result=result), ensure_ascii=False), flush=True)

    if args.stage in ('all', 'prepare'):
        run('prepare')
    if args.stage == 'prepare':
        return
    expected = json.loads((folder / 'restart-expected.json').read_text())
    if connection['pid'] == expected['process']:
        subprocess.run([sys.executable, str(Path(__file__).with_name('baseline_scenarios.py')), 'quit'], cwd=PROJECT, env=baseline.ENV, check=True)
        deadline = time.monotonic() + 15
        while baseline.running(connection['pid']):
            if time.monotonic() > deadline:
                raise RuntimeError('已请求冷启动退出，原进程仍活着，保留等待状态')
            time.sleep(.1)
        print('准备通过，原进程已退出，正在真正重启', flush=True)
        connection = baseline.connect(True, validate_runtime=False)
    run('restore')


if __name__ == '__main__':
    main()
