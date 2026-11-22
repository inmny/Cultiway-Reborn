#!/usr/bin/env python3
"""在独立游戏中复验命魂等实际施法来源。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile

import baseline_scenarios as baseline
from common import PROJECT, write_json
from record_body_connections import encode
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


FILMS = {
    'film-soul': '命魂与肉身交替出手',
    'film-node': '分念随动与归返',
    'film-summon': '原幡召唤物代施',
    'film-spirit': '原剑器灵代施',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', choices=('soul', 'travel-stances', 'thought-orders', 'soul-force', 'node', 'avatar', 'summon', 'artifact-spirit',
                                              'natural-summon', 'facility', 'prepare', 'cold', *FILMS), default='soul')
    parser.add_argument('--archive', type=Path, help='使用 prepare 写出的正式档目录')
    args = parser.parse_args()
    section = args.section
    cold = section == 'cold'
    if cold != bool(args.archive):
        parser.error('仅 cold 阶段需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')
    output = Path(tempfile.mkdtemp(prefix=f'independent-casting-{section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
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
    scene = '' if cold else Path(__file__).with_name('independent_casting_scene.script').read_text()
    if section in ('summon', 'artifact-spirit', 'natural-summon', 'prepare') or section in FILMS:
        scene += Path(__file__).with_name('independent_artifact_casting_scene.script').read_text()
    if section in ('prepare', 'cold'):
        scene += Path(__file__).with_name('verify_independent_casting_archive.script').read_text()
        if cold:
            scene += f'var originalFixture = {json.dumps(str(fixture))};\n'
            scene += f'var saved = JObject.Parse(File.ReadAllText({json.dumps(str(prepared / "expected.json"))}));\n'
        body = f'verify_independent_casting_{section}.script'
    elif section in ('summon', 'artifact-spirit'):
        body = 'verify_independent_artifact_casting.script'
    elif section in FILMS:
        scene += Path(__file__).with_name('record_independent_casting_camera.script').read_text()
        body = 'record_independent_casting.script'
    elif section == 'natural-summon':
        body = 'verify_natural_summon.script'
    elif section == 'travel-stances':
        body = 'verify_yuanshen_travel_stances.script'
    elif section == 'thought-orders':
        body = 'verify_yuanshen_thought_orders.script'
    elif section == 'facility':
        body = 'verify_facility_nodes.script'
    else:
        body = 'verify_independent_casting.script' if section == 'soul' else 'verify_independent_sources.script'
    script.write_text(IMPORTS + 'using Cultiway.Core.SkillLibV3.Utils;\n' + helpers
                      + f'const string section = "{section}";\n' + scene + Path(__file__).with_name(body).read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '145000' if section == 'avatar' or section in FILMS else '90000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'独立施法复验见 {output}') from error
    if result['passed'] and section in ('prepare', 'cold'):
        result['sha256'] = hashlib.sha256((archive / 'map.wbox').read_bytes()).hexdigest()
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'独立施法未通过，见 {output}')
    if section in FILMS:
        encode(section.removeprefix('film-'), output, output, result['view'] == 'native',
               label=FILMS[section], milestone='10.10 独立施法实录',
               worldfall_crop='crop=iw*0.52:ih*0.52:iw*0.27:ih*0.22')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
