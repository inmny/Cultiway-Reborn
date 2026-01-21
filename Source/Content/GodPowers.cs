using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Attributes;
using Cultiway.Content.Extensions;
using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Cultiway.UI;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using strings;
using UnityEngine;

namespace Cultiway.Content;
[Dependency(typeof(Actors), typeof(Buildings), typeof(Drops))]
public class GodPowers : ExtendLibrary<GodPower, GodPowers>
{
    [CloneSource(PowerLibrary.TEMPLATE_SPAWN_ACTOR)]
    public static GodPower Plant { get; private set; }
    [CloneSource(PowerLibrary.TEMPLATE_SPAWN_ACTOR)]
    public static GodPower EasternHuman { get; private set; }
    [CloneSource(PowerLibrary.TEMPLATE_TERRAFORM_TILES)]
    public static GodPower ExtendGeoRegion { get; private set; }
    [CloneSource(PowerLibrary.TEMPLATE_TERRAFORM_TILES)]
    public static GodPower RemoveGeoRegion { get; private set; }
    protected override bool AutoRegisterAssets() => true;
    protected override void OnInit()
    {
        Plant.name = Actors.Plant.getLocaleID();
        Plant.actor_asset_id = Actors.Plant.id;
        EasternHuman.name = Actors.EasternHuman.getLocaleID();
        EasternHuman.actor_asset_id = Actors.EasternHuman.id;
        ExtendGeoRegion.name = "Extend Geo Region";
        RemoveGeoRegion.name = "Remove Geo Region";

        ExtendGeoRegion.click_action = ExtendGeoRegionAction;
        ExtendGeoRegion.click_brush_action = InitializeGeoRegionAction + ExtendGeoRegion.click_brush_action;
        RemoveGeoRegion.click_action = RemoveGeoRegionAction;
        SetupCommonCreaturePlacePower();
        SetupCommonBuildingPlacePower();
        SetupCommonDropPlacePower();
    }
    private static Entity _current_geo_region = default;
    private static bool InitializeGeoRegionAction(WorldTile tile, string power_id)
    {
        var te = tile.GetExtend();
        var rels = te.E.GetRelations<BelongToRelation>();
        foreach (var rel in rels)
        {
            if (rel.entity.HasComponent<GeoRegion>())
            {
                _current_geo_region = rel.entity;
                return true;
            }
        }
        if (rels.Length == 0)
        {
            // 创建一个空的geo region
            var region = te.E.Store.CreateEntity(new GeoRegion()
            {
                color = Randy.getRandomColor()
            });
            region.AddRelation(new BelongToRelation { entity = te.E });
            _current_geo_region = region;
        }
        
        return true;
    }
    private static bool ExtendGeoRegionAction(WorldTile tile, string power_id)
    {
        var te = tile.GetExtend();
        var rels = te.E.GetRelations<BelongToRelation>();
        if (rels.Length != 0)
        {
            foreach (var rel in rels)
            {
                if (rel.entity.HasComponent<GeoRegion>())
                {
                    te.E.RemoveRelation<BelongToRelation>(rel.entity);
                    break;
                }
            }
        }
        te.E.AddRelation(new BelongToRelation { entity = _current_geo_region });
        return true;
    }
    private static bool RemoveGeoRegionAction(WorldTile tile, string power_id)
    {
        var te = tile.GetExtend();
        var rels = te.E.GetRelations<BelongToRelation>();
        if (rels.Length != 0)
        {
            foreach (var rel in rels)
            {
                if (rel.entity.HasComponent<GeoRegion>())
                {
                    te.E.RemoveRelation<BelongToRelation>(rel.entity);
                    break;
                }
            }
        }
        return true;
    }
    private void SetupCommonBuildingPlacePower()
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

                    Clone(power_id, PowerLibrary.TEMPLATE_DROP_BUILDING);
                    t.name = item.id.Underscore();
                    t.drop_id = item.id;

                    var all_sprites = item.loadBuildingSpriteList();
                    var icon = all_sprites.First(x => x.name.Contains("main"));
                    Cultiway.UI.Manager.AddButton(TabButtonType.BUILDING, PowerButtonCreator.CreateGodPowerButton(
                        power_id, icon
                    ));
                }
            }
    }

    private void SetupCommonDropPlacePower()
    {
        var props = typeof(Drops).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        foreach (PropertyInfo prop in props)
            if (prop.PropertyType == typeof(DropAsset))
            {
                DropAsset item = prop.GetValue(null) as DropAsset;
                if (item == null) continue;
                if (prop.GetCustomAttribute<SetupButtonAttribute>() != null)
                {
                    var power_id = item.id;

                    Clone(power_id, PowerLibrary.TEMPLATE_DROPS);
                    t.name = item.id.Underscore();
                    t.drop_id = item.id;

                    var icon = SpriteTextureLoader.getSprite("ui/icons/iconRain");
                    Cultiway.UI.Manager.AddButton(TabButtonType.DROP, PowerButtonCreator.CreateGodPowerButton(
                        power_id, icon
                    ));
                }
            }
    }
    private void SetupCommonCreaturePlacePower()
    {
        var props = typeof(Actors).GetProperties(BindingFlags.Public | BindingFlags.Static | BindingFlags.NonPublic);
        foreach (PropertyInfo prop in props)
            if (prop.PropertyType == typeof(ActorAsset))
            {
                ActorAsset item = prop.GetValue(null) as ActorAsset;
                if (item == null) continue;
                if (prop.GetCustomAttribute<SetupButtonAttribute>() != null)
                {
                    var power_id = item.id;

                    Clone(power_id, PowerLibrary.TEMPLATE_SPAWN_ACTOR);
                    t.name = item.getLocaleID();
                    t.actor_asset_id = power_id;
                    
                    Cultiway.UI.Manager.AddButton(TabButtonType.CREATURE, PowerButtonCreator.CreateGodPowerButton(
                        power_id, item.getSpriteIcon()
                    ));
                }
            }
    }

    protected override void PostInit(GodPower asset)
    {
        base.PostInit(asset);
        if (!string.IsNullOrEmpty(asset.drop_id))
        {
            asset.cached_drop_asset = AssetManager.drops.get(asset.drop_id);
        }
        if (!string.IsNullOrEmpty(asset.tile_type))
        {
            asset.cached_tile_type_asset = AssetManager.tiles.get(asset.tile_type);
        }
        if (!string.IsNullOrEmpty(asset.top_tile_type))
        {
            asset.cached_top_tile_type_asset = AssetManager.top_tiles.get(asset.top_tile_type);
        }
        if (asset.actor_asset_id != null)
        {
            ActorAsset actor_asset = AssetManager.actor_library.get(asset.actor_asset_id);
            if (actor_asset.power_id == null)
            {
                actor_asset.power_id = asset.id;
            }
        }
        string[] actor_asset_ids = asset.actor_asset_ids;
        if (actor_asset_ids != null && actor_asset_ids.Length != 0)
        {
            foreach (string id in asset.actor_asset_ids)
            {
                ActorAsset actor_asset = AssetManager.actor_library.get(id);
                if (actor_asset.power_id == null)
                {
                    actor_asset.power_id = asset.id;
                }
            }
        }
    }
}
