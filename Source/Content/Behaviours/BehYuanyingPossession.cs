using ai.behaviours;

namespace Cultiway.Content.Behaviours;

/// <summary>持续驱动离体元婴的寻主、引导和唯一一次夺舍结算。</summary>
public sealed class BehYuanyingPossession : BehaviourActionActor
{
    public override BehResult execute(Actor actor)
    {
        return YuanyingPossessionService.TickSoul(actor)
            ? BehResult.RepeatStep
            : BehResult.Continue;
    }
}
