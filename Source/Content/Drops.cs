using System;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Attributes;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;

namespace Cultiway.Content;
[Dependency(typeof(Buildings), typeof(TopTileTypes))]
public class Drops : ExtendLibrary<DropAsset, Drops>
{
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Enlighten { get; private set; }
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Slow { get; private set; }
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Poison { get; private set; }
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Burn { get; private set; }
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Freeze { get; private set; }
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Weaken { get; private set; }
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset ArmorBreak { get; private set; }
    // ---- Biome seeds: rain-drop that paints the biome via action_drop_seeds.
    // No [SetupButton]: these are wired to the BIOME tab explicitly in GodPowers, so they
    // must not also be picked up by SetupCommonDropPlacePower (which targets the DROP tab).
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_bamboo")]
    public static DropAsset SeedsBamboo { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_candle")]
    public static DropAsset SeedsCandle { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_cemetery")]
    public static DropAsset SeedsCemetery { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_coral")]
    public static DropAsset SeedsCoral { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_dark")]
    public static DropAsset SeedsDark { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_fern")]
    public static DropAsset SeedsFern { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_fleshblood")]
    public static DropAsset SeedsFleshBlood { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_knowledge")]
    public static DropAsset SeedsKnowledge { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_oak")]
    public static DropAsset SeedsOak { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_rice")]
    public static DropAsset SeedsRice { get; private set; }
    [CloneSource(DropsLibrary.TEMPLATE_BIOME_SEEDS), AssetId("seeds_titans")]
    public static DropAsset SeedsTitans { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        Enlighten.action_landed = CreateStatusDropAction(StatusEffects.Enlighten);
        Slow.action_landed = CreateStatusDropAction(StatusEffects.Slow);
        Poison.action_landed = CreateStatusDropAction(StatusEffects.Poison, e =>{
            e.GetComponent<StatusTickState>().Value += 1f;
            e.GetComponent<StatusTickState>().Element = ElementComposition.Static.Poison;
        });
        Burn.action_landed = CreateStatusDropAction(StatusEffects.Burn, e =>
        {
            e.GetComponent<StatusTickState>().Value = 10f;
            e.GetComponent<StatusTickState>().Element = ElementComposition.Static.Fire;
        });
        Freeze.action_landed = CreateStatusDropAction(StatusEffects.Freeze);
        Weaken.action_landed = CreateStatusDropAction(StatusEffects.Weaken);
        ArmorBreak.action_landed = CreateStatusDropAction(StatusEffects.ArmorBreak);

        SetupCommonBuildingPlaceDrop();
        SetupBiomeSeeds();
    }

    private const string SEED_SOUND = "event:/SFX/DROPS/DropSeedGrass";

    private static void SetupBiomeSeeds()
    {
        SetupBiomeSeed(SeedsBamboo,     "Bamboo");
        SetupBiomeSeed(SeedsCandle,     "Candle");
        SetupBiomeSeed(SeedsCemetery,   "Cemetery");
        SetupBiomeSeed(SeedsCoral,      "Coral");
        SetupBiomeSeed(SeedsDark,       "Dark");
        SetupBiomeSeed(SeedsFern,       "Fern");
        SetupBiomeSeed(SeedsFleshBlood, "FleshBlood");
        SetupBiomeSeed(SeedsKnowledge,  "Knowledge");
        SetupBiomeSeed(SeedsOak,        "Oak");
        SetupBiomeSeed(SeedsRice,       "Rice");
        SetupBiomeSeed(SeedsTitans,     "Titans");
    }

    private static void SetupBiomeSeed(DropAsset drop, string biome)
    {
        drop.path_texture = $"drops/{biome}_drop";
        drop.falling_speed = 3f;
        drop.action_landed = DropsLibrary.action_drop_seeds;
        drop.drop_type_high = $"{biome}_high";
        drop.drop_type_low = $"{biome}_low";
        // Populate the cached TopTileType refs directly so the seed works regardless of
        // when vanilla DropsLibrary.linkAssets runs relative to mod init.
        drop.cached_drop_type_high = AssetManager.top_tiles.get(drop.drop_type_high);
        drop.cached_drop_type_low = AssetManager.top_tiles.get(drop.drop_type_low);
        drop.sound_drop = SEED_SOUND;
    }
    private DropsAction CreateStatusDropAction(StatusEffectAsset status, Action<Entity> addition_action = null)
    {
        return (tile, drop_id) =>
        {
            foreach (Actor a in Finder.getUnitsFromChunk(tile, 1, 3f, false))
            {
                var statuses = a.GetExtend().GetStatuses();
                bool has_status = false;
                foreach (var status_entity in statuses)
                {
                    if (status_entity.GetComponent<StatusComponent>().Type == status)
                    {
                        has_status = true;
                        status_entity.GetComponent<AliveTimer>().value = status.ParticleSettings.interval;
                        addition_action?.Invoke(status_entity);
                        break;
                    }
                }
                if (!has_status)
                {
                    var status_entity = status.NewEntity();
                    addition_action?.Invoke(status_entity);
                    a.GetExtend().AddSharedStatus(status_entity);
                }
                a.startColorEffect();
            }
        };
    }

    private void SetupCommonBuildingPlaceDrop()
    {
        var props = typeof(Buildings).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        foreach (PropertyInfo prop in props)
            if (prop.PropertyType == typeof(BuildingAsset))
            {
                BuildingAsset item = prop.GetValue(null) as BuildingAsset;
                if (item == null) continue;
                if (prop.GetCustomAttribute<SetupButtonAttribute>() != null)
                {
                    var power_id = item.id;

                    Clone(power_id, DropsLibrary.TEMPLATE_SPAWN_BUILDING);
                    t.building_asset = item.id;
                }
            }
    }
}
