using System.Collections.Generic;
using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 将藏宝阁或用于应急领取的宗门大殿设为行为目标。
/// </summary>
public class BehFindSectTreasureBuilding(bool allowHallFallback) : BehaviourActionActor
{
    /// <summary>
    /// 查找可用的藏宝阁；领取物品时允许在藏宝阁被毁后改用宗门大殿。
    /// </summary>
    public override BehResult execute(Actor pActor)
    {
        Sect sect = pActor.GetExtend().sect;
        Building building = FindUsableBuilding(sect, Buildings.SectTreasurePavilion.id);
        if (building == null && allowHallFallback)
        {
            building = FindUsableBuilding(sect, Buildings.SectHall.id);
        }

        if (building == null)
        {
            SectVerifyLog.Log("SectTreasureTarget", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} result=false");
            return BehResult.Stop;
        }

        pActor.beh_building_target = building;
        pActor.beh_tile_target = null;
        SectVerifyLog.Log("SectTreasureTarget", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} building={building.asset.id}");
        return BehResult.Continue;
    }

    private static Building FindUsableBuilding(Sect sect, string buildingId)
    {
        List<Building> buildings = sect.GetBuildingListOfID(buildingId);
        if (buildings == null) return null;

        for (int i = 0; i < buildings.Count; i++)
        {
            Building building = buildings[i];
            if (building.isUsable() && !building.isUnderConstruction()) return building;
        }

        return null;
    }
}
