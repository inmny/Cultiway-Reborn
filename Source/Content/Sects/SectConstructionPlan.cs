using Cultiway.Core.Libraries;

namespace Cultiway.Content.Sects;

/// <summary>
/// 一次已经完成订单校验和选址的宗门建造计划。
/// </summary>
public readonly struct SectConstructionPlan
{
    public readonly SectBuildOrder Order;
    public readonly BuildingAsset BuildingAsset;
    public readonly WorldTile Site;

    public SectConstructionPlan(SectBuildOrder order, BuildingAsset buildingAsset, WorldTile site)
    {
        Order = order;
        BuildingAsset = buildingAsset;
        Site = site;
    }
}
