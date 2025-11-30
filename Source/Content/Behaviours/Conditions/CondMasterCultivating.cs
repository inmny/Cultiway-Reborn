using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 条件：师傅正在修炼
/// </summary>
public class CondMasterCultivating : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        if (!ae.HasMaster()) return false;
        
        var master = ae.GetMaster();
        if (master.isRekt()) return false;
        
        // 简化处理：检查师傅是否有修仙状态
        return master.GetExtend().HasCultisys<Xian>();
    }
}

