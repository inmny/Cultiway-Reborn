using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Sects;

/// <summary>
/// 创建宗门工地并维护开工后的宗门状态。
/// </summary>
public static class SectConstructionService
{
    [Hotfixable]
    public static bool TryOpen(Sect sect)
    {
        List<SectConstructionPlan> plans =
            SectConstructionPlanner.GetAvailablePlans(sect, SectBuildOrders.Classic);
        if (plans.Count == 0) return false;

        bool result = TryStart(sect, plans.GetRandom(), out Building building);
        if (result)
        {
            sect.RefreshSectJobs();
            SectVerifyLog.Log("SectConstructionOpen", $"sect={SectVerifyLog.Sect(sect)} building={building?.asset?.id ?? "null"} jobs={sect.jobs.CountCurrentJobs(SectJobs.Builder)}");
        }

        return result;
    }

    public static bool TryStart(Sect sect, SectBuildOrder order, out Building building)
    {
        building = null;
        return SectConstructionPlanner.TryCreatePlan(sect, order, out SectConstructionPlan plan) &&
               TryStart(sect, plan, out building);
    }

    private static bool TryStart(Sect sect, SectConstructionPlan plan, out Building building)
    {
        building = World.world.buildings.addBuilding(plan.BuildingAsset, plan.Site, true);
        if (building == null || building.isRekt()) return false;

        building.setUnderConstruction();
        building.data.set(BuildingDataKeys.SectID_Long, sect.getID());
        sect.under_construction_building = building;
        WorldboxGame.I?.Sects?.setDirtyBuildings(sect);
        SectVerifyLog.Log("SectBuild", $"sect={SectVerifyLog.Sect(sect)} building={building.asset?.id ?? "null"} tile={plan.Site.x},{plan.Site.y}");
        return true;
    }
}
