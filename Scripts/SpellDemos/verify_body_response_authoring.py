#!/usr/bin/env python3
"""复用原版世界与正式编辑入口，验证结果恢复的生成、改造和冷启动存档。"""
import argparse
import json
import os
from pathlib import Path
from common import PROJECT, write_json


def main():
    """准备与真正重启分开运行，沿现有独立安装，不创建身体或装备矩阵。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--restart', action='store_true', help='退出原独立游戏后，验证同一正式存档')
    args = parser.parse_args()
    install = PROJECT / 'artifacts/skill_rebuild/performance-baseline/cultiway'
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    folder = PROJECT / 'artifacts/skill_rebuild/body-response-authoring'
    folder.mkdir(parents=True, exist_ok=True)
    baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('代码晚于本游戏，请先退出这一独立进程')
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bridge =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-footwork', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('if (contact.AbilityId.StartsWith("test.body-footwork.")) contacts.Add(contact);', 'contacts.Add(contact);')
    helpers = helpers.replace('if (fact.AbilityId.StartsWith("test.body-footwork."))\n        endings.Add', 'endings.Add')
    script = folder / ('restart.script' if args.restart else 'input.script')
    fragment = Path(__file__).with_suffix('.script').read_text()
    fragment = fragment.replace('const bool restart = false;', 'const bool restart = ' + str(args.restart).lower() + ';')
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\nusing Cultiway.Core.Persistence;\nusing Newtonsoft.Json.Linq;\n' + helpers + '\ntry\n{\n' + fragment + '\n}\nfinally\n{\nConstructedAbilityService.ContactConfirmed -= Hit;\nConstructedAbilityService.CastEnded -= Ended;\n}\n')
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        raise RuntimeError('生成编辑存档专项未完成，详见 ' + str(folder / 'error.json')) from None
    write_json(folder / ('restart-result.json' if args.restart else 'result.json'), result)
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
