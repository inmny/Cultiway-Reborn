#!/usr/bin/env python3
"""在原版或 Worldfall 的独立游戏内录制天劫、状态和环境的实际过程。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from record_body_connections import encode
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


CASES = {
    'yao': ('化形天劫与躲避', 'crop=iw*0.86:ih*0.86:iw*0.09:0'),
    'wind': ('煞风劫的实际来向', 'crop=iw*0.84:ih*0.84:iw*0.12:ih*0.08'),
    'status': ('周期伤害与回春', 'crop=iw*0.52:ih*0.52:iw*0.27:ih*0.22'),
    'rain': ('原版雨滴落地启灵', 'crop=iw*0.48:ih*0.48:iw*0.30:ih*0.23'),
    'weather': ('独立雨云继续降雨', None),
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=CASES, required=True)
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请在独立游戏中使用 dev.py exec')
    case = args.case
    output = Path(tempfile.mkdtemp(prefix=f'tribulation-environment-film-{case}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    folder = Path(__file__).parent
    helpers = world_helpers(output, PROJECT / FIXTURE)
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    script = output / 'input.script'
    script.write_text(IMPORTS + helpers + f'\nconst string scene = "{case}";\n' +
                      (folder / 'record_independent_casting_camera.script').read_text() +
                      Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '175000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'实机录像见 {output}') from error
    write_json(output / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'录像场景未通过，见 {output}')
    label, crop = CASES[case]
    encode(case, output, output, result['view'] == 'native', label=label, milestone='10.11 实机记录',
           crop_worldfall=crop is not None, worldfall_crop=crop)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
