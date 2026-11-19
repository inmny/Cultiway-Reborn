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
    parser.add_argument('--section', choices=('editor', 'cast', 'strike', 'restore', 'film'), default='editor')
    parser.add_argument('--basis', type=Path, help='编辑底本，或 cast 段使用的窗口成品 edited.json')
    parser.add_argument('--opposing', type=float, default=65.275, help='剑尖复验的对手站位')
    parser.add_argument('--restore', type=Path, help='剑尖段正式存档及 expected.json 的目录')
    args = parser.parse_args()
    if args.basis and not args.basis.is_file():
        parser.error(f'找不到保存的资料：{args.basis}')
    if args.section in ('cast', 'strike', 'film') and not args.basis:
        parser.error(f'{args.section} 段需要 --basis 指向窗口成品')
    if args.section == 'restore' and (not args.restore or not (args.restore / 'expected.json').is_file()):
        parser.error('restore 段需要 --restore 指向剑尖段输出目录')
    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    output = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'martial-process-813c'
    if args.section != 'editor':
        output /= args.section
    output.mkdir(parents=True, exist_ok=True)
    helpers = Path(__file__).with_name('verify_body_footwork.script').read_text().split('var bridge =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(output)))
    fixture = Path(__file__).resolve().parents[2] / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    if args.section == 'restore':
        fixture = args.restore.resolve() / 'save'
        helpers = helpers.split('for (int x = 60;', 1)[0]
    if not (fixture / 'map.wbox').is_file():
        parser.error(f'找不到现成测试世界：{fixture}')
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")',
                              json.dumps(str(fixture)))
    if args.section == 'film':
        from common import version_stamp
        film = Path(__file__).with_name('record_body_connections.script').read_text()
        film = film.replace('bool filmResponses = false;', 'bool filmResponses = true;')
        film = film.replace('fact.Fact.Age,\n        action', 'fact.Fact.Age, fact.Fact.Instance,\n        action')
        film = film.replace('AbilityActionEventKind.Active => "实际作用窗口",',
                            'AbilityActionEventKind.Active => "第" + (fact.Fact.Instance + 1) + "段实际作用",')
        helpers += '\nbool filmCapturing = false;\nfloat[] P(Vector3 p) => new[] { p.x, p.y, p.z };\n' + film
        write_json(output / 'metadata.json', dict(version=version_stamp()[0]))
    script = output / 'input.script'
    name = 'verify_martial_process' + ('' if args.section == 'editor' else '_' + args.section)
    fragment = Path(__file__).with_name(name + '.script').read_text()
    fragment = fragment.replace('__BASIS__', json.dumps(str(args.basis.resolve())) if args.basis else 'null')
    fragment = fragment.replace('__OPPOSING__', repr(args.opposing))
    if args.restore:
        fragment = fragment.replace('__RESTORE__', json.dumps(str(args.restore.resolve())))
    cleanup = 'ConstructedAbilityService.CastEnded -= Ended;'
    if args.section != 'restore':
        cleanup += '\nConstructedAbilityService.ContactConfirmed -= Hit;'
    script.write_text('using Cultiway.UI;\nusing Cultiway.UI.Prefab;\nusing UnityEngine.UI;\n'
                      + helpers + '\ntry {\n' + fragment
                      + '\n} finally {\n' + cleanup + '\n}\n')
    try:
        result = baseline.call('run', str(script), '140000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'战技阶段复验失败：{output}')
    if args.section == 'film':
        from record_body_connections import encode
        native = json.loads((output / 'recording.json').read_text())['view'] == 'native'
        encode('martial-process', output, output, native, '同一把铁剑：剑尖命中与收势', '8.13 战技分段')
    print(json.dumps(result | dict(output=str(output)), ensure_ascii=False))


if __name__ == '__main__':
    main()
