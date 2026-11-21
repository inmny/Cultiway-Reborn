#!/usr/bin/env python3
"""通过正式学习和施放入口实测指定法术的旧索引。"""
import argparse
import json
import os
from pathlib import Path
import tempfile

from common import PROJECT, write_json


def run(asset_ids, body_path, variants, case='hit', archive=None):
    """复用同一份世界和推进工具，保留每次检查的实际输入和结果。"""
    if not os.getenv('CULTIWAY_DEV_ACTIVE'):
        raise SystemExit('请通过 dev.py exec 运行')

    import baseline_scenarios as baseline

    baseline.connect(False, validate_runtime=False)
    scripts = Path(__file__).parent
    folder = Path(tempfile.mkdtemp(prefix=f'{body_path.stem.removeprefix("verify_")}-',
                                  dir=os.environ['CULTIWAY_DEV_OUTPUT']))
    helpers = (scripts / 'verify_body_footwork.script').read_text().split('var bindingType =', 1)[0]
    helpers = helpers.replace('Path.Combine(root, "artifacts/skill_rebuild/body-footwork")', json.dumps(str(folder)))
    fixture = archive / 'archive' if archive else PROJECT / 'Scripts/WorldSaveTests/Fixtures/world-format-1'
    helpers = helpers.replace('Path.Combine(root, "Scripts/WorldSaveTests/Fixtures/world-format-1")', json.dumps(str(fixture)))
    identifiers = ', '.join(map(json.dumps, asset_ids))
    for event in ('fact', 'contact'):
        helpers = helpers.replace(f'{event}.AbilityId.StartsWith("test.body-footwork.")',
                                  f'new[] {{ {identifiers} }}.Contains({event}.AbilityId)')
    helpers = helpers.replace('var hold = Cultiway.Core.Libraries.StatusEffectAsset.StartBuild',
                              'var hold = Cultiway.ModClass.L.StatusEffectLibrary.get("Cultiway.Verify.SourceLoss.Hold") ?? '
                              'Cultiway.Core.Libraries.StatusEffectAsset.StartBuild')
    imports = ('using Cultiway.Content.Components.Skill;\nusing Cultiway.Core.SkillLibV3;\nusing Cultiway.Core.SkillLibV3.Components;\n'
               'using Cultiway.Core.SkillLibV3.Modifiers;\nusing Cultiway.Core.SkillLibV3.Settings;\n'
               'using Cultiway.Core.SkillLibV3.Utils;\nusing Cultiway.Core.SkillLibV3.Blueprints;\nusing Friflo.Engine.ECS;\n')
    inputs = 'int[] chosenVariants = { ' + ', '.join(map(str, variants)) + ' };\n'
    inputs += f'string[] chosenAssets = {{ {identifiers} }};\n'
    inputs += f'const string caseName = {json.dumps(case)};\n'
    inputs += f'const string archivePath = {json.dumps(str(archive)) if archive else "null"};\n'
    script = folder / 'input.script'
    script.write_text(imports + helpers + inputs + body_path.read_text())
    try:
        result = baseline.call('run', str(script), '180000')
    except RuntimeError as error:
        write_json(folder / 'result.json', dict(passed=False, error=str(error)))
        raise RuntimeError(f'旧变体实测见 {folder}') from error
    write_json(folder / 'result.json', result)
    if not result.get('passed'):
        raise SystemExit(f'旧变体实测未通过，见 {folder}')
    print(json.dumps(dict(output=str(folder), result=result), ensure_ascii=False))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    water = {'water-arrow': 'Cultiway.WaterArrow', 'water-orb': 'Cultiway.WaterOrb', 'water-blade': 'Cultiway.WaterBlade'}
    parser.add_argument('spell', choices=('fireball', 'fire-family', 'water-family', *water))
    parser.add_argument('--variant', type=int, choices=range(18), action='append',
                        help='只复验指定旧索引，可重复指定')
    parser.add_argument('--case', choices=('hit', 'miss', 'tracking', 'gravity', 'archive', 'cold'))
    parser.add_argument('--archive', type=Path, help='冷启动使用之前实际保存的目录')
    args = parser.parse_args()
    if args.spell == 'water-family' or args.spell in water:
        case = args.case or 'hit'
        family = args.spell == 'water-family'
        count = 5 if args.spell == 'water-orb' else 4
        if family and (args.variant or case not in ('hit', 'archive', 'cold')):
            parser.error('水系批次支持 hit、archive 或 cold。指定变体时请选择单术')
        if not family and (case not in ('hit', 'miss', 'tracking') or any(i >= count for i in args.variant or [])):
            parser.error('单术实测需选择该法术已有的旧索引，支持 hit、miss 或 tracking')
        if case == 'tracking' and args.spell != 'water-orb':
            parser.error('改轨迹复验使用水球')
        archive = args.archive.resolve() if args.archive else None
        if case == 'cold' and (not archive or not all(
                (archive / file).is_file() for file in ('archive/map.wbox', 'expected.json'))):
            parser.error('冷启动需要 --archive <保存输出目录>')
        if archive and case != 'cold':
            parser.error('--archive 用于 cold')
        selected = tuple(water.values()) if family else (water[args.spell],)
        variants = args.variant or (list(range(count)) if not family and case == 'hit' else [1] if not family else [])
        run(selected, Path(__file__).with_name('verify_water_variants.script'), variants, case, archive)
        return
    if args.spell == 'fire-family':
        if args.variant or args.case not in ('archive', 'cold'):
            parser.error('火系保存使用 --case archive 或 --case cold')
        archive = args.archive.resolve() if args.case == 'cold' and args.archive else None
        if args.case == 'cold' and (not archive or not all(
                (archive / file).is_file() for file in ('archive/map.wbox', 'expected.json'))):
            parser.error('冷启动需要 --archive <保存输出目录>')
        run(('Cultiway.Fireball', 'Cultiway.FireBlade'),
            Path(__file__).with_name('verify_fire_family_save.script'), [], args.case, archive)
        return
    case = args.case or 'hit'
    if args.archive or case not in ('hit', 'miss', 'tracking', 'gravity'):
        parser.error('火球实测使用 hit、miss、tracking 或 gravity')
    variants = args.variant or (range(18) if case == 'hit' else [0])
    run(('Cultiway.Fireball',), Path(__file__).with_name('verify_fireball_variants.script'), variants, case)


if __name__ == '__main__':
    main()
