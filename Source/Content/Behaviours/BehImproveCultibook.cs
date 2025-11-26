using ai.behaviours;
using Cultiway.Content.Extensions;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 改进功法行为
/// </summary>
public class BehImproveCultibook : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pActor)
    {
        var ae = pActor.GetExtend();
        
        var mainCultibook = ae.GetMainCultibook();
        if (mainCultibook == null)
        {
            return BehResult.Stop;
        }
        
        // 尝试改进功法
        bool success = ae.TryImproveCultibook();
        
        if (success)
        {
            ModClass.LogInfo($"[{ae}] 改进功法成功: {mainCultibook.Name}");
            return BehResult.Continue;
        }
        else
        {
            ModClass.LogInfo($"[{ae}] 改进功法失败: {mainCultibook.Name}");
            // 改进失败，停止任务（避免频繁失败）
            return BehResult.Stop;
        }
    }
}

