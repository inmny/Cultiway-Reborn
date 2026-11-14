#!/usr/bin/env python3
"""复用现成世界验证沿地面传播、到达后结算及真实断路。"""
import json
import os
from pathlib import Path

from common import PROJECT, write_json


def main():
    """仅运行本批地表过程，规模双视图和正式恢复另按交付点完成。"""
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('本轮代码晚于独立游戏，请先正常退出该进程')
    folder = PROJECT / 'artifacts/skill_rebuild/ground-propagation'
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    script = folder / 'input.script'
    script.write_text(helpers + '\n' + Path(__file__).with_suffix('.script').read_text())
    write_json(folder / 'metadata.json', dict(connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        write_json(folder / 'result.json', dict(passed=False))
        raise RuntimeError('地表传播验证未完成，见 ' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
