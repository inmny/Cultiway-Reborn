#!/usr/bin/env python3
"""从正式旧档复验植被催生、净土、召雨和催熟。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run
from verify_terrain_variants import FORMS as UTILITY_ASSETS

FORMS = {
    'NatureGrowthField': 'utility_young_growth',
    'CleanLandField': 'world_domain',
    'Fertilize': 'utility_ripe_grain',
    'SummonRainCloud': 'utility_rain_condensate',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('cold',), default='cold')
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--baseline', type=Path, required=True)
    parser.add_argument('--spell', choices=tuple(FORMS), action='append')
    args = parser.parse_args()
    if not all((args.archive / name).is_file() for name in ('expected.json', 'archive/map.wbox')):
        parser.error('需要原正式世界的输出目录')
    if not args.baseline.is_file():
        parser.error('需要旧执行的 result.json')

    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    start = common.index('var catalog = new[]')
    end = common.index('bool CanTestTracking', start)
    entries = ',\n'.join(f'    (Asset: SkillEntities.{asset}, Form: "{form}", Count: 1)'
                         for asset, form in FORMS.items())
    common = common[:start] + 'var catalog = new[]\n{\n' + entries + '\n};\n' + common[end:]
    selection = ', '.join(json.dumps('Cultiway.' + spell) for spell in args.spell or FORMS)
    inputs = f'string[] utilitySpells = {{ {selection} }};\n'
    inputs += 'const string baselinePath = ' + json.dumps(str(args.baseline.resolve())) + ';\n'
    with TemporaryDirectory(prefix='world-utility-') as directory:
        body = Path(directory) / 'verify_world_utility.script'
        body.write_text(common + inputs + (scripts / body.name).read_text())
        run(tuple('Cultiway.' + asset for asset in UTILITY_ASSETS), body, [], args.case, args.archive.resolve())


if __name__ == '__main__':
    main()
