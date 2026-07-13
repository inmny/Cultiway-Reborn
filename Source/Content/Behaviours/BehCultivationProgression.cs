using ai.behaviours;
using Cultiway.Core.Progression;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

/// <summary>
///     统一进阶行为。具体体系只声明过渡规则，行为本身不理解境界条件或奖励内容。
/// </summary>
public sealed class BehCultivationProgression : BehaviourActionActor
{
    /// <summary>执行角色注册顺序中的第一项可调度进阶，并在大境界成功时增加升级幸福度。</summary>
    public override BehResult execute(Actor pObject)
    {
        var result = ProgressionService.TryAdvanceFirst(pObject.GetExtend());
        if (result.Code == ProgressionResultCode.MajorAdvanced)
        {
            pObject.changeHappiness(HappinessAssets.LevelUp.id);
        }
        return BehResult.Continue;
    }
}
