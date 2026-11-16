#!/usr/bin/env python3
"""5.05 C9：世界接触表单、自主创作及正式新旧请求存读。"""
import argparse
import json
import os
import shutil
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'ui', 'cast', 'creation', 'save', 'placement', 'cast-tail'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出再验证')
    base = PROJECT / 'artifacts/skill_rebuild/trajectories/world-contact-creation'
    for part in (('ui', 'cast', 'creation', 'save') if section == 'all' else (section,)):
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
        helpers = helpers.replace('test.body-footwork.', 'test.world-contact-creation.')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.world-contact-creation."))', '')
        if part in ('placement', 'cast-tail'):
            fire = helpers.split('async Task Fire(', 1)[1].split('void Ready(', 1)[0]
            place = 'void Place(' + helpers.split('void Place(', 1)[1].split('foreach (Actor a in World.world', 1)[0]
            helpers = helpers.split('Game.Resume();', 1)[0] + place + 'async Task Fire(' + fire
        body = Path(__file__).with_name('verify_world_contact_creation_' + part + '.script').read_text()
        basis = base / 'ui/saved-definition.json'
        if basis.exists() and json.loads(basis.read_text())['Root']['Operations'][0]['Ground'] != 0:
            basis = base / 'placement/saved-definition.json'
        body = body.replace('__WORLD_CONTACT_BASIS__', str(basis.relative_to(PROJECT)))
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
            raise RuntimeError('世界作用验证未通过：' + str(folder / 'checks.json'))
        print(json.dumps(dict(section=part, result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
