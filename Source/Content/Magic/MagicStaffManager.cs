using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content;

/// <summary>
/// 将魔法体系的法杖要求注册到施法前置条件链。
/// </summary>
[Dependency(typeof(Cultisyses), typeof(SkillCastResources))]
public sealed class MagicStaffManager : ICanInit
{
    public void Init()
    {
        SkillCastRequirements.Register(CheckStaffRequirement);
    }

    private static bool CheckStaffRequirement(ActorExtend caster, Entity skill,
        SkillCastFundingSource fundingSource)
    {
        // 卷轴、符箓等预付费载体自行承担施法媒介，不要求使用者手持法杖。
        if (fundingSource == SkillCastFundingSource.Prepaid) return true;
        if (!caster.HasCultisys<Magic>() ||
            !SkillCastResourceResolver.UsesResource(skill, SkillCastResources.Mana)) return true;
        return MagicStaffTools.HasEquippedStaff(caster.Base);
    }
}
