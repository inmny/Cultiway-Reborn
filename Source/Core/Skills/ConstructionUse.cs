using System.IO;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Usage;

namespace Cultiway.Core.Skills;

/// <summary>选择施放对象与使用场景，接触后作用的人物关系仍由实际作用规则决定。</summary>
public sealed class ConstructionUse
{
    /// <summary>允许由战斗或世界用途选择；两者可以同时存在。</summary>
    public ActiveAbilityChannel Channels = ActiveAbilityChannel.Combat;

    /// <summary>选择本人、人物、地点或两者之一，不用人物充当环境用途的定位物。</summary>
    public ActiveAbilityTargetMode Target = ActiveAbilityTargetMode.Object;
}

/// <summary>已经验证并复制的选择规则，运行与说明共用，不随外部资料修改。</summary>
public sealed class CompiledConstructionUse
{
    /// <summary>实际允许的使用场景。</summary>
    public ActiveAbilityChannel Channels { get; }

    /// <summary>本次选取人物或地点的方式。</summary>
    public ActiveAbilityTargetMode Target { get; }

    /// <summary>旧资料保持原有战斗及本人或人物选取，新资料只在入口验证一次。</summary>
    /// <param name="source">可选的明确使用规则。</param>
    /// <param name="relation">旧资料原有的接触人物关系。</param>
    internal CompiledConstructionUse(ConstructionUse source, SkillUseTargetRelation relation)
    {
        Channels = source?.Channels ?? ActiveAbilityChannel.Combat;
        Target = source?.Target ?? (relation == SkillUseTargetRelation.Self
            ? ActiveAbilityTargetMode.Self
            : ActiveAbilityTargetMode.Object);
        if (Channels == ActiveAbilityChannel.None ||
            (Channels & ~(ActiveAbilityChannel.Combat | ActiveAbilityChannel.World)) != 0 ||
            Target is not (ActiveAbilityTargetMode.Self or ActiveAbilityTargetMode.Object or
                ActiveAbilityTargetMode.Point or ActiveAbilityTargetMode.ObjectOrPoint))
            throw new InvalidDataException("构造使用规则需要明确场景及本人、人物或地点选取方式");
    }
}
