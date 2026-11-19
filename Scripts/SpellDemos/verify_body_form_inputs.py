#!/usr/bin/env python3
"""8.14：复用 8.13 的正式人物和铁剑，验证身体资料提示及恢复。"""
import argparse
import json
import os
from pathlib import Path

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--source', type=Path, required=True, help='8.13 剑尖段的输出目录')
    args = parser.parse_args()
    prior = args.source.resolve()
    if not (prior / 'save/map.wbox').is_file() or not (prior / 'expected.json').is_file():
        parser.error('--source 需要同时有 8.13 的 expected.json 与 save/map.wbox')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过 develop 的 dev.py exec 运行')
    import baseline_scenarios as baseline
    baseline.connect(False, validate_runtime=False)
    folder = Path(os.environ['CULTIWAY_DEV_OUTPUT']) / 'body-form-inputs-814'
    folder.mkdir()
    script = folder / 'input.script'
    program = Path(__file__).with_suffix('.script').read_text()
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
