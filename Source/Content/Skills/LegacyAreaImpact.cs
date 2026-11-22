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
    /// <summary>在落地中心使用原人物选取的状态范围。</summary>
    internal readonly bool ActorStatuses;

    private LegacyAreaImpact(string path, float radius, bool dealsBaseDamage, SkillVfxElementAsset element, bool actorStatuses = false)
    {
        Path = path;
        Radius = radius;
        DealsBaseDamage = dealsBaseDamage;
        Element = element;
        ActorStatuses = actorStatuses;
    }

    /// <summary>功能术没有基础伤害，原对象状态仍使用落地中心和地面半径。</summary>
    /// <param name="radius">原单位中心范围。</param>
    internal static LegacyAreaImpact ForUtility(float radius) => new("root", radius, false, null, actorStatuses: true);

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
