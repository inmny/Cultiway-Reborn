#!/usr/bin/env python3
"""从真实已学入口验证瞬发、追踪光束；可单独核对 9.02 的新束体。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, version_stamp, write_json


def main():
    """复用已有世界、人物准备和调试通道，分别保存本批输入及实际结果。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('sources', 'appearance'), default='sources')
    args = parser.parse_args()
    development = os.environ.get('CULTIWAY_DEV_ACTIVE')
    if not development:
        if args.section == 'appearance':
            parser.error('外观复验请通过 develop 的 dev.py exec 运行')
        install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
        os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
        os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(not development, validate_runtime=False)
    if development:
        output = Path(os.environ['CULTIWAY_DEV_OUTPUT'])
        folder = Path(tempfile.mkdtemp(prefix=f'beams-{args.section}-', dir=output))
    else:
        started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
        if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
            raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
        folder = PROJECT / 'artifacts/skill_rebuild/trajectories/beams'
        folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    imports = ('using Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Modifiers;\n'
               'using Cultiway.Core.SkillLibV3.Settings;\nusing Cultiway.Core.SkillLibV3.Blueprints;\n'
               'using Cultiway.Content.Components.Skill;\nusing Cultiway.Core.EventSystem.Events;\n'
               'using Friflo.Engine.ECS;\n')
    script = folder / 'input.script'
    program = Path(__file__).with_suffix('.script').read_text()
    if args.section == 'appearance':
        program = program.split('// SOURCE_CHECKS', 1)[0] + Path(__file__).with_name('verify_beams_appearance.script').read_text()
        fixture = Path(__file__).with_name('fixtures') / 'beams-b18.json'
        json.loads(fixture.read_text())
        program = program.replace('__B18_BEAM_EVIDENCE__', json.dumps(str(fixture.resolve())))
    script.write_text(imports + helpers + '\n' + program)
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '285000' if args.section == 'appearance' else '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('光束未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(dict(result, output=str(folder)), ensure_ascii=False))


if __name__ == '__main__':
    main()
