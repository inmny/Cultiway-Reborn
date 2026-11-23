#!/usr/bin/env python3
"""在独立游戏中复验地块候选和同刻命中。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('world', 'ordered'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    output = Path(tempfile.mkdtemp(prefix=f'contact-candidates-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    source = Path(__file__).with_suffix('.script').read_text(encoding='utf-8')
    script.write_text(source.replace('__CONTACT_BATCH_FIXTURE__', json.dumps(str(fixture))).replace(
        '__CONTACT_SWEEP_SECTION__', json.dumps(args.section)), encoding='utf-8')
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '90000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'error': str(error)})
        raise RuntimeError(f'候选实机复验失败，见 {output}') from error
    write_json(output / 'result.json', result)
    print(output)


if __name__ == '__main__':
    main()
