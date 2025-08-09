using System.Linq;
using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Attributes;
using Cultiway.UI;
using NeoModLoader.General;
using strings;

namespace Cultiway.Content;
[Dependency(typeof(Actors), typeof(Buildings), typeof(Drops))]
public class GodPowers : ExtendLibrary<GodPower, GodPowers>
{
    [CloneSource(PowerLibrary.TEMPLATE_SPAWN_ACTOR)]
    public static GodPower Plant { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();
        Plant.name = Actors.Plant.getLocaleID();
        Plant.actor_asset_id = Actors.Plant.id;
        SetupCommonCreaturePlacePower();
        SetupCommonBuildingPlacePower();
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