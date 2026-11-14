#!/usr/bin/env python3
"""复用现成城市与平坦世界，检查4.32B2的两种规模和实际显示。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, write_json


def main():
    """指定一类或依次运行，两种显示使用同一冻结场景输入。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--case', nargs='+', choices=['city', 'world'])
    args = parser.parse_args()
    mode = 'worldfall' if args.worldfall else 'native'
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else
                         'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏尚未加载本轮代码，请先正常退出该进程')
    for case in args.case or ['city', 'world']:
        folder = PROJECT / 'artifacts/skill_rebuild/ground-scale' / mode / case
        folder.mkdir(parents=True, exist_ok=True)
        save = 'artifacts/skill_rebuild/performance-baseline/cultiway/worlds/none'
        if not (PROJECT / save / 'map.wbox').is_file():
            raise RuntimeError('缺少现成演示世界：' + save)
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
        helpers = helpers.replace('Scripts/WorldSaveTests/Fixtures/world-format-1', save)
        begin = helpers.index('for (int x = 60; x <= 75; x++)')
        end = helpers.index('var holdStats', begin)
        helpers = helpers[:begin] + helpers[end:]
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        fragment = Path(__file__).with_suffix('.script').read_text().replace('GROUND_SCALE_CASE', case)
        script = folder / 'input.script'
        script.write_text(helpers + '\n' + fragment)
        write_json(folder / 'metadata.json', dict(connection=connected, view=mode, case=case, save=save))
        write_json(folder / 'result.json', dict(passed=False, status='running'))
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            (folder / 'error.json').write_text(str(error))
            write_json(folder / 'result.json', dict(passed=False))
            raise RuntimeError('规模检查未完成，保留同一进程和现场，见 ' + str(folder / 'error.json')) from None
        if result.get('view') != mode:
            result.update(passed=False, error='实际显示与请求不符：' + str(result.get('view')))
            write_json(folder / 'result.json', result)
            raise RuntimeError(result['error'])
        write_json(folder / 'result.json', result)
        (folder / 'error.json').unlink(missing_ok=True)
        print(json.dumps(dict(view=mode, case=case, result=result), ensure_ascii=False), flush=True)


if __name__ == '__main__':
    main()
