using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 条件：需要师傅（没有师傅且需要）
/// </summary>
public class CondNeedMaster : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        // 没有师傅且境界较低（需要指导）
        if (ae.HasMaster()) return false;
        if (!ae.HasCultisys<Xian>()) return false;
        
        ref var xian = ref ae.GetCultisys<Xian>();
        // 境界较低时需要师傅（练气、筑基期）
        return xian.level <= 2;
    }
}

