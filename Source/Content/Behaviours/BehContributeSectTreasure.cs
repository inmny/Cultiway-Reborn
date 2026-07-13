using ai.behaviours;
using Cultiway.Content.Sects;
using Cultiway.Debug;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 将成员预先挑选出的特殊物品贡献给所属宗门。
/// </summary>
public class BehContributeSectTreasure : BehaviourActionActor
{
    /// <summary>
    /// 挑选一件可贡献物品并完成入库和贡献结算。
    /// </summary>
    public override BehResult execute(Actor pActor)
    {
        if (!SectTreasurePlanner.TryPickContribution(pActor, out Entity item)) return BehResult.Stop;

        bool result = SectTreasureService.TryContribute(pActor, item);
        SectVerifyLog.Log("SectTreasureTask", $"type=contribute actor={SectVerifyLog.Actor(pActor)} item={item.Id} result={result}");
        return result ? BehResult.Continue : BehResult.Stop;
    }
}
