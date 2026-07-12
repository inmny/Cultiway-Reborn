using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3;

/// <summary>
/// 判断施法者是否满足某项技能释放前置条件。
/// </summary>
public delegate bool SkillCastRequirement(ActorExtend caster, Entity skill, SkillCastFundingSource fundingSource);

/// <summary>
/// 汇总各内容体系注册的技能释放前置条件，Core 的所有施法入口统一通过这里校验。
/// </summary>
public static class SkillCastRequirements
{
    private static readonly List<SkillCastRequirement> Requirements = new();

    /// <summary>
    /// 注册一项施法前置条件；同一委托不会重复注册。
    /// </summary>
    public static void Register(SkillCastRequirement requirement)
    {
        if (requirement == null) throw new ArgumentNullException(nameof(requirement));
        if (!Requirements.Contains(requirement)) Requirements.Add(requirement);
    }

    /// <summary>
    /// 检查施法者是否满足当前所有已注册的前置条件。
    /// </summary>
    public static bool Check(ActorExtend caster, Entity skill, SkillCastFundingSource fundingSource)
    {
        if (caster == null || skill.IsNull) return false;
        foreach (var requirement in Requirements)
        {
            if (!requirement(caster, skill, fundingSource)) return false;
        }
        return true;
    }
}
