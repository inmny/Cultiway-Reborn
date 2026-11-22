#!/usr/bin/env python3
"""用同一份正式档分段录制阵门、镇宗宝脉和元神台。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import write_json
from record_body_connections import encode
from verify_weapon_flight_archive import IMPORTS, world_helpers


CASES = {
    'array': '传送阵 · 同批旅客和实际出口',
    'sect': '镇宗宝脉 · 供奉场所接替',
    'node': '元神台 · 实际抵达和护持中断',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=CASES, required=True)
    parser.add_argument('--archive', type=Path, required=True, help='已通过的设施正式档准备阶段输出目录')
    args = parser.parse_args()
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        parser.error('请在独立游戏中使用 dev.py exec')
    prepared = args.archive.resolve()
    archive = prepared / 'archive'
    receipt = json.loads((prepared / 'result.json').read_text())
    digest = hashlib.sha256((archive / 'map.wbox').read_bytes()).hexdigest()
    if not receipt['passed'] or receipt['sha256'] != digest:
        parser.error('须使用已验证的原正式文件')
    case = args.case
    output = Path(tempfile.mkdtemp(prefix=f'facility-film-{case}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = world_helpers(output, archive, restore=True)
    start = helpers.index('for (int x = 60;')
    helpers = helpers[:start] + helpers[helpers.index('var holdStats =', start):]
    helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
    helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    folder = Path(__file__).parent
    script = output / 'input.script'
    script.write_text('using Cultiway;\nusing Cultiway.Const;\nusing strings;\n' + IMPORTS +
                      'using Cultiway.Core.Pathfinding;\nusing Cultiway.Content.Systems.Logic;\n' + helpers +
                      f'\nconst string scene = "{case}";\n' +
                      f'var saved = JObject.Parse(File.ReadAllText({json.dumps(str(prepared / "expected.json"))}));\n' +
                      (folder / 'record_independent_casting_camera.script').read_text() +
                      Path(__file__).with_suffix('.script').read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'录像记录见 {output}') from error
    write_json(output / 'result.json', dict(result, sha256=digest))
    if not result['passed']:
        raise SystemExit(f'录像场景未通过，见 {output}')
    crop = 'crop=iw*0.78:ih*0.78:iw*0.10:ih*0.22' if case == 'array' else None
    encode(case, output, output, result['view'] == 'native', label=CASES[case], milestone='10.12 实机记录',
           crop_worldfall=crop is not None, worldfall_crop=crop)
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
