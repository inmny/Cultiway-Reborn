#!/usr/bin/env python3
"""复验器官和妖丹的实际出手及被动效果。"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import tempfile
import zlib

import baseline_scenarios as baseline
from common import PROJECT, write_json
from record_body_connections import encode
from verify_weapon_flight_archive import FIXTURE, IMPORTS, world_helpers


SURVIVAL_SECTIONS = ('shell', 'phoenix', 'tails')
PASSIVE_SECTIONS = ('venom', 'regen', 'devour')
ARCHIVE_SECTIONS = ('prepare', 'cold')
FILM_SECTIONS = {
    'film-active': '雷角与狐火吐息',
    'film-shell': '玄甲受击',
    'film-phoenix': '涅槃与行动恢复',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--section', default='source',
                        choices=('source', 'natural-source', 'natural-core', 'core-sources', 'horn', 'mouth', 'torso', 'poison', *SURVIVAL_SECTIONS,
                                 *PASSIVE_SECTIONS, *ARCHIVE_SECTIONS, *FILM_SECTIONS))
    parser.add_argument('--archive', type=Path, help='冷读档使用 prepare 的输出目录')
    args = parser.parse_args()
    restoring = args.section == 'cold'
    if restoring != bool(args.archive):
        parser.error('仅 cold 阶段需要 --archive')
    if not os.environ.get('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过独立游戏的 dev.py exec 运行')
    output = Path(tempfile.mkdtemp(prefix=f'organ-ability-{args.section}-', dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    prepared = args.archive.resolve() if restoring else output
    archive = prepared / 'archive'
    world = archive if restoring else PROJECT / FIXTURE
    if restoring:
        receipt = json.loads((prepared / 'result.json').read_text())
        if not receipt['passed'] or hashlib.sha256((archive / 'map.wbox').read_bytes()).hexdigest() != receipt['archiveSha256']:
            parser.error('冷读档需要原封不动的正式验收档')
    script = output / 'input.script'
    name = 'verify_organ_outlets.script'
    if args.section in ('source', 'natural-source'):
        name = 'verify_organ_ability.script'
    elif args.section in ('natural-core', 'core-sources'):
        name = 'verify_natural_yao_core.script'
    elif args.section in SURVIVAL_SECTIONS:
        name = 'verify_organ_survival.script'
    elif args.section in PASSIVE_SECTIONS:
        name = 'verify_organ_passives.script'
    elif args.section in ARCHIVE_SECTIONS:
        name = 'verify_organ_archive.script'
    elif args.section in FILM_SECTIONS:
        name = 'record_organ_ability.script'
    body = Path(__file__).with_name(name)
    settings = (f'var saved = JObject.Parse(File.ReadAllText({json.dumps(str(prepared / "expected.json"))}));\n'
                if restoring else 'JObject saved = null;\n')
    helpers = world_helpers(output, world, restore=restoring)
    if args.section in ARCHIVE_SECTIONS or args.section in FILM_SECTIONS:
        helpers = helpers.replace('ConstructedAbilityService.CastEnded += Ended;', '')
        helpers = helpers.replace('ConstructedAbilityService.ContactConfirmed += Hit;', '')
    script.write_text(IMPORTS + 'using Cultiway.Content.CreatureCompositions.ActiveAbilities;\n'
                      + helpers
                      + f'const string section = "{args.section}";\n'
                      + (settings if args.section in ARCHIVE_SECTIONS else '')
                      + body.read_text())
    baseline.connect(False, validate_runtime=False)
    try:
        result = baseline.call('run', str(script), '115000' if args.section in FILM_SECTIONS else '90000')
    except RuntimeError as error:
        write_json(output / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'器官能力复验见 {output}') from error
    if result['passed'] and args.section in ARCHIVE_SECTIONS:
        map_bytes = (archive / 'map.wbox').read_bytes()
        if not restoring:
            expected = json.loads((output / 'expected.json').read_text())
            world_data = json.loads(zlib.decompress(map_bytes))
            entry = next(row for row in world_data['actors_data'] if row['id'] == expected['phoenix'])
            data = json.loads(entry['custom_data_string']['cultiway.actor'])['sections']['yao']['data']
            if data['Yao']['PhoenixRevivalUses'] != expected['uses'] or data['Nirvana']['ExpiresAt'] != expected['expiry']:
                raise RuntimeError(f'正式档中的涅槃状态不一致，见 {output}')
        result['archiveSha256'] = hashlib.sha256(map_bytes).hexdigest()
    write_json(output / 'result.json', result)
    if not result['passed']:
        raise SystemExit(f'器官能力复验未通过，见 {output}')
    if args.section in FILM_SECTIONS:
        encode(args.section.removeprefix('film-'), output, output, result['view'] == 'native',
               label=FILM_SECTIONS[args.section], milestone='10.09 组合器官实录',
               worldfall_crop='crop=iw*0.64:ih*0.64:iw*0.34:ih*0.20')
    print(json.dumps(dict(output=str(output), result=result), ensure_ascii=False))


if __name__ == '__main__':
    main()
