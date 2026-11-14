#!/usr/bin/env python3
"""复用既有世界、人物与施放入口，验证4.31的延伸接触与法宝承载。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, write_json


def main():
    """日常只运行延伸改造受影响路径，连续录像及正式恢复集中交付。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--carrier', action='store_true', help='使用同一世界入口验证真实法宝承载')
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
