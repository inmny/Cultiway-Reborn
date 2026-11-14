#!/usr/bin/env python3
"""复用原版世界，检查触地传播的创作、真实编辑和正式冷启动恢复。"""
import argparse
import json
import os
import shutil
from pathlib import Path

from common import PROJECT, write_json


def main():
    """准备和恢复分开运行，恢复必须使用真正重启后的独立进程。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--restart', action='store_true')
    args = parser.parse_args()
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    connection = baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('当前游戏未加载本轮代码，请先正常退出这一独立进程')
    folder = PROJECT / 'artifacts/skill_rebuild/ground-authoring'
    folder.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    # 原版解除附体会施加随机推力；已由同一人物控制时不要再次解除。
    helpers = helpers.replace('ControllableUnit.clear(false);\n    ControllableUnit.setControllableCreature(source);',
        'if (!ControllableUnit.isControllingUnit(source)) { ControllableUnit.clear(false); ControllableUnit.setControllableCreature(source); }')
    fragment = Path(__file__).with_suffix('.script').read_text().replace('const bool restart = false;',
        'const bool restart = ' + str(args.restart).lower() + ';')
    phase = 'restart' if args.restart else 'prepare'
    script = folder / (phase + '.script')
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\nusing Cultiway.Core.Persistence;\n' +
        helpers + '\ntry\n{\n' + fragment + '\n}\nfinally\n{\n' +
        'Config.paused = true;\nConstructedAbilityService.ContactConfirmed -= Hit;\nConstructedAbilityService.CastEnded -= Ended;\n}\n')
    write_json(folder / (phase + '-metadata.json'), dict(connection=connection))
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / (phase + '-error.json')).write_text(str(error))
        raise RuntimeError('创作编辑恢复未完成，保留现场，见 ' + str(folder / (phase + '-error.json'))) from None
    finally:
        if (folder / 'checks.json').exists():
            shutil.copyfile(folder / 'checks.json', folder / (phase + '-checks.json'))
    write_json(folder / (phase + '-result.json'), result)
    (folder / (phase + '-error.json')).unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
