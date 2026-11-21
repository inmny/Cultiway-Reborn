#!/usr/bin/env python3
"""对照旧爆发和共同爆发真实提交的地块与建筑。"""
import argparse
import json
from pathlib import Path
from tempfile import TemporaryDirectory

from verify_legacy_variants import run

SPELLS = {
    'GroundSpike': (4, 0, False),
    'Rift': (1, 0, False),
    'LightningStrike': (11, 0, False),
    'Icefall': (1, 0, False),
    'FallingRock': (3, 1, False),
    'FallingTimber': (2, 1, False),
    'FlameMeteor': (1, 0, False),
    'Fireball': (18, 0, True),
    'ThunderOrb': (4, 0, True),
    'ChaosOrb': (1, 0, True),
}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--case', choices=('archive', 'legacy', 'cold'), required=True)
    parser.add_argument('--archive', type=Path)
    parser.add_argument('--baseline', type=Path)
    parser.add_argument('--spell', choices=tuple(SPELLS), action='append')
    parser.add_argument('--objects', action='store_true', help='启用稳定的建筑与人物代表样本')
    args = parser.parse_args()
    if args.case != 'archive' and (not args.archive or not all((args.archive / file).is_file()
            for file in ('expected.json', 'archive/map.wbox'))):
        parser.error('需要正式世界的输出目录')
    if args.case == 'cold' and (not args.baseline or not args.baseline.is_file()):
        parser.error('需要旧执行的 result.json')

    scripts = Path(__file__).parent
    common = (scripts / 'verify_projectile_variants.script').read_text().split('async Task PlaySpike(', 1)[0]
    start = common.index('var catalog = new[]')
    end = common.index('bool CanTestTracking', start)
    entries = ',\n'.join(f'    (Asset: SkillEntities.{spell}, Form: "pivot", Count: {value[0]})'
                         for spell, value in SPELLS.items())
    common = common[:start] + 'var catalog = new[]\n{\n' + entries + '\n};\n' + common[end:]
    common = common.replace('bool cold = caseName == "cold", archive',
                            'bool cold = caseName is "cold" or "legacy", archive')
    common = common.replace('    Check(process != (int)expected["process"]',
                            '    if (caseName == "cold") Check(process != (int)expected["process"]')
    selected = args.spell or SPELLS
    uses = ',\n'.join(f'    (Asset: SkillEntities.{spell}, Index: {SPELLS[spell][1]}, Collision: {str(SPELLS[spell][2]).lower()})'
                      for spell in selected)
    inputs = 'var areaSpells = new[]\n{\n' + uses + '\n};\n'
    inputs += f'const bool areaObjects = {str(args.objects).lower()};\n'
    inputs += 'const string groundBaselinePath = ' + (json.dumps(str(args.baseline.resolve())) if args.baseline else 'null') + ';\n'
    with TemporaryDirectory(prefix='area-world-effects-') as directory:
        body = Path(directory) / 'verify_legacy_area_impacts.script'
        body.write_text(common + inputs + (scripts / body.name).read_text())
        run(tuple('Cultiway.' + spell for spell in SPELLS), body, [], args.case,
            args.archive.resolve() if args.archive else None)


if __name__ == '__main__':
    main()
