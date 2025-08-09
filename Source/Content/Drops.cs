using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Attributes;
using Cultiway.UI;
using NeoModLoader.General;

namespace Cultiway.Content;
[Dependency(typeof(Buildings))]
public class Drops : ExtendLibrary<DropAsset, Drops>
{
    protected override void OnInit()
    {
        SetupCommonBuildingPlaceDrop();
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