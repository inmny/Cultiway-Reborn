#!/usr/bin/env python3
"""用同一座完工房屋对照攻击的真实建筑接触。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

FLIGHT = ('WaterArrow', 'Fireball', 'RockProjectile')
SUSTAINED = ('SolarBeam', 'PoisonPool', 'FireWall', 'WindWall')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('legacy', 'current'), required=True)
    parser.add_argument('--baseline', type=Path)
    parser.add_argument('--group', choices=('flight', 'sustained'), default='flight')
    parser.add_argument('--spell', choices=FLIGHT + SUSTAINED, action='append')
    args = parser.parse_args()
    if args.case == 'current' and (args.baseline is None or not args.baseline.is_file()):
        parser.error('需要原执行的 result.json')
    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    names = args.spell or (FLIGHT if args.group == 'flight' else SUSTAINED)
    selection = ', '.join(f'SkillEntities.{name}' for name in names)
    baseline = json.dumps(str(args.baseline.resolve())) if args.baseline else 'null'
    inputs = f'var buildingSpells = new[] {{ {selection} }};\nconst string buildingBaseline = {baseline};\n'
    with TemporaryDirectory(prefix='legacy-building-flight-') as directory:
        script = Path(directory) / 'verify_legacy_building_flight.script'
        script.write_text(common + inputs + (scripts / script.name).read_text())
        run(tuple(f'Cultiway.{name}' for name in names), script, [], args.case)


if __name__ == '__main__':
    main()
