#!/usr/bin/env python3
"""8.12：使用 8.11 的同一正式档检查万法阁列表和窗口关闭。"""
import argparse
import json
import os
from pathlib import Path

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True, help='8.11 保存段的输出目录')
    parser.add_argument('--section', choices=('all', 'catalog', 'lifecycle'), default='all')
    args = parser.parse_args()
    prior = args.source.resolve()
    if not (prior / 'save/map.wbox').is_file() or not (prior / 'expected.json').is_file():
        parser.error('--source 需要同时有 8.11 的 expected.json 与 save/map.wbox')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    connection = baseline.connect(False, validate_runtime=False)
    output = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'wanfa-window-edges' / args.section
    output.mkdir(parents=True, exist_ok=True)
    program = Path(__file__).with_suffix('.script').read_text()
    for key, value in [('__PRIOR_OUTPUT__', prior), ('__OUTPUT__', output), ('__SECTION__', args.section)]:
        program = program.replace(key, json.dumps(str(value)))
    script = output / 'input.script'
    script.write_text(program)
    write_json(output / 'connection.json', connection)
    try:
        result = baseline.call('run', str(script), '180000')
        if not result.get('passed'):
            raise RuntimeError(f'窗口复验未通过：{output}')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise
    result['output'] = str(output)
    write_json(output / 'result.json', result)
    print(json.dumps(result, ensure_ascii=False))


if __name__ == '__main__':
    main()
