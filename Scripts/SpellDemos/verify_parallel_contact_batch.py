#!/usr/bin/env python3
"""在独立游戏中核对批量采样、剑形交锋和换世界清理。"""
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
    output = Path(tempfile.mkdtemp(prefix='contact-batch-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    script.write_text(Path(__file__).with_suffix('.script').read_text().replace(
        '__CONTACT_BATCH_FIXTURE__', json.dumps(str(fixture))), encoding='utf-8')
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '90000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'error': str(error)})
        raise RuntimeError(f'批量采样实战复验失败，见 {output}') from error
    write_json(output / 'result.json', result)
    print(output)


if __name__ == '__main__':
    main()
