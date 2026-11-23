#!/usr/bin/env python3
"""复用现成世界，分别复验长期条件、地块和近身的真实更新时间。"""

import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('standing', 'terrain', 'melee'), required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请通过独立游戏的 dev.py exec 运行')

    import baseline_scenarios as baseline

    folder = Path(tempfile.mkdtemp(prefix=f'construction-time-{args.section}-',
                                   dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = Path(__file__).with_name('verify_contact_candidates.script').read_text().split(
        'if (__CONTACT_SWEEP_SECTION__', 1)[0]
    helpers = helpers.replace('__CONTACT_BATCH_FIXTURE__', json.dumps(str(fixture)))
    source = Path(__file__).with_suffix('.script').read_text().replace(
        '__CONSTRUCTION_TIME_SECTION__', json.dumps(args.section))
    script = folder / 'input.script'
    script.write_text('using Cultiway.Content;\nusing Cultiway.Content.Components;\n'
                      + helpers + '\n' + source, encoding='utf-8')
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '90000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise RuntimeError(f'实机复验失败，见 {folder}')
    print(folder)


if __name__ == '__main__':
    main()
