using System;
using System.Collections.Generic;

namespace Cultiway.Core.Libraries;

[Serializable]
public class SectBuildOrderAsset : Asset
{
    public List<SectBuildOrder> list = new();

    public SectBuildOrder AddBuilding(
        BuildingAsset building,
        int maxPerSect = 0,
        int requiredMembers = 0,
        int requiredResidenceZones = 0)
    {
        return AddBuilding(building?.id, maxPerSect, requiredMembers, requiredResidenceZones);
    }

    public SectBuildOrder AddBuilding(
        string buildingId,
        int maxPerSect = 0,
        int requiredMembers = 0,
        int requiredResidenceZones = 0)
    {
        var order = new SectBuildOrder
        {
            id = buildingId,
            buildingId = buildingId,
            maxPerSect = maxPerSect,
            requiredMembers = requiredMembers,
            requiredResidenceZones = requiredResidenceZones
        };
        list.Add(order);
        return order;
    }
}

[Serializable]
public class SectBuildOrder : Asset
{
    public string buildingId;
    public int maxPerSect;
    public int requiredMembers;
    public int requiredResidenceZones;
    public string[] requirementsBuildings;
    public string[] requirementsTypes;
    public bool upgrade;

    public BuildingAsset GetBuildingAsset()
    {
        return string.IsNullOrEmpty(buildingId)
            ? null
            : AssetManager.buildings.getSimple(buildingId);
    }
}
