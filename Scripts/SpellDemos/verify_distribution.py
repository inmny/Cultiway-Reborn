#!/usr/bin/env python3
"""5.02：复用已有世界核对集合、真实部位及表面分布。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """按入口只运行对应一批，完整双端影片在后续交付。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--surfaces', action='store_true', help='5.02 B1：只核对真实部位与三角面随动')
    args = parser.parse_args()
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('本轮代码晚于独立游戏，请先正常退出该进程')
    folder = PROJECT / 'artifacts/skill_rebuild/distribution' / ('surfaces' if args.surfaces else 'collections')
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    script = folder / 'input.script'
    fragment = Path(__file__).with_name('verify_surface_distribution.script') if args.surfaces else Path(__file__).with_suffix('.script')
    script.write_text(helpers + '\n' + fragment.read_text())
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        raise RuntimeError('集合分布验证未完成，保留现场：' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('集合分布未通过，请查看 ' + str(folder / 'checks.json'))
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
