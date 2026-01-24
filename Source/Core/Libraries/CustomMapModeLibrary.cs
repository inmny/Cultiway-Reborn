using Cultiway.Core.Components;
using Cultiway.Core.GeoLib.Components;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Core.Libraries;

public class CustomMapModeLibrary : AssetLibrary<CustomMapModeAsset>
{
    public static CustomMapModeAsset Sect { get; private set; }
    public static CustomMapModeAsset GeoRegion { get; private set; }
    public override void init()
    {
        Sect = add(new CustomMapModeAsset()
        {
            id = "sect",
            icon_path = "cultiway/icons/iconGeoRegion",
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                out_color.a = 0;
            }
        });
        GeoRegion = add(new CustomMapModeAsset()
        {
            id = "geo_region",
            icon_path = "cultiway/icons/iconGeoRegion",
            kernel_func = (int x, int y, ref Color32 out_color) =>
            {
                var tile = World.world.GetTile(x, y).GetExtend();
                var rels = tile.E.GetRelations<BelongToRelation>();
                foreach (var rel in rels)
                {
                    if (rel.entity.HasComponent<GeoRegionBinder>())
                    {
                        out_color = rel.entity.GetComponent<GeoRegionBinder>().GeoRegion.getColor().getColorMain32();
                        return;
                    }
                }
                out_color.a = 0;
            }
        });
    }

    public override CustomMapModeAsset add(CustomMapModeAsset pAsset)
    {
        GodPower power = new GodPower()
        {
            id = pAsset.toggle_name,
            name = pAsset.toggle_name,
            unselect_when_window = true,
            map_modes_switch = true,
            toggle_name = pAsset.toggle_name,
            toggle_action = _ => ModClass.I.CustomMapModeManager.SetAllDirty()
        };
        AssetManager.powers.add(power);
        UI.Manager.AddButton(TabButtonType.WORLD,
            PowerButtonCreator.CreateToggleButton(pAsset.toggle_name, SpriteTextureLoader.getSprite(pAsset.icon_path)));
        return base.add(pAsset);
    }
}