#!/usr/bin/env python3
"""8.13 C：复用同一世界和铁剑，验证标记、分段和独立接触窗口。"""
import argparse
import json
import os
from pathlib import Path

from common import write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 develop 的 dev.py exec 运行')
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('editor', 'cast'), default='editor')
    parser.add_argument('--basis', type=Path, help='编辑底本，或 cast 段使用的窗口成品 edited.json')
    args = parser.parse_args()
    if args.basis and not args.basis.is_file():
        parser.error(f'找不到保存的资料：{args.basis}')
    if args.section == 'cast' and not args.basis:
        parser.error('cast 段需要 --basis 指向窗口成品')
    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'martial-process-813c'
    if args.section != 'editor':
        output /= args.section
    output.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bridge =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    fixture = Path(__file__).resolve().parents[2] / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    if not (fixture / 'map.wbox').is_file():
        parser.error(f'找不到现成测试世界：{fixture}')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(fixture)))
    script = output / 'input.script'
    name = 'verify_martial_process' + ('' if args.section == 'editor' else '_' + args.section)
    fragment = Path(__file__).with_name(name + '.script').read_text()
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
        raise SystemExit(f'战技阶段复验失败：{output}')
    print(json.dumps(result | dict(output=str(output)), ensure_ascii=False))


if __name__ == '__main__':
    main()
