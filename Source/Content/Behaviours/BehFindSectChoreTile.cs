using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Core;
using Cultiway.Debug;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 为宗门杂务选择一个可到达的宗门驻地地块。
/// </summary>
public class BehFindSectChoreTile : BehaviourActionActor
{
    /// <summary>
    /// 优先在宗门驻地影响区随机挑选同岛地块，找不到时回退到单位当前地块。
    /// </summary>
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        if (!SectChoreRules.CanDoSectChore(pActor))
        {
            SectVerifyLog.Log("SectChoreTarget", $"actor={SectVerifyLog.Actor(pActor)} result=false reason=no_permission");
            return BehResult.Stop;
        }

        Sect sect = pActor.GetExtend().sect;
        WorldTile target = sect.GetRandomResidenceTile(pActor) ?? pActor.current_tile;
        pActor.beh_tile_target = target;
        SectVerifyLog.Log("SectChoreTarget", $"sect={SectVerifyLog.Sect(sect)} actor={SectVerifyLog.Actor(pActor)} residence={sect.data.ResidenceName ?? "null"} tile={target?.pos.x ?? -1},{target?.pos.y ?? -1}");
        return BehResult.Continue;
    }
}
