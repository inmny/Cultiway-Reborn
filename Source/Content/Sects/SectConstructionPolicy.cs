using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Sects;

/// <summary>
/// 判断宗门成员和建筑是否仍满足当前建造流程的运行条件。
/// </summary>
public static class SectConstructionPolicy
{
    public static bool CanAssignBuilder(Actor actor, Sect sect)
    {
        if (actor == null || actor.isRekt() || sect == null || sect.isRekt()) return false;
        return actor.CanBuildSectBuilding(sect) && sect.HasBuildingToBuild();
    }

    [Hotfixable]
    public static bool CanWorkOnCurrentConstruction(Actor actor)
    {
        if (actor == null || actor.isRekt()) return false;

        Sect sect = actor.GetExtend().sect;
        if (sect == null || sect.isRekt()) return false;
        if (!actor.CanBuildSectBuilding(sect)) return false;
        if (actor.GetSectJob() != SectJobs.Builder || actor.GetSectJobSectId() != sect.getID()) return false;

        return sect.HasBuildingToBuild();
    }

    public static bool IsCurrentBuilding(Sect sect, Building building)
    {
        if (sect == null || sect.isRekt()) return false;
        if (building == null || building.isRekt() || !building.isAlive()) return false;
        if (building.asset == null || !building.asset.IsSectBuilding()) return false;
        if (!building.isUnderConstruction()) return false;

        building.data.get(BuildingDataKeys.SectID_Long, out long sectId, -1);
        return sectId == sect.getID() && sect.GetBuildingToBuild() == building;
    }
}
