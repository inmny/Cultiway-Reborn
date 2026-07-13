using System.Collections.Generic;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Core.Libraries;

namespace Cultiway.Content.Sects;

/// <summary>
/// 校验宗门建造订单并为可执行订单选择建筑地块。
/// </summary>
public static class SectConstructionPlanner
{
    public static List<SectConstructionPlan> GetAvailablePlans(Sect sect, SectBuildOrderAsset orderAsset)
    {
        var result = new List<SectConstructionPlan>();
        if (sect == null || sect.isRekt() || orderAsset == null || sect.HasBuildingToBuild()) return result;

        for (int i = 0; i < orderAsset.list.Count; i++)
        {
            if (TryCreatePlan(sect, orderAsset.list[i], out SectConstructionPlan plan))
                result.Add(plan);
        }

        return result;
    }

    public static bool TryCreatePlan(Sect sect, SectBuildOrder order, out SectConstructionPlan plan)
    {
        plan = default;
        if (sect == null || sect.isRekt() || order == null || sect.HasBuildingToBuild()) return false;

        BuildingAsset building = order.GetBuildingAsset();
        if (building == null || !building.IsSectBuilding()) return false;
        if (order.maxPerSect > 0 && sect.CountBuildingsOfID(building.id, false) >= order.maxPerSect) return false;
        if (order.requiredMembers > 0 && sect.GetLivingMembers().Count < order.requiredMembers) return false;
        if (order.requiredResidenceZones > 0 && sect.GetResidenceZones().Count < order.requiredResidenceZones)
            return false;
        if (!HasRequiredBuildings(sect, order.requirementsBuildings)) return false;
        if (!HasRequiredBuildingTypes(sect, order.requirementsTypes)) return false;

        WorldTile site = SectResidencePlanner.FindBuildTile(sect, building);
        if (site == null) return false;

        plan = new SectConstructionPlan(order, building, site);
        return true;
    }

    private static bool HasRequiredBuildings(Sect sect, string[] buildingIds)
    {
        if (buildingIds == null) return true;

        for (int i = 0; i < buildingIds.Length; i++)
        {
            string buildingId = buildingIds[i];
            if (!string.IsNullOrEmpty(buildingId) && !HasBuildingOrUpgrade(sect, buildingId)) return false;
        }

        return true;
    }

    private static bool HasRequiredBuildingTypes(Sect sect, string[] buildingTypes)
    {
        if (buildingTypes == null) return true;

        for (int i = 0; i < buildingTypes.Length; i++)
        {
            string buildingType = buildingTypes[i];
            if (!string.IsNullOrEmpty(buildingType) && !sect.HasBuildingType(buildingType)) return false;
        }

        return true;
    }

    private static bool HasBuildingOrUpgrade(Sect sect, string buildingId)
    {
        BuildingAsset building = AssetManager.buildings.get(buildingId);
        if (building == null) return sect.CountBuildingsOfID(buildingId) > 0;

        while (building != null)
        {
            if (sect.CountBuildingsOfID(building.id) > 0) return true;
            if (!building.can_be_upgraded || string.IsNullOrEmpty(building.upgrade_to)) return false;
            if (building.id == building.upgrade_to) return false;

            building = AssetManager.buildings.get(building.upgrade_to);
        }

        return false;
    }
}
