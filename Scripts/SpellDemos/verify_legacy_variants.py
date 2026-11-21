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
    projectile_spells = {
        'water-arrow': ('Cultiway.WaterArrow', 4),
        'water-orb': ('Cultiway.WaterOrb', 5),
        'water-blade': ('Cultiway.WaterBlade', 4),
        'flying-sword': ('Cultiway.FlyingSword', 2),
        'sword-qi': ('Cultiway.SwordQi', 2),
        'golden-needle': ('Cultiway.GoldenNeedle', 1),
        'wind-bullet': ('Cultiway.WindBullet', 2),
        'wind-blade': ('Cultiway.WindBlade', 2),
        'thunder-orb': ('Cultiway.ThunderOrb', 4),
        'lightning-spear': ('Cultiway.LightningSpear', 1),
        'lightning-strike': ('Cultiway.LightningStrike', 11),
        'ice-shard': ('Cultiway.IceShard', 1),
        'ice-lance': ('Cultiway.IceLance', 1),
        'icefall': ('Cultiway.Icefall', 1),
        'rock-projectile': ('Cultiway.RockProjectile', 1),
        'stone-cone': ('Cultiway.StoneCone', 1),
        'falling-rock': ('Cultiway.FallingRock', 3),
        'wood-spear': ('Cultiway.WoodSpear', 2),
        'falling-timber': ('Cultiway.FallingTimber', 2),
        'spirit-bolt': ('Cultiway.SpiritBolt', 1),
        'yin-bolt': ('Cultiway.YinBolt', 1),
        'yang-bolt': ('Cultiway.YangBolt', 1),
        'chaos-orb': ('Cultiway.ChaosOrb', 1),
        'poison-needle': ('Cultiway.PoisonNeedle', 1),
        'yin-blade': ('Cultiway.YinBlade', 1),
        'flame-meteor': ('Cultiway.FlameMeteor', 1),
        'ground-spike': ('Cultiway.GroundSpike', 4),
        'earth-ridge': ('Cultiway.EarthRidge', 1),
        'tidal-wave': ('Cultiway.TidalWave', 1),
        'spirit-vine': ('Cultiway.SpiritVine', 1),
    }
    batches = {
        'water-family': ('water-arrow', 'water-orb', 'water-blade'),
        'metal-wind': ('flying-sword', 'sword-qi', 'golden-needle', 'wind-bullet', 'wind-blade'),
        'thunder-projectiles': ('thunder-orb', 'lightning-spear'),
        'ice-projectiles': ('ice-shard', 'ice-lance', 'icefall'),
        'earth-projectiles': ('rock-projectile', 'stone-cone', 'falling-rock'),
        'wood-projectiles': ('wood-spear', 'falling-timber'),
        'energy-bolts': ('spirit-bolt', 'yin-bolt', 'yang-bolt', 'chaos-orb'),
        'needle-blade-meteor': ('poison-needle', 'yin-blade', 'flame-meteor'),
        'ground-forms': ('ground-spike', 'earth-ridge', 'tidal-wave', 'spirit-vine'),
        'ground-waves': ('earth-ridge', 'tidal-wave', 'spirit-vine'),
    }
    parser.add_argument('spell', choices=('fireball', 'fire-family', *batches, *projectile_spells))
    parser.add_argument('--variant', type=int, choices=range(18), action='append',
                        help='只复验指定旧索引，可重复指定')
    ground_checks = ('legacy', 'legacy-edge', 'edge')
    parser.add_argument('--case', choices=('hit', 'miss', 'tracking', 'landing', 'cancel', 'scatter', 'gravity', 'archive', 'cold', *ground_checks))
    parser.add_argument('--archive', type=Path, help='冷启动使用之前实际保存的目录')
    args = parser.parse_args()
    if args.spell in batches or args.spell in projectile_spells:
        case = args.case or 'hit'
        save_batch = args.spell in batches or args.spell == 'lightning-strike' and case in ('archive', 'cold')
        if save_batch:
            if args.variant or case not in ('hit', 'archive', 'cold'):
                parser.error('批次支持 hit、archive 或 cold。指定变体时请选择单术')
            selected = tuple(projectile_spells[spell][0] for spell in batches.get(args.spell, (args.spell,)))
            variants = []
        else:
            asset, count = projectile_spells[args.spell]
            if case not in ('hit', 'miss', 'tracking', 'landing', 'cancel', 'scatter', *ground_checks) or any(i >= count for i in args.variant or []):
                parser.error('单术实测需选择该法术已有的旧索引')
            if case in ground_checks and args.spell != 'ground-spike':
                parser.error('旧执行对照与圆形边界复验使用地刺')
            if case == 'tracking' and args.spell not in ('water-orb', 'wind-bullet', 'thunder-orb', 'wood-spear', 'yin-bolt', 'poison-needle'):
                parser.error('改轨迹复验使用水球、风弹、雷珠、木枪、阴煞弹或毒针')
            if case == 'landing' and args.spell not in ('thunder-orb', 'lightning-strike', 'falling-rock', 'falling-timber', 'chaos-orb', 'flame-meteor'):
                parser.error('指定地点爆发复验使用雷珠、落雷、落石、落木、混沌珠或炎陨')
            if case == 'scatter' and args.spell != 'icefall':
                parser.error('重复施放核对散落使用冰雨')
            if case == 'cancel' and args.spell != 'lightning-strike':
                parser.error('取消复验使用落雷')
            selected = (asset,)
            default_index = min(1, count - 1)
            if case in ('landing', 'cancel') and args.spell in ('lightning-strike', 'falling-rock', 'falling-timber'):
                default_index = 0
            variants = args.variant or (list(range(count)) if case in ('hit', *ground_checks) else [default_index])
        archive = args.archive.resolve() if args.archive else None
        if case == 'cold' and (not archive or not all(
                (archive / file).is_file() for file in ('archive/map.wbox', 'expected.json'))):
            parser.error('冷启动需要 --archive <保存输出目录>')
        if archive and case != 'cold':
            parser.error('--archive 用于 cold')
        run(selected, Path(__file__).with_name('verify_projectile_variants.script'), variants, case, archive)
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
