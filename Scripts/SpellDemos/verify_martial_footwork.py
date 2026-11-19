#!/usr/bin/env python3
"""8.13 B：在同一名人物和铁剑上验证双足步法、瞄准、回弹与收势。"""
import argparse
import json
import os
from pathlib import Path

from common import write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--basis', type=Path, help='可提供 A 实际保存的 edited.json 作为改造底本')
    parser.add_argument('--cast', type=Path, help='只补施放段，传入窗口实际保存的 edited.json')
    args = parser.parse_args()
    for state in (args.basis, args.cast):
        if state and not state.is_file():
            parser.error(f'找不到保存的资料：{state}')
    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / ('martial-footwork-813b-cast' if args.cast else 'martial-footwork-813b')
    output.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bridge =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    script = output / 'input.script'
    fragment = Path(__file__).with_suffix('.script').read_text()
    fragment = fragment.replace('__CAST_STATE__', json.dumps(str(args.cast.resolve())) if args.cast else 'null')
    fragment = fragment.replace('__BASIS__', json.dumps(str(args.basis.resolve())) if args.basis else 'null')
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
                      + helpers + '\ntry {\n' + fragment
                      + '\n} finally {\nConstructedAbilityService.ContactConfirmed -= Hit;\n'
                      'ConstructedAbilityService.CastEnded -= Ended;\n}\n')
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'战技步法复验失败：{output}')
    print(json.dumps(result | dict(output=str(output)), ensure_ascii=False))


if __name__ == '__main__':
    main()
