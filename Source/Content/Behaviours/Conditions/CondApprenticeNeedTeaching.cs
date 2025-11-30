using System.Linq;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 条件：有需要传授的弟子
/// </summary>
public class CondApprenticeNeedTeaching : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        var apprentices = ae.GetApprentices();
        
        if (apprentices.Count == 0) return false;
        
        // 检查是否有需要传授的弟子
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null) return false;
        
        // 检查是否有弟子需要传授（掌握度较低）
        return apprentices.Any(apprentice =>
        {
            var apprenticeMastery = apprentice.GetMaster(mainCultibook);
            var masterMastery = ae.GetMainCultibookMastery();
            return apprenticeMastery <= 0 || apprenticeMastery < masterMastery * 0.8f;
        });
    }
}

