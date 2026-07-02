using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Extensions;

public static class SectBuildRules
{
    public static bool CanUseBuildOrder(Sect sect, SectBuildOrder order)
    {
        if (sect == null || sect.isRekt() || order == null) return false;
        if (sect.HasBuildingToBuild()) return false;

        BuildingAsset building = order.GetBuildingAsset();
        if (building == null || !building.IsSectBuilding()) return false;

        if (order.maxPerSect > 0 && sect.CountBuildingsOfID(building.id, false) >= order.maxPerSect) return false;
        if (order.requiredMembers > 0 && sect.GetLivingMembers().Count < order.requiredMembers) return false;
        if (order.requiredResidenceZones > 0 && sect.GetResidenceZones().Count < order.requiredResidenceZones) return false;
        if (!HasRequiredBuildings(sect, order.requirementsBuildings)) return false;
        if (!HasRequiredBuildingTypes(sect, order.requirementsTypes)) return false;

        return FindBuildTile(sect, building) != null;
    }

    public static List<SectBuildOrder> GetAvailableBuildOrders(Sect sect, SectBuildOrderAsset orderAsset)
    {
        var result = new List<SectBuildOrder>();
        if (sect == null || sect.isRekt() || orderAsset == null) return result;

        for (int i = 0; i < orderAsset.list.Count; i++)
        {
            SectBuildOrder order = orderAsset.list[i];
            if (CanUseBuildOrder(sect, order))
            {
                result.Add(order);
            }
        }

        return result;
    }

    public static bool TryBuildFromOrder(Sect sect, SectBuildOrder order, out Building building)
    {
        building = null;
        if (!CanUseBuildOrder(sect, order)) return false;

        BuildingAsset asset = order.GetBuildingAsset();
        WorldTile tile = FindBuildTile(sect, asset);
        if (tile == null) return false;

        building = World.world.buildings.addBuilding(asset, tile, true);
        if (building == null || building.isRekt()) return false;

        building.setUnderConstruction();
        building.data.set(BuildingDataKeys.SectID_Long, sect.getID());
        sect.under_construction_building = building;
        WorldboxGame.I?.Sects?.setDirtyBuildings();
        SectVerifyLog.Log("SectBuild", $"sect={SectVerifyLog.Sect(sect)} building={building.asset?.id ?? "null"} tile={tile.x},{tile.y}");
        return true;
    }

    public static WorldTile FindBuildTile(Sect sect, SectBuildOrder order)
    {
        return order == null ? null : FindBuildTile(sect, order.GetBuildingAsset());
    }

    public static WorldTile FindBuildTile(Sect sect, BuildingAsset building)
    {
        if (sect == null || sect.isRekt() || building == null) return null;

        return SectResidencePlanner.FindBuildTile(sect, building);
    }

    private static bool HasRequiredBuildings(Sect sect, string[] buildingIds)
    {
        if (buildingIds == null) return true;

        for (int i = 0; i < buildingIds.Length; i++)
        {
            string buildingId = buildingIds[i];
            if (!string.IsNullOrEmpty(buildingId) && !HasBuildingOrUpgrade(sect, buildingId))
            {
                return false;
            }
        }

        return true;
    }

    private static bool HasRequiredBuildingTypes(Sect sect, string[] buildingTypes)
    {
        if (buildingTypes == null) return true;

        for (int i = 0; i < buildingTypes.Length; i++)
        {
            string buildingType = buildingTypes[i];
            if (!string.IsNullOrEmpty(buildingType) && !sect.HasBuildingType(buildingType))
            {
                return false;
            }
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
