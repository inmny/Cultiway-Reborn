#!/usr/bin/env python3
"""复验由原建筑受理的传送阵充能、取消及实际抵达。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--travel', type=Path, help='同一游戏进程刚完成的充能复验 result.json')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中使用 dev.py exec')
    prefix = 'teleport-array-travel-' if args.travel else 'teleport-array-'
    output = Path(tempfile.mkdtemp(prefix=prefix, dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    script = output / 'input.script'
    helpers = world_helpers(output, PROJECT / FIXTURE)
    if args.travel:
        helpers = helpers.split('Game.Resume();', 1)[0].replace('ConstructedAbilityService.CastEnded += Ended;', '')
        scene = args.travel.resolve()
        helpers += f'\nvar scene = JObject.Parse(File.ReadAllText({json.dumps(str(scene))}));\n'
    body = 'verify_teleport_array_travel.script' if args.travel else 'verify_teleport_array.script'
    script.write_text(IMPORTS + 'using Cultiway.Core.Pathfinding;\nusing Cultiway.Content.Systems.Logic;\n' +
                      helpers + Path(__file__).with_name(body).read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '150000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'传送阵实机记录见 {output}') from error
    write_json(output / 'result.json', result)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
