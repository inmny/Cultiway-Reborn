#!/usr/bin/env python3
"""从正式旧档复验四种瞬时改地法术。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

FORMS = {
    'RaiseTerrain': 'terrain_raised_slab',
    'LowerTerrain': 'terrain_lowered_steps',
    'FillWater': 'world_domain',
    'DrainWater': 'world_domain',
    'NatureGrowthField': '',
    'CleanLandField': '',
    'SummonRainCloud': '',
    'Fertilize': '',
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('legacy', 'cold'), required=True)
    parser.add_argument('--archive', type=Path, required=True)
    parser.add_argument('--baseline', type=Path, help='旧执行生成的 result.json')
    parser.add_argument('--spell', choices=tuple(FORMS)[:4], action='append')
    args = parser.parse_args()
    if not all((args.archive / name).is_file() for name in ('expected.json', 'archive/map.wbox')):
        parser.error('需要包含原正式世界的目录')
    if args.case == 'cold' and (not args.baseline or not args.baseline.is_file()):
        parser.error('改后实测需要旧执行留下的 result.json')

    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    start = common.index('var catalog = new[]')
    end = common.index('bool CanTestTracking', start)
    entries = ',\n'.join(f'    (Asset: SkillEntities.{asset}, Form: "{form}", Count: 1)'
                         for asset, form in FORMS.items())
    common = common[:start] + 'var catalog = new[]\n{\n' + entries + '\n};\n' + common[end:]
    common = common.replace('bool cold = caseName == "cold", archive', 'bool cold = true, archive')
    selected = ', '.join(json.dumps('Cultiway.' + name) for name in (args.spell or list(FORMS)[:4]))
    inputs = f'string[] terrainSpells = {{ {selected} }};\n'
    inputs += f'const string baselinePath = {json.dumps(str(args.baseline.resolve())) if args.baseline else "null"};\n'
    with TemporaryDirectory(prefix='terrain-variants-') as directory:
        body = Path(directory) / 'verify_terrain_variants.script'
        body.write_text(common + inputs + (scripts / body.name).read_text())
        run(tuple('Cultiway.' + asset for asset in FORMS), body, [], args.case, args.archive.resolve())


if __name__ == '__main__':
    main()
