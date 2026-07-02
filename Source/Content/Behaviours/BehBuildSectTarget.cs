using ai.behaviours;
using Cultiway.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 推进宗门在建建筑的施工进度。
/// </summary>
public class BehBuildSectTarget : BehaviourActionActor
{
    public override void setupErrorChecks()
    {
        base.setupErrorChecks();
        null_check_building_target = true;
        null_check_tile_target = true;
    }

    /// <summary>
    /// 按成员建造速度推进工地，完工时发放宗门贡献并刷新建筑列表。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        Sect sect = pActor.GetExtend().sect;
        Building building = pActor.beh_building_target;
        if (!SectConstructionRules.IsCurrentConstruction(sect, building))
        {
            return BehResult.Stop;
        }

        bool completed = building.updateBuild(pActor.getConstructionSpeed());
        if (completed)
        {
            WorldboxGame.I?.Sects?.setDirtyBuildings();
            sect.AddContribution(pActor, SectConst.ContributionBuildSectBuilding);
            SectVerifyLog.Log("SectConstructionComplete", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} building={building.asset?.id ?? "null"} contribution={SectConst.ContributionBuildSectBuilding}");
        }

        return BehResult.Continue;
    }
}
