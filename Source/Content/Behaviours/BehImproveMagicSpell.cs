using ai.behaviours;
using Cultiway.Utils.Extension;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Behaviours;

/// <summary>
/// 在主动研究任务结算时改进一个已达到使用阈值的法术，并将结果上传魔网。
/// </summary>
public sealed class BehImproveMagicSpell : BehaviourActionActor
{
    [Hotfixable]
    public override BehResult execute(Actor pObject)
    {
        return MagicSpellProgressionService.TryImproveAndPublish(pObject.GetExtend())
            ? BehResult.Continue
            : BehResult.Stop;
    }
}
