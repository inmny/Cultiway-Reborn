#!/usr/bin/env python3
"""5.04 A1/A2：复用已有世界核对有限射线、沿线停止与真实遮挡。"""
import argparse
import json
import os
from pathlib import Path

from common import PROJECT, version_stamp, write_json


def main():
    """按入口只运行对应一批，完整双端影片在后续交付。"""
    parser = argparse.ArgumentParser(description=__doc__)
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument('--blocking', action='store_true', help='只验证沿线停止与强弱屏障')
    mode.add_argument('--authoring', action='store_true', help='验证投送创作、修改、名称与正式使用')
    args = parser.parse_args()
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connected = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('本轮代码晚于独立游戏，请先正常退出该进程')
    folder = PROJECT / 'artifacts/skill_rebuild/ray'
    if args.blocking:
        folder = folder / 'blocking'
    if args.authoring:
        folder = PROJECT / 'artifacts/skill_rebuild/delivery/authoring'
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('ControllableUnit.clear(false);\n    ControllableUnit.setControllableCreature(source);',
        'if (!ControllableUnit.isControllingUnit(source)) { ControllableUnit.clear(false); ControllableUnit.setControllableCreature(source); }')
    if args.authoring:
        helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
        helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        ', '')
    script = folder / 'input.script'
    body = Path(__file__).with_name('verify_delivery_authoring.script' if args.authoring else 'verify_ray_blocking.script') if args.authoring or args.blocking else Path(__file__).with_suffix('.script')
    headers = 'using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\nusing Cultiway.Core.Persistence;\n' if args.authoring else ''
    script.write_text(headers + helpers + '\ntry\n{\n' + body.read_text() + '\n}\nfinally\n{\nConstructedAbilityService.ContactConfirmed -= Hit; ConstructedAbilityService.CastEnded -= Ended; Config.paused = true; ControllableUnit.clear(false);\n}\n')
    write_json(folder / 'metadata.json', dict(version=version_stamp()[0], connection=connected))
    write_json(folder / 'result.json', dict(passed=False, status='running'))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        raise RuntimeError('有限射线验证未完成，保留现场：' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError('有限射线未通过，请查看 ' + str(folder / 'checks.json'))
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
