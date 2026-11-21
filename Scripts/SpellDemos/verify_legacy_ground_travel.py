#!/usr/bin/env python3
"""从正式学习入口对照旧攻击实际走过的地面。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

SPELLS = ('PoisonNeedle', 'IceLance', 'Tornado', 'PoisonPool')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('legacy', 'current'), required=True)
    parser.add_argument('--baseline', type=Path)
    parser.add_argument('--spell', choices=SPELLS, action='append')
    args = parser.parse_args()
    if args.case == 'current' and (args.baseline is None or not args.baseline.is_file()):
        parser.error('需要原执行的 result.json')

    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    spells = ', '.join(f'SkillEntities.{spell}' for spell in args.spell or SPELLS)
    inputs = f'var travelSpells = new[] {{ {spells} }};\n'
    baseline = json.dumps(str(args.baseline.resolve())) if args.baseline else 'null'
    inputs += f'const string travelBaseline = {baseline};\n'
    with TemporaryDirectory(prefix='legacy-ground-travel-') as directory:
        body = Path(directory) / 'verify_legacy_ground_travel.script'
        body.write_text(common + inputs + (scripts / body.name).read_text())
        run(tuple(f'Cultiway.{spell}' for spell in SPELLS), body, [], args.case)


if __name__ == '__main__':
    main()
