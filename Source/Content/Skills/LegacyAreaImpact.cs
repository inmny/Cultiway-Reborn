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

    private LegacyAreaImpact(string path, float radius, bool dealsBaseDamage, SkillVfxElementAsset element)
    {
        Path = path;
        Radius = radius;
        DealsBaseDamage = dealsBaseDamage;
        Element = element;
    }

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
