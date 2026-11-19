using System.IO;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Core.Skills;

/// <summary>来源为选中友方承担剩余伤害，实际承担量消耗本次额度。</summary>
public sealed class ConstructionSoulPact
{
    public float Fraction = .5f;
    public float Capacity = 80f;
}

/// <summary>当次施放使用的固定份额和初始额度。</summary>
internal sealed class CompiledSoulPact
{
    internal readonly float Fraction, Capacity;

    internal CompiledSoulPact(ConstructionSoulPact source, ActiveAbilityTargetMode target, SkillUseTargetRelation relation)
    {
        if (target != ActiveAbilityTargetMode.Object || relation != SkillUseTargetRelation.Friendly)
            throw new InvalidDataException("SoulPact：需要选取一名友方人物");
        if (!(source.Fraction > 0f && source.Fraction <= 1f))
            throw new InvalidDataException("SoulPact/Fraction：份额须大于零且不超过一");
        if (!(source.Capacity >= 1f && source.Capacity <= 1_000_000f))
            throw new InvalidDataException("SoulPact/Capacity：额度须在1到1000000之间");
        Fraction = source.Fraction;
        Capacity = source.Capacity;
    }
}
