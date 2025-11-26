using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Utils;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 检查是否应该转修功法
/// </summary>
public class CondCanSwitchCultibook : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        var ae = pActor.GetExtend();
        
        // 必须有主修功法才考虑转修
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null) return false;
        
        // 必须有可用的转修功法
        var bestCultibook = ae.SelectBestCultibookToSwitch();
        if (bestCultibook == null) return false;
        
        // 检查是否有足够的灵力（转修失败会损失灵力）
        if (ae.HasCultisys<Xian>())
        {
            ref var xian = ref ae.GetCultisys<Xian>();
            // 至少需要50%的灵力才考虑转修
            float maxWakan = ae.Base.stats[BaseStatses.MaxWakan.id];
            if (xian.wakan < maxWakan * 0.5f) return false;
        }
        
        // 检查当前主修功法的掌握程度（掌握度太高时转修代价大）
        float currentMastery = ae.GetMainCultibookMastery();
        // 如果掌握度超过80%，转修意愿降低
        if (currentMastery > 80f && !Randy.randomChance(0.3f)) return false;
        
        return true;
    }
}

