using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core;
using Cultiway.Core.Libraries;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Extensions;

/// <summary>
/// 宗门建筑自动开工和成员修建规则。
/// </summary>
public static class SectConstructionRules
{
    /// <summary>
    /// 尝试给宗门开启一个新的建筑工地，并刷新对应岗位。
    /// </summary>
    [Hotfixable]
    public static bool TryOpenConstruction(Sect sect)
    {
        if (sect == null || sect.isRekt()) return false;
        if (sect.HasBuildingToBuild()) return false;

        List<SectBuildOrder> orders = SectBuildRules.GetAvailableBuildOrders(sect, SectBuildOrders.Classic);
        if (orders.Count == 0) return false;

        SectBuildOrder order = orders.GetRandom();
        bool result = sect.TryBuild(order, out Building building);
        if (result)
        {
            sect.RefreshSectJobs();
            SectVerifyLog.Log("SectConstructionOpen", $"sect={SectVerifyLog.Sect(sect)} building={building?.asset?.id ?? "null"} jobs={sect.jobs.CountCurrentJobs(SectJobs.Builder)}");
        }

        return result;
    }

    /// <summary>
    /// 判断成员是否可以领取宗门建造岗位。
    /// </summary>
    public static bool CanUseBuilderJob(Actor actor, Sect sect)
    {
        if (actor == null || actor.isRekt() || sect == null || sect.isRekt()) return false;
        if (!actor.CanBuildSectBuilding(sect)) return false;
        return sect.HasBuildingToBuild();
    }

    /// <summary>
    /// 判断成员当前能否继续执行宗门建造任务。
    /// </summary>
    [Hotfixable]
    public static bool CanBuildSectBuilding(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;
        return CanBuildSectBuilding(actor, actor.GetExtend().sect);
    }

    /// <summary>
    /// 判断成员当前能否继续执行指定宗门的建造任务。
    /// </summary>
    [Hotfixable]
    public static bool CanBuildSectBuilding(Actor actor, Sect sect)
    {
        if (actor == null || actor.isRekt() || sect == null || sect.isRekt()) return false;
        if (!actor.CanBuildSectBuilding(sect)) return false;
        if (actor.GetSectJob() != SectJobs.Builder || actor.GetSectJobSectId() != sect.getID()) return false;

        return sect.HasBuildingToBuild();
    }

    /// <summary>
    /// 判断建筑是否为指定宗门当前的在建建筑。
    /// </summary>
    public static bool IsCurrentConstruction(Sect sect, Building building)
    {
        if (sect == null || sect.isRekt()) return false;
        if (building == null || building.isRekt() || !building.isAlive()) return false;
        if (building.asset == null || !building.asset.IsSectBuilding()) return false;
        if (!building.isUnderConstruction()) return false;

        building.data.get(BuildingDataKeys.SectID_Long, out long sectId, -1);
        return sectId == sect.getID() && sect.GetBuildingToBuild() == building;
    }
}
