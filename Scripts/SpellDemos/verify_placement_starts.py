#!/usr/bin/env python3
"""5.05 C15：放置改造的真实开始条件、就绪步骤和旧请求保持。"""
import argparse
import json
import os
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'ui', 'behavior', 'save'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再验证')
    base = PROJECT / 'artifacts/skill_rebuild/trajectories/placement-starts'
    for part in (('ui', 'behavior', 'save') if section == 'all' else (section,)):
        folder = base / part
        folder.mkdir(parents=True, exist_ok=True)
        if (folder / 'result.json').exists() and not json.loads((folder / 'result.json').read_text()).get('passed'):
            failures = base / 'failures'
            failures.mkdir(exist_ok=True)
            index = 1
            while (failures / f'{part}-{index}').exists():
                index += 1
            shutil.copytree(folder, failures / f'{part}-{index}')
        helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
        helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
        helpers = helpers.replace('test.body-footwork.', 'test.placement-starts.')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.placement-starts."))', '')
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.placement-starts.")) contacts.Add(contact);', 'contacts.Add(contact);')
        body = Path(__file__).with_name('verify_placement_starts_' + part.replace('-', '_') + '.script').read_text()
        script = folder / 'input.script'
        script.write_text('using Cultiway.Core.Persistence;\nusing UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\n' + helpers + '\n' + body)
        write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
        write_json(folder / 'result.json', dict(passed=False, status='running'))
        try:
            result = baseline.call('run', str(script), '180000')
        except RuntimeError as error:
            write_json(folder / 'result.json', dict(passed=False, error=str(error)))
            raise
        write_json(folder / 'result.json', result)
        if not result.get('passed'):
            raise RuntimeError('放置开始条件验证未通过：' + str(folder / 'checks.json'))
        print(json.dumps(dict(section=part, result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
