#!/usr/bin/env python3
"""5.05 B2/B3：原已学飞剑发射、基础或周期轨迹与实际逐发支付。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """完整输入和实际结果留在同一批目录；失败保留原进程及现场。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--periodic', action='store_true', help='验证B3波形、折线和路程曲线')
    args = parser.parse_args()
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前独立游戏未加载本轮代码，请先正常退出再验证')
    folder = PROJECT / 'artifacts/skill_rebuild/trajectories' / ('periodic-flight' if args.periodic else 'flying-sword')
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    script = folder / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text()
    body = body.replace('// PERIODIC_NUMERIC', Path(__file__).with_name('verify_periodic_path.script').read_text() if args.periodic else '')
    body = body.replace('// PERIODIC_EDITOR', Path(__file__).with_name('verify_periodic_path_editor.script').read_text() if args.periodic else '')
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\nusing Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\nusing Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Modifiers;\nusing Cultiway.Core.SkillLibV3.Settings;\nusing Cultiway.Core.SkillLibV3.Blueprints;\nusing Cultiway.Content.Components.Skill;\nusing Cultiway.Core.EventSystem.Events;\nusing Friflo.Engine.ECS;\n' + helpers + '\nbool periodicMode = ' + str(args.periodic).lower() + ';\n' + body)
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        raise RuntimeError('原飞剑验证未完成，保留现场：' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    (folder / 'error.json').unlink(missing_ok=True)
    if not result.get('passed'):
        raise RuntimeError('原飞剑未通过，请查看 ' + str(folder / 'checks.json'))
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
