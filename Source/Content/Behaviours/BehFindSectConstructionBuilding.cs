using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 将成员当前宗门的在建建筑设为行为目标。
/// </summary>
public class BehFindSectConstructionBuilding : BehaviourActionActor
{
    /// <summary>
    /// 查找当前宗门正在修建的建筑。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (!SectConstructionRules.CanBuildSectBuilding(pActor))
        {
            SectVerifyLog.Log("SectConstructionTarget", $"actor={SectVerifyLog.Actor(pActor)} result=false reason=no_job");
            return BehResult.Stop;
        }

        Sect sect = pActor.GetExtend().sect;
        Building building = sect.GetBuildingToBuild();
        if (!SectConstructionRules.IsCurrentConstruction(sect, building))
        {
            SectVerifyLog.Log("SectConstructionTarget", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} result=false reason=no_building");
            return BehResult.Stop;
        }

        pActor.beh_building_target = building;
        pActor.beh_tile_target = null;
        SectVerifyLog.Log("SectConstructionTarget", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} building={building.asset?.id ?? "null"}");
        return BehResult.Continue;
    }
}
