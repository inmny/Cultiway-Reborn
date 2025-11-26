using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 检查是否可以改进功法（需要完全掌握主修功法）
/// </summary>
public class CondCanImproveCultibook : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        
        // 必须有主修功法
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null) return false;
        
        // 必须完全掌握（100%）
        float mastery = ae.GetMainCultibookMastery();
        if (mastery < 100f) return false;
        
        // 仙级最高，无法继续改进
        if (mainCultibook.Level.Stage >= 3 && mainCultibook.Level.Level >= 8) return false;
        
        return true;
    }
}

