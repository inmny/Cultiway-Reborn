#!/usr/bin/env python3
"""5.02 A：复用已有世界核对集合选择、有限分布与真实子结构作用。"""
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """只运行本批集合通道，表面与完整双端影片在后续交付。"""
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('本轮代码晚于独立游戏，请先正常退出该进程')
    folder = PROJECT / 'artifacts/skill_rebuild/distribution/collections'
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + Path(__file__).with_suffix('.script').read_text())
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
