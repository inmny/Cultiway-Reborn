using Cultiway.Abstract;
using Cultiway.Core.SkillLibV3.Visuals;
using UnityEngine;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;
using ModifierTag = Cultiway.Core.SkillLibV3.SkillTags.Modifier;
using SeriesTag = Cultiway.Core.SkillLibV3.SkillTags.Series;
using SimilarityTag = Cultiway.Core.SkillLibV3.SkillTags.Similarity;

namespace Cultiway.Content;

public class SkillVfxElements : ExtendLibrary<SkillVfxElementAsset, SkillVfxElements>
{
    public static SkillVfxElementAsset Generic { get; private set; }
    public static SkillVfxElementAsset Metal { get; private set; }
    public static SkillVfxElementAsset Wood { get; private set; }
    public static SkillVfxElementAsset Water { get; private set; }
    public static SkillVfxElementAsset Ice { get; private set; }
    public static SkillVfxElementAsset Fire { get; private set; }
    public static SkillVfxElementAsset Earth { get; private set; }
    public static SkillVfxElementAsset Neg { get; private set; }
    public static SkillVfxElementAsset Pos { get; private set; }
    public static SkillVfxElementAsset Entropy { get; private set; }
    public static SkillVfxElementAsset Wind { get; private set; }
    public static SkillVfxElementAsset Lightning { get; private set; }
    public static SkillVfxElementAsset Poison { get; private set; }
    public static SkillVfxElementAsset Explosion { get; private set; }
    public static SkillVfxElementAsset Burnout { get; private set; }
    public static SkillVfxElementAsset Gravity { get; private set; }
    public static SkillVfxElementAsset Curse { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.SkillVfxElement";

    protected override void OnInit()
    {
        Generic.SetAccent(Color.white)
            .MatchAny(0, ElementTag.Generic);
        Metal.SetAccent(new Color(1f, 0.95f, 0.55f), 0.45f, 0.78f)
            .MatchAny(10, ElementTag.Iron, SeriesTag.Metal)
            .SetGroundImpact(ApplyMetalImpact);
        Wood.SetAccent(new Color(0.55f, 1f, 0.35f), 0.42f, 0.78f)
            .MatchAny(10, ElementTag.Wood)
            .SetGroundImpact(ApplyWoodImpact);
        Water.SetAccent(new Color(0.52f, 0.9f, 1f), 0.45f, 0.78f)
            .MatchAny(10, ElementTag.Water)
            .SetGroundFlyOver(ApplyWaterFlyOver)
            .SetGroundImpact(ApplyWaterImpact);
        Ice.SetAccent(new Color(0.82f, 1f, 1f), 0.55f, 0.82f)
            .MatchAny(80, ElementTag.Ice, ModifierTag.Freeze, SimilarityTag.Freeze)
            .SetGroundFlyOver(ApplyIceFlyOver)
            .SetGroundImpact(ApplyIceImpact);
        Fire.SetAccent(new Color(1f, 0.82f, 0.35f), 0.42f, 0.8f)
            .MatchAny(20, ElementTag.Fire, ModifierTag.Burn, SimilarityTag.Burn)
            .SetGroundFlyOver(ApplyFireFlyOver)
            .SetGroundImpact(ApplyFireImpact);
        Earth.SetAccent(new Color(0.92f, 0.68f, 0.36f), 0.4f, 0.78f)
            .MatchAny(10, ElementTag.Earth)
            .SetGroundImpact(ApplyEarthImpact);
        Neg.SetAccent(new Color(0.52f, 0.22f, 0.88f), 0.48f, 0.8f)
            .MatchAny(10, ElementTag.Neg)
            .SetGroundImpact(ApplyNegImpact);
        Pos.SetAccent(new Color(1f, 0.96f, 0.42f), 0.45f, 0.8f)
            .MatchAny(10, ElementTag.Pos)
            .SetGroundImpact(ApplyPosImpact);
        Entropy.SetAccent(new Color(1f, 0.22f, 0.92f), 0.5f, 0.82f)
            .MatchAny(68, ElementTag.Entropy, ModifierTag.Chaos, ModifierTag.RandomAffix,
                ModifierTag.ReincarnationTrial)
            .SetGroundImpact(ApplyEntropyImpact);
        Wind.SetAccent(new Color(0.78f, 1f, 0.92f), 0.35f, 0.72f)
            .MatchAny(30, ElementTag.Wind)
            .MatchAll(28, ElementTag.Water, ElementTag.Wood)
            .SetGroundImpact(ApplyWindImpact);
        Lightning.SetAccent(new Color(0.72f, 0.95f, 1f), 0.55f, 0.86f)
            .MatchAny(30, ElementTag.Lightning)
            .MatchAll(28, ElementTag.Water, ElementTag.Fire)
            .SetGroundImpact(ApplyLightningImpact);
        Poison.SetAccent(new Color(0.58f, 0.95f, 0.2f), 0.52f, 0.82f)
            .MatchAny(70, ElementTag.Poison, ModifierTag.Poison, SimilarityTag.Poison)
            .SetGroundFlyOver(ApplyPoisonFlyOver)
            .SetGroundImpact(ApplyPoisonImpact);
        Explosion.SetAccent(new Color(1f, 0.52f, 0.18f), 0.55f, 0.86f)
            .MatchAny(60, ModifierTag.Explosion, SimilarityTag.Blast)
            .SetGroundImpact(ApplyExplosionImpact);
        Burnout.SetAccent(new Color(1f, 0.26f, 0.08f), 0.62f, 0.9f)
            .MatchAny(75, ModifierTag.Burnout)
            .SetGroundFlyOver(ApplyBurnoutFlyOver)
            .SetGroundImpact(ApplyBurnoutImpact);
        Gravity.SetAccent(new Color(0.34f, 0.22f, 0.72f), 0.52f, 0.82f)
            .MatchAny(65, ModifierTag.Gravity, SimilarityTag.Pull)
            .SetGroundImpact(ApplyGravityImpact);
        Curse.SetAccent(new Color(0.38f, 0.05f, 0.5f), 0.58f, 0.84f)
            .MatchAny(76, ModifierTag.DeathSentence, ModifierTag.EternalCurse, SimilarityTag.Curse,
                SimilarityTag.Execute)
            .SetGroundImpact(ApplyCurseImpact);
    }

    private static void ApplyArea(WorldTile tile, int rad, bool isArea, System.Action<WorldTile> action)
    {
        if (isArea && rad > 0)
        {
            SkillGroundFx.ForEachTileInRadius(tile, rad, action);
            return;
        }

        action(tile);
    }

    private static void ApplyWaterFlyOver(WorldTile tile)
    {
        if (tile.isOnFire() || tile.burned_stages > 0 || tile.isTemporaryFrozen() || tile.Type.lava)
        {
            ApplyWaterToTile(tile);
        }
    }

    private static void ApplyWaterImpact(WorldTile tile, int rad, bool isArea)
    {
        ApplyArea(tile, rad, isArea, ApplyWaterToTile);
    }

    private static void ApplyWaterToTile(WorldTile tile)
    {
        MapAction.terraformTile(tile, tile.main_type, tile.top_type, TerraformLibrary.water_fill, pSkipTerraform: true);
        tile.removeBurn();
        if (tile.Type.lava)
        {
            LavaHelper.putOut(tile);
        }

        if (tile.Type.can_be_filled_with_ocean)
        {
            MapAction.setOcean(tile);
        }
    }

    private static void ApplyIceFlyOver(WorldTile tile)
    {
        tile.freeze(1);
    }

    private static void ApplyIceImpact(WorldTile tile, int rad, bool isArea)
    {
        ApplyArea(tile, rad, isArea, t => t.freeze(3));
    }

    private static void ApplyFireImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            MapAction.damageWorld(tile, rad, WorldboxGame.Terraforms.HitGround);
            return;
        }

