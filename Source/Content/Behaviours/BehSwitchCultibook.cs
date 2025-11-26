using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 转修功法行为
/// </summary>
public class BehSwitchCultibook : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        var ae = pActor.GetExtend();
        
        // 选择最佳转修功法
        var bestCultibook = ae.SelectBestCultibookToSwitch();
        if (bestCultibook == null)
        {
            return BehResult.Stop;
        }
        
        // 尝试转修
        bool success = ae.TrySwitchMainCultibook(bestCultibook);
        
        if (success)
        {
            ModClass.LogInfo($"[{ae}] 转修功法成功: {bestCultibook.Name}");
            return BehResult.Continue;
        }
        else
        {
            ModClass.LogInfo($"[{ae}] 转修功法失败: {bestCultibook.Name}");
            // 转修失败，停止任务（避免频繁失败）
            return BehResult.Stop;
        }
    }
}

