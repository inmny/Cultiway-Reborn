using Cultiway.Utils.Extension;

namespace Cultiway.Content.Behaviours.Conditions;

/// <summary>
/// 检查魔法师是否有法术已经积累了足够的实际使用经验，可以开始主动改进。
/// </summary>
public sealed class CondCanImproveMagicSpell : BehaviourActorCondition
{
    public override bool check(Actor pActor)
    {
        return MagicSpellProgressionService.ShouldImprove(pActor.GetExtend());
    }
}
