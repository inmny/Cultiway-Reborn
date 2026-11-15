#!/usr/bin/env python3
"""5.05 B29：投射与坠落爆发的真实余力交接及来源接管。"""
import argparse
import json
import os
import re
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """复用现有世界、人物及调试通道，保留本批实际输入和结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('all', 'sources', 'common', 'lifecycle', 'ui', 'visuals'), default='all')
    section = parser.parse_args().section
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
    folder = PROJECT / 'artifacts/skill_rebuild/trajectories/impacts'
    if section != 'all':
        folder = folder / section
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    imports = ('using UnityEngine.UI;\nusing Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Settings;\nusing Cultiway.Core.SkillLibV3.Blueprints;\n'
               'using Cultiway.Content.Components.Skill;\nusing Cultiway.Core.EventSystem.Events;\n'
               'using Friflo.Engine.ECS;\n')
    wall = Path(__file__).with_name('verify_wall_migration.script').read_text()
    shot = 'async Task Shot' + wall.split('async Task Shot', 1)[1].split('var activeCasts', 1)[0]
    shot = shot.replace('new Vector3(68,74,', 'new Vector3(69,72,')
    script = folder / 'input.script'
    body = re.sub(r'const string section\s*=\s*"all";', f'const string section="{section}";', Path(__file__).with_suffix('.script').read_text(), count=1)
    script.write_text(imports + helpers + '\n' + shot + body)
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('爆发来源接入未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
