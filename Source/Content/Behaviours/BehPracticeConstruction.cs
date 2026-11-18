using ai.behaviours;
using Cultiway.Content.Skills;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours;

/// <summary>训练任务热身后实际练一次已学战技，收势前不进入下一轮操练。</summary>
public sealed class BehPracticeConstruction : BehCityActor
{
    /// <summary>按共同动作的时长等待，不在任务内重复播一份动作。</summary>
    /// <param name="pActor">已经走到训练假人的实际人物。</param>
    public override BehResult execute(Actor pActor)
    {
        if (ConstructedAbilityService.TryPracticeConstruction(pActor.GetExtend(), out float seconds))
            pActor.timer_action = seconds;
        return BehResult.Continue;
    }
}
