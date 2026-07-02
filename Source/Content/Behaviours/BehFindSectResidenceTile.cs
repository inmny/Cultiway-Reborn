using ai.behaviours;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 为宗门事务选择一个可到达的宗门驻地地块。
/// </summary>
public class BehFindSectResidenceTile : BehaviourActionActor
{
    private readonly string logTag;

    public BehFindSectResidenceTile(string logTag = "SectResidenceTarget")
    {
        this.logTag = logTag;
    }

    /// <summary>
    /// 在宗门驻地影响区内挑选与单位同岛的地块，并设置为行为目标。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (pActor == null || pActor.isRekt())
        {
            return BehResult.Stop;
        }

        Sect sect = pActor.GetExtend().sect;
        if (sect == null || sect.isRekt())
        {
            SectVerifyLog.Log(logTag, $"actor={SectVerifyLog.Actor(pActor)} result=false reason=no_sect");
            return BehResult.Stop;
        }

        WorldTile target = sect.GetRandomResidenceTile(pActor);
        if (target == null)
        {
            SectVerifyLog.Log(logTag, $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} residence={sect.GetResidenceName() ?? "null"} result=false reason=no_reachable_residence");
            return BehResult.Stop;
        }

        pActor.beh_tile_target = target;
        pActor.beh_building_target = null;
        SectVerifyLog.Log(logTag, $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} residence={sect.GetResidenceName() ?? "null"} tile={target.pos.x},{target.pos.y}");
        return BehResult.Continue;
    }
}
