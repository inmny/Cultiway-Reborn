#!/usr/bin/env python3
"""通过同一份正式存档复验持续环境、状态和已完成的环境结果。"""
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
    input_source = parser.add_mutually_exclusive_group(required=True)
    input_source.add_argument('--environment-result', type=Path, help='同一进程中已通过的环境复验 result.json')
    input_source.add_argument('--archive', type=Path, help='准备阶段写出的正式档所在目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中使用 dev.py exec')
    cold = args.archive is not None
    section = 'cold' if cold else 'prepare'
    output = Path(tempfile.mkdtemp(prefix=f'environment-archive-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    prepared = args.archive.resolve() if cold else output
    archive = prepared / 'archive' / 'map.wbox'
    source = prepared / 'expected.json' if cold else args.environment_result.resolve()
    if cold:
        receipt = json.loads((prepared / 'result.json').read_text())
        if not receipt['passed'] or hashlib.sha256(archive.read_bytes()).hexdigest() != receipt['sha256']:
            parser.error('冷读需要同一份未改动的正式档')
    elif not json.loads(source.read_text())['passed']:
        parser.error('保存阶段需要已通过的真实启灵与抬地结果')

    fixture = PROJECT / FIXTURE
    helpers = world_helpers(output, archive.parent if cold else fixture, restore=True)
    start = helpers.index('for (int x = 60;' if cold else 'World.world.save_manager.loadWorld(fixture);')
    finish = helpers.index('var holdStats =', start)
    helpers = helpers[:start] + helpers[finish:]
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    script = output / 'input.script'
    script.write_text(IMPORTS + helpers + f'\nconst bool cold = {str(cold).lower()};\n'
                      + f'var saved = JObject.Parse(File.ReadAllText({json.dumps(str(source))}));\n'
                      + f'var originalFixture = {json.dumps(str(fixture))};\n'
                      + Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '115000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'环境档复验的记录见 {output}') from error
    result['sha256'] = hashlib.sha256(archive.read_bytes()).hexdigest()
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'环境档未通过，见 {output}')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
