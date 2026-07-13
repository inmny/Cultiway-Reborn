using ai.behaviours;
using Cultiway.Content.Sects;
using Cultiway.Debug;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 从所属宗门领取消耗品或借用法宝。
/// </summary>
public class BehClaimSectTreasure : BehaviourActionActor
{
    /// <summary>
    /// 挑选一件当前需要且负担得起的物品并完成领取。
    /// </summary>
    public override BehResult execute(Actor pActor)
    {
        if (!SectTreasurePlanner.TryPickClaim(pActor, out Entity item)) return BehResult.Stop;

        bool result = SectTreasureService.TryClaim(pActor, item);
        SectVerifyLog.Log("SectTreasureTask", $"type=claim actor={SectVerifyLog.Actor(pActor)} item={item.Id} result={result}");
        return result ? BehResult.Continue : BehResult.Stop;
    }
}
