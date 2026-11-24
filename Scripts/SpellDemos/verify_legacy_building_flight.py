#!/usr/bin/env python3
"""用同一座完工房屋对照攻击的真实建筑接触。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

FLIGHT = ('WaterArrow', 'Fireball', 'RockProjectile')
SUSTAINED = ('SolarBeam', 'PoisonPool', 'FireWall', 'WindWall')
CHAIN = ('ChainLightning',)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('current',), default='current')
    parser.add_argument('--baseline', type=Path, required=True)
    groups = {'flight': FLIGHT, 'sustained': SUSTAINED, 'chain': CHAIN}
    parser.add_argument('--group', choices=tuple(groups), default='flight')
    parser.add_argument('--spell', choices=FLIGHT + SUSTAINED + CHAIN, action='append')
    scenarios = ('actor-relay', 'building-first', 'open')
    parser.add_argument('--chain-case', choices=scenarios, action='append')
    args = parser.parse_args()
    if not args.baseline.is_file():
        parser.error('需要原执行的 result.json')
    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    names = args.spell or groups[args.group]
    if args.chain_case and 'ChainLightning' not in names:
        parser.error('--chain-case 需要选择链雷')
    selection = ', '.join(f'SkillEntities.{name}' for name in names)
    baseline = json.dumps(str(args.baseline.resolve()))
    inputs = f'var buildingSpells = new[] {{ {selection} }};\nconst string buildingBaseline = {baseline};\n'
    cases = ', '.join(map(json.dumps, args.chain_case or scenarios))
    inputs += f'var chainScenarios = new[] {{ {cases} }};\n'
    with TemporaryDirectory(prefix='legacy-building-flight-') as directory:
        script = Path(directory) / 'verify_legacy_building_flight.script'
        script.write_text(common + inputs + (scripts / script.name).read_text())
        run(tuple(f'Cultiway.{name}' for name in names), script, [], args.case)


if __name__ == '__main__':
    main()
