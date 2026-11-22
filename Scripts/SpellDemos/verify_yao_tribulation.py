#!/usr/bin/env python3
"""在独立游戏验证妖兽天劫的实际落点、承伤与正式存读。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', required=True, choices=('prepare', 'cold'))
    parser.add_argument('--archive', type=Path, help='prepare 写出的正式档所在目录')
    args = parser.parse_args()
    cold = args.section == 'cold'
    if cold != bool(args.archive):
        parser.error('仅冷读需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中使用 dev.py exec')
    output = Path(tempfile.mkdtemp(prefix=f'yao-tribulation-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    fixture = PROJECT / FIXTURE
    prepared = args.archive.resolve() if cold else output
    archive = prepared / 'archive'
    if cold:
        receipt = json.loads((prepared / 'result.json').read_text())
        if not receipt['passed'] or hashlib.sha256((archive / 'map.wbox').read_bytes()).hexdigest() != receipt['sha256']:
            parser.error('冷读需要同一份未改动的正式档')
    helpers = world_helpers(output, archive if cold else fixture, restore=cold)
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    script = output / 'input.script'
    expected = f'JObject.Parse(File.ReadAllText({json.dumps(str(prepared / "expected.json"))}))' if cold else 'null'
    script.write_text(IMPORTS + 'using Cultiway.Core.Progression;\n' + helpers +
                      f'\nJObject saved = {expected};\nvar originalFixture = {json.dumps(str(fixture))};\n' +
                      Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '120000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'妖兽天劫复验见 {output}') from error
    result['sha256'] = hashlib.sha256((archive / 'map.wbox').read_bytes()).hexdigest()
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'妖兽天劫未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
