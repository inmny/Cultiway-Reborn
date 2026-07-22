using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Visuals;

/// <summary>
/// 法术视觉元素颜色工具。
/// </summary>
public static class SkillVfxColor
{
    public static SkillVfxElementAsset ResolveElement(SkillEntityAsset asset)
    {
        return ModClass.I.SkillV3.VfxElementLib.Resolve(asset);
    }

    public static SkillVfxElementAsset ResolveElement(Entity entity)
    {
        if (entity.HasComponent<SkillEntity>())
        {
            return entity.GetComponent<SkillEntity>().VfxElement;
        }

        return entity.GetComponent<SkillContainer>().VfxElement;
    }

    public static SkillVfxElementAsset ResolveElement(SkillEntity skill)
    {
        return skill.VfxElement;
    }

    public static SkillVfxElementAsset ResolveElement(SkillContainer container)
    {
        return container.VfxElement;
    }

    public static SkillVfxElementAsset ResolveElement(ElementComposition element)
    {
        return ModClass.I.SkillV3.VfxElementLib.Resolve(element);
    }

    public static Color GetElementColor(ElementComposition element)
    {
        var palette = SemanticColorResolver.Resolve(ElementSemanticProfileService.Build(element));
        var color = palette.GetColor(0, Color.white);
        color.a = 0.82f;
        return color;
    }
}