        tile.startFire(true);
        tile.setBurned();
    }

    private static void ApplyFireFlyOver(WorldTile tile)
    {
        tile.setBurned();
    }

    private static void ApplyEarthImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            MapAction.damageWorld(tile, rad, AssetManager.terraform.get("earthquake"));
            return;
        }

        MapAction.decreaseTile(tile, true);
    }

    private static void ApplyMetalImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            SkillGroundFx.ForEachTileInRadius(tile, rad, t => MapAction.decreaseTile(t, true));
            return;
        }

        if (tile.top_type != null)
        {
            tile.setTopTileType(null);
            return;
        }

        MapAction.decreaseTile(tile, true);
    }

    private static void ApplyWoodImpact(WorldTile tile, int rad, bool isArea)
    {
        ApplyArea(tile, rad, isArea, ApplyGrowthToTile);
    }

    private static void ApplyWindImpact(WorldTile tile, int rad, bool isArea)
    {
        var forceRad = isArea && rad > 0 ? rad : 2;
        World.world.applyForceOnTile(tile, forceRad, 2f, pForceOut: true);
    }

    private static void ApplyLightningImpact(WorldTile tile, int rad, bool isArea)
    {
        if (rad > 0)
        {
            MapAction.damageWorld(tile, Mathf.Max(1, rad), AssetManager.terraform.get("lightning_power"));
            return;
        }

        tile.setBurned();
    }

    private static void ApplyNegImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            SkillGroundFx.ForEachTileInRadius(tile, rad, t =>
            {
                t.setBurnedStage(15);
                if (t.top_type == null)
                {
                    var wasteland = t.Height > 1 ? TopTileLibrary.wasteland_high : TopTileLibrary.wasteland_low;
                    t.setTopTileType(wasteland);
                }
            });
            return;
        }

        tile.setBurnedStage(15);
    }

    private static void ApplyPosImpact(WorldTile tile, int rad, bool isArea)
    {
        ApplyArea(tile, rad, isArea, ApplyPositiveToTile);
    }

    private static void ApplyEntropyImpact(WorldTile tile, int rad, bool isArea)
    {
        ApplyArea(tile, rad, isArea, ApplyEntropyToTile);
    }

    private static void ApplyPoisonFlyOver(WorldTile tile)
    {
        if (!tile.Type.liquid)
        {
            tile.setBurnedStage(Mathf.Max(tile.burned_stages, 3));
        }
    }

    private static void ApplyPoisonImpact(WorldTile tile, int rad, bool isArea)
    {
        ApplyArea(tile, rad, isArea, MapAction.checkAcidTerraform);
    }

    private static void ApplyExplosionImpact(WorldTile tile, int rad, bool isArea)
    {
        var range = isArea && rad > 0 ? Mathf.Max(1, rad) : 2;
        var terraform = range >= 3 ? "bomb" : "grenade";
        MapAction.damageWorld(tile, range, AssetManager.terraform.get(terraform));
    }

    private static void ApplyBurnoutFlyOver(WorldTile tile)
    {
        if (tile.Type.liquid) return;

        tile.startFire(true);
        tile.setBurnedStage(15);
    }

    private static void ApplyBurnoutImpact(WorldTile tile, int rad, bool isArea)
    {
        if (isArea && rad > 0)
        {
            MapAction.damageWorld(tile, Mathf.Max(1, rad), AssetManager.terraform.get("dragon_attack"));
            return;
        }

        tile.startFire(true);
        tile.setBurnedStage(15);
    }

    private static void ApplyGravityImpact(WorldTile tile, int rad, bool isArea)
    {
        var forceRad = isArea && rad > 0 ? Mathf.Max(1, rad) : 3;
        World.world.applyForceOnTile(tile, forceRad, 3f, pForceOut: false);
        if (!isArea || rad <= 0)
        {
            MapAction.decreaseTile(tile, true);
        }
    }

    private static void ApplyCurseImpact(WorldTile tile, int rad, bool isArea)
    {
        ApplyArea(tile, rad, isArea, ApplyCurseToTile);
    }

    private static void ApplyGrowthToTile(WorldTile tile)
    {
        tile.removeBurn();
        MapAction.increaseTile(tile, false);
        var biome = tile.getBiome();
        if (biome != null && biome.grow_vegetation_auto && Randy.randomChance(0.35f))
        {
            ActionLibrary.growRandomVegetation(tile, biome);
        }
    }

    private static void ApplyPositiveToTile(WorldTile tile)
    {
        tile.removeBurn();
        MapAction.increaseTile(tile, false);
    }

    private static void ApplyEntropyToTile(WorldTile tile)
    {
        if (Randy.randomBool())
        {
            MapAction.decreaseTile(tile, true);
            return;
        }

        tile.setBurnedStage(15);
    }

    private static void ApplyCurseToTile(WorldTile tile)
    {
        tile.setBurnedStage(15);
        if (tile.top_type == null && tile.Type.ground)
        {
            var wasteland = tile.Height > 1 ? TopTileLibrary.wasteland_high : TopTileLibrary.wasteland_low;
            tile.setTopTileType(wasteland);
        }
    }
}
