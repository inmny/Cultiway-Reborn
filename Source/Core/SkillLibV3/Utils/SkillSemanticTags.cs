using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 统一收集法术资产、元素、词条与最终轨迹的语义标签。
/// </summary>
public static class SkillSemanticTags
{
    private const float ElementTagThreshold = 0.25f;

    public static HashSet<string> NewSet()
    {
        return new HashSet<string>(StringComparer.Ordinal);
    }

    public static void CollectAssetTags(SkillEntityAsset asset, HashSet<string> tags)
    {
        foreach (var tag in asset.SeriesTags)
        {
            tags.Add(tag);
        }

        CollectElementTags(asset.Element, tags);
    }

    public static void CollectModifierTags(Entity entity, HashSet<string> tags)
    {
        foreach (var type in entity.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;

            var modifier = (IModifier)entity.GetComponent(type);
            var asset = modifier.ModifierAsset;
            tags.Add(asset.id);
            foreach (var tag in asset.SimilarityTags)
            {
                tags.Add(tag);
            }

            foreach (var tag in asset.ConflictTags)
            {
                tags.Add(tag);
            }
        }
    }

    public static void CollectTrajectoryTags(SkillEntityAsset asset, Entity containerEntity, HashSet<string> tags)
    {
        TrajectoryAsset trajectory;
        if (containerEntity.HasComponent<Trajectory>())
        {
            trajectory = containerEntity.GetComponent<Trajectory>().Asset;
        }
        else
        {
            trajectory = asset.PrefabEntity.GetComponent<Trajectory>().Asset;
        }

        foreach (var tag in trajectory.MotionTags)
        {
            tags.Add(tag);
        }
    }

    public static void CollectElementTags(ElementComposition element, HashSet<string> tags)
    {
        var hasTag = false;
        hasTag |= AddElementTag(tags, element.iron, ElementTag.Iron);
        hasTag |= AddElementTag(tags, element.wood, ElementTag.Wood);
        hasTag |= AddElementTag(tags, element.water, ElementTag.Water);
        hasTag |= AddElementTag(tags, element.fire, ElementTag.Fire);
        hasTag |= AddElementTag(tags, element.earth, ElementTag.Earth);
        hasTag |= AddElementTag(tags, element.neg, ElementTag.Neg);
        hasTag |= AddElementTag(tags, element.pos, ElementTag.Pos);
        hasTag |= AddElementTag(tags, element.entropy, ElementTag.Entropy);
        if (!hasTag)
        {
            tags.Add(ElementTag.Generic);
        }
    }

    private static bool AddElementTag(HashSet<string> tags, float value, string tag)
    {
        if (value <= ElementTagThreshold) return false;

        tags.Add(tag);
        return true;
    }
}
