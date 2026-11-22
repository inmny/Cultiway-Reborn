#!/usr/bin/env python3
"""在独立游戏中复验世界来源的瞬时和持续功能法术。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('terrain', 'field', 'all'), default='all')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    world = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    output = Path(tempfile.mkdtemp(prefix='utility-world-source-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    script.write_text(Path(__file__).with_suffix('.script').read_text().replace(
        '__UTILITY_WORLD_FIXTURE__', json.dumps(str(world))).replace('__UTILITY_WORLD_SECTION__', json.dumps(args.section)))
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '95000')
    except RuntimeError as error:
        write_json(output / 'failure.json', {'error': str(error)})
        raise RuntimeError(f'世界来源复验失败，见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'世界来源复验未通过，见 {output}')
    print(output)


if __name__ == '__main__':
    main()
