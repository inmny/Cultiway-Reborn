#!/usr/bin/env python3
"""对照持续领域的实际推力、水滴与地形变化。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

SPELLS = ('Tornado', 'YinField')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('current',), default='current')
    parser.add_argument('--baseline', type=Path, required=True)
    parser.add_argument('--spell', choices=SPELLS, action='append')
    args = parser.parse_args()
    if not args.baseline.is_file():
        parser.error('需要原执行的 result.json')
    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    names = args.spell or SPELLS
    spells = ', '.join(f'SkillEntities.{name}' for name in names)
    baseline = json.dumps(str(args.baseline.resolve()))
    inputs = f'var worldFields = new[] {{ {spells} }};\nconst string worldFieldBaseline = {baseline};\n'
    with TemporaryDirectory(prefix='legacy-field-world-') as directory:
        body = Path(directory) / 'verify_legacy_field_world.script'
        body.write_text(common + inputs + (scripts / body.name).read_text())
        run(tuple(f'Cultiway.{name}' for name in names), body, [], args.case)


if __name__ == '__main__':
    main()
