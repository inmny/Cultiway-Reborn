#!/usr/bin/env python3
"""对照已学攻击每次真实接触对应的原地块作用。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

SPELLS = ('FireBlade', 'ChainLightning')


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
    names = ', '.join(f'SkillEntities.{name}' for name in args.spell or SPELLS)
    baseline = json.dumps(str(args.baseline.resolve())) if args.baseline else 'null'
    inputs = f'var directSpells = new[] {{ {names} }};\nconst string directBaseline = {baseline};\n'
    with TemporaryDirectory(prefix='legacy-ground-contact-') as directory:
        script = Path(directory) / 'verify_legacy_ground_contact.script'
        script.write_text(common + inputs + (scripts / script.name).read_text())
        run(tuple(f'Cultiway.{name}' for name in SPELLS), script, [], args.case)


if __name__ == '__main__':
    main()
