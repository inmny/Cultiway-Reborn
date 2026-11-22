#!/usr/bin/env python3
"""用一份已有的有效万法阁蓝图，复验同库坏记录的局部恢复。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive', type=Path, required=True, help='12.05 已写回的当前万法阁 JSON 文件')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请在独立游戏中使用 dev.py exec')
    import baseline_scenarios as baseline

    source = args.archive.resolve()
    original = source.read_bytes()
    out = Path(tempfile.mkdtemp(prefix='wanfa-partial-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = out / 'input.script'
    body = Path(__file__).with_suffix('.script').read_text()
    script.write_text(body.replace('"$INPUT"', json.dumps(str(source), ensure_ascii=False))
                      .replace('"$OUTPUT"', json.dumps(str(out), ensure_ascii=False)))
    write_json(out / 'connection.json', baseline.connect(False, validate_runtime=False))
    try:
        result = baseline.call('run', str(script), '45000')
        if not result.get('passed') or source.read_bytes() != original:
            raise RuntimeError('局部恢复未通过或输入原库发生改写')
    except RuntimeError as error:
        write_json(out / 'failure.json', {'passed': False, 'error': str(error)})
        raise
    write_json(out / 'result.json', result)
    print(out)


if __name__ == '__main__':
    main()
