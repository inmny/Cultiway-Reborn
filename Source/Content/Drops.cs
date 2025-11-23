using System.Reflection;
using Cultiway.Abstract;
using Cultiway.Content.Attributes;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.UI;
using Cultiway.Utils.Extension;
using NeoModLoader.General;
using strings;

namespace Cultiway.Content;
[Dependency(typeof(Buildings))]
public class Drops : ExtendLibrary<DropAsset, Drops>
{
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Enlighten { get; private set; }
    [SetupButton, CloneSource(S_Drop.dust_white)]
    public static DropAsset Slow { get; private set; }
    protected override void OnInit()
    {
        RegisterAssets();

        Enlighten.action_landed = CreateStatusDropAction(StatusEffects.Enlighten);
        Slow.action_landed = CreateStatusDropAction(StatusEffects.Slow);



        SetupCommonBuildingPlaceDrop();
    }
    private DropsAction CreateStatusDropAction(StatusEffectAsset status)
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
                        break;
                    }
                }
                if (!has_status)
                {
                    a.GetExtend().AddSharedStatus(status.NewEntity());
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
