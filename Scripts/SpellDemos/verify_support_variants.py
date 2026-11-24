#!/usr/bin/env python3
"""在同一份正式旧档中对照和重放六种友方术。"""
import argparse
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

FORMS = {
    'HealingLight': 'healing_seed',
    'BattleBlessing': 'battle_knot',
    'GuardBlessing': 'guard_seal',
    'HasteBlessing': 'haste_vanes',
    'PurificationWave': 'purification_surge',
    'RejuvenationField': 'rejuvenation_ground',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('archive', 'cold'), required=True)
    parser.add_argument('--archive', type=Path, help='原正式保存输出目录')
    parser.add_argument('--section', choices=('all', 'area', 'single'), default='all')
    args = parser.parse_args()
    if args.case != 'archive' and (not args.archive or
        not (args.archive / 'expected.json').is_file() or not (args.archive / 'archive/map.wbox').is_file()):
        parser.error('重放需要 --archive <原正式保存输出目录>')
    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    start = common.index('var catalog = new[]')
    end = common.index('bool CanTestTracking', start)
    entries = ',\n'.join(f'    (Asset: SkillEntities.{asset}, Form: "{form}", Count: 1)' for asset, form in FORMS.items())
    common = common[:start] + 'var catalog = new[]\n{\n' + entries + '\n};\n' + common[end:]
    with TemporaryDirectory(prefix='support-variants-') as directory:
        body = Path(directory) / 'verify_support_variants.script'
        body.write_text(common + f'const string section = "{args.section}";\n' + (scripts / body.name).read_text())
        run(tuple('Cultiway.' + asset for asset in FORMS), body, [], args.case,
            args.archive.resolve() if args.archive else None)


if __name__ == '__main__':
    main()
