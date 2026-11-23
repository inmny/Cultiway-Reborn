#!/usr/bin/env python3
"""复用现成世界验证波面、实际命中和结束清理。"""
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    output = Path(tempfile.mkdtemp(prefix='construction-allocation-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    source = Path(__file__).with_suffix('.script').read_text(encoding='utf-8')
    script.write_text(source.replace('__ALLOCATION_FIXTURE__', json.dumps(str(fixture))), encoding='utf-8')
    baseline.connect(False, validate_runtime=False)
    try:
        write_json(output / 'result.json', baseline.call('run', str(script), '90000'))
    except RuntimeError as error:
        write_json(output / 'failure.json', {'error': str(error)})
        raise RuntimeError(f'波面实机复验失败，见 {output}') from error
    print(output)


if __name__ == '__main__':
    main()
