#!/usr/bin/env python3
"""采集原版与三维身体的真实关节和非人支撑，不创建新身体样本。"""
import argparse
import json
import os
from pathlib import Path
from common import PROJECT, write_json


def main():
    """只验证同一既有狼形的结构入口，完整动作继续后续专项。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true', help='复用现有独立三维安装')
    parser.add_argument('--frames', action='store_true', help='检查同一原版兽形的实际行走帧切换')
    args = parser.parse_args()
    if args.frames and args.worldfall:
        parser.error('--frames只检查原版图片资料')
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else 'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    folder = PROJECT / 'artifacts/skill_rebuild/body-structure' / ('worldfall' if args.worldfall else 'native')
    if args.frames:
        folder = PROJECT / 'artifacts/skill_rebuild/body-data/native'
    folder.mkdir(parents=True, exist_ok=True)
    baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(p.stat().st_mtime > started for p in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('代码晚于本游戏，请先退出这一独立进程')
    helpers = Path(__file__).with_name('verify_body_organs.script').read_text().split('var editor =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-organs', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('var service = typeof(ConstructedAbilityService);', 'bool spatial = ' + str(args.worldfall).lower() + ';\nvar service = typeof(ConstructedAbilityService);')
    script = folder / 'input.script'
    fragment = Path(__file__).with_suffix('.script').read_text()
    if args.frames:
        fragment = Path(__file__).with_name('verify_creature_frames.script').read_text()
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\nusing Cultiway.Core.Persistence;\nusing Newtonsoft.Json.Linq;\n' + helpers + '\ntry\n{\n' + fragment + '\n}\nfinally\n{\nConstructedAbilityService.ContactConfirmed -= Hit;\n}\n')
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        raise RuntimeError('真实身体结构专项未完成，详见 ' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
