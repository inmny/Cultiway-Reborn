#!/usr/bin/env python3
"""8.14：复用 8.13 的人物和铁剑，分段复验资料提示和跨形态改造。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True, help='8.13 剑尖段的输出目录')
    parser.add_argument('--section', choices=('inputs', 'transform', 'cast'), default='inputs')
    parser.add_argument('--basis', type=Path, help='仅复验施放时使用窗口已保存的成品')
    args = parser.parse_args()
    if args.section == 'cast' and (not args.basis or not args.basis.is_file()):
        parser.error('cast 段需要 --basis 指向窗口成品 saved.json')
    prior = args.source.resolve()
    if not (prior / 'save/map.wbox').is_file() or not (prior / 'expected.json').is_file():
        parser.error('--source 需要同时有 8.13 的 expected.json 与 save/map.wbox')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / f'body-form-{args.section}-814'
    if folder.exists():
        folder = Path(tempfile.mkdtemp(prefix=folder.name + '-', dir=folder.parent))
    else:
        folder.mkdir()
    script = folder / 'input.script'
    program = Path(__file__).with_suffix('.script').read_text()
    if args.section != 'inputs':
        program = ('using Cultiway.Content.Components;\nusing Cultiway.Content.Combat;\n'
                   'using Cultiway.Core.SkillLibV3.ActiveAbilities;\nusing Cultiway.Core.SkillLibV3.Usage;\n'
                   + program.split('// INPUT_CHECKS', 1)[0] + Path(__file__).with_name('verify_body_form_transform.script').read_text())
    if args.section != 'inputs':
        basis = json.dumps(str(args.basis.resolve())) if args.section == 'cast' else 'null'
        program = program.replace('__BASIS__', basis)
    script.write_text(program.replace('__PRIOR__', json.dumps(str(prior))).replace('__OUTPUT__', json.dumps(str(folder))))
    try:
        result = baseline.call('run', str(script), '120000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'身体资料复验未通过：{folder}')
    print(json.dumps(result | dict(output=str(folder)), ensure_ascii=False))


if __name__ == '__main__':
    main()
