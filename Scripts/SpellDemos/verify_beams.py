#!/usr/bin/env python3
"""5.05 B18：从真实已学入口验证瞬发与追踪光束，不重复屏障和身体矩阵。"""
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """复用已有世界、人物准备和调试通道，分别保存本批输入及实际结果。"""
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
    folder = PROJECT / 'artifacts/skill_rebuild/trajectories/beams'
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    imports = ('using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Settings;\nusing Cultiway.Core.SkillLibV3.Blueprints;\n'
               'using Cultiway.Content.Components.Skill;\nusing Cultiway.Core.EventSystem.Events;\n'
               'using Friflo.Engine.ECS;\n')
    script = folder / 'input.script'
    script.write_text(imports + helpers + '\n' + Path(__file__).with_suffix('.script').read_text())
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    result = baseline.call('run', str(script), '180000')
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('光束未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
