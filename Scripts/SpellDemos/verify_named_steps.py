#!/usr/bin/env python3
"""复用同一狼形检查实际命名足过程、移动作用、缺失拒绝及恢复。"""
import argparse
import json
import os
from pathlib import Path
from common import PROJECT, write_json


def main():
    """复用已有准备和摄像入口，不扩充身体或装备样本。"""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--worldfall', action='store_true')
    parser.add_argument('--external', action='store_true', help='检查实际外部搬移后归还')
    args = parser.parse_args()
    install = PROJECT / ('artifacts/skill_rebuild/view-switch/environment/cultiway' if args.worldfall else 'artifacts/skill_rebuild/performance-baseline/cultiway')
    os.environ['CULTIWAY_BASELINE_ROOT'] = str(install)
    os.environ['CULTIWAY_BASELINE_GAME'] = str(install / 'game/worldbox')
    import baseline_scenarios as baseline
    folder = PROJECT / 'artifacts/skill_rebuild/named-steps' / ('worldfall' if args.worldfall else 'native') / ('external' if args.external else 'normal')
    folder.mkdir(parents=True, exist_ok=True)
    baseline.connect(True, validate_runtime=False)
    started = json.loads((baseline.ROOT / 'process.json').read_text())['started']
    if any(path.stat().st_mtime > started for path in (PROJECT / 'Source').rglob('*.cs')):
        raise RuntimeError('代码晚于本游戏，请先退出这一独立进程')
    helpers = Path(__file__).with_name('verify_body_organs.script').read_text().split('var editor =', 1)[0]
    helpers = helpers.replace('artifacts/skill_rebuild/body-organs', str(folder.relative_to(PROJECT)))
    helpers = helpers.replace('var service = typeof(ConstructedAbilityService);', 'bool spatial = ' + str(args.worldfall).lower() + ';\nbool externalMove = ' + str(args.external).lower() + ';\nvar service = typeof(ConstructedAbilityService);')
    setup = Path(__file__).with_name('verify_body_joint.script').read_text().split('ConstructedAbilityService.CastEnded += Ended;', 1)[0]
    configuration, verification = Path(__file__).with_suffix('.script').read_text().split('// NAMED_READY', 1)
    setup = setup.replace('var plan = AbilityCompiler.Compile(definition, false);', configuration + '\nvar plan = AbilityCompiler.Compile(definition, false);')
    capture = Path(__file__).with_name('verify_joint_handoff.script').read_text().split('ConstructedAbilityService.CastEnded += Ended;', 1)[0]
    script = folder / 'input.script'
    script.write_text(helpers + '\ntry\n{\n' + setup + capture + verification + '\n}\nfinally\n{\nConstructedAbilityService.ContactConfirmed -= Hit;\n}\n')
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        (folder / 'error.json').write_text(str(error))
        raise RuntimeError('命名足步法未完成，详见 ' + str(folder / 'error.json')) from None
    write_json(folder / 'result.json', result)
    (folder / 'error.json').unlink(missing_ok=True)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
