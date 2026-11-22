using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.SkillLibV3.Visuals;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Skills;

/// <summary>原发射当时确定的范围和地块规则，仅由已经受理的同一次施放持有。</summary>
internal sealed class LegacyAreaImpact
{
    internal readonly string Path;
    internal readonly float Radius;
    internal readonly bool DealsBaseDamage;
    internal readonly SkillVfxElementAsset Element;
    /// <summary>功能术的附加强度，存在时用原范围选人及建筑，作用面本身不带攻击量。</summary>
    internal readonly float? UtilityForce;
    /// <summary>同次位移词条使用原容器实际排列顺序。</summary>
    internal readonly bool SwapFirst;

    private LegacyAreaImpact(string path, float radius, bool dealsBaseDamage, SkillVfxElementAsset element,
        float? utilityForce = null, bool swapFirst = false)
    {
        Path = path;
        Radius = radius;
        DealsBaseDamage = dealsBaseDamage;
        Element = element;
        UtilityForce = utilityForce;
        SwapFirst = swapFirst;
    }

    /// <summary>按同一次原落地的对象范围和逐发强度结算附加词条。</summary>
    /// <param name="radius">原单位中心范围。</param><param name="force">已计入原强度及倍率的附加作用量。</param>
    /// <param name="swapFirst">同时存在两种位移时，原容器先换位再击退。</param>
    internal static LegacyAreaImpact ForUtility(float radius, float force, bool swapFirst) => new("root", radius, false, null, force, swapFirst);

    /// <summary>保留原范围与实际元素选择。构造的尺寸、强度和画面各自继续使用已有规则。</summary>
    internal static LegacyAreaImpact From(in SkillEmission emission, string path)
    {
        Entity skill = emission.SkillContainer;
        SkillEntityAsset source = skill.GetComponent<SkillContainer>().Asset;
        float radius = SkillEffectRadius.ResolveForCaster(emission.Source.a.GetExtend(), skill,
            source.ImpactProfile.EffectRadius * source.ImpactTuning.EffectRadiusMultiplier);
        SkillVfxElementAsset element = source.WorldVisualProfile?.SuppressDefaultGroundImpact == true
            ? null : ModClass.I.SkillV3.ResolveGroundEffectElement(skill, emission.RuntimeData);
        return new LegacyAreaImpact(path, radius, source.DealsBaseDamage, element);
    }
}
