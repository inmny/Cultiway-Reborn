using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;

namespace Cultiway.Core.SkillLibV3.Visuals;

public class SkillVfxElementLibrary : AssetLibrary<SkillVfxElementAsset>
{
    private const float ElementTagThreshold = 0.25f;

    public SkillVfxElementAsset Resolve(Entity entity)
    {
        var tags = NewTagSet();
        if (entity.HasComponent<SkillEntity>())
        {
            var skill = entity.GetComponent<SkillEntity>();
            CollectAssetTags(skill.Asset, tags);
            CollectModifierTags(entity, tags);
            if (!skill.SkillContainer.IsNull)
            {
                CollectModifierTags(skill.SkillContainer, tags);
            }

            return ResolveTags(tags);
        }

        if (entity.HasComponent<SkillContainer>())
        {
            var container = entity.GetComponent<SkillContainer>();
            CollectAssetTags(container.Asset, tags);
            CollectModifierTags(entity, tags);
            return ResolveTags(tags);
        }

        return get(SkillVfxElementIds.Generic);
    }

    public SkillVfxElementAsset Resolve(SkillEntity skill, Entity skillEntity)
    {
        var tags = NewTagSet();
        CollectAssetTags(skill.Asset, tags);
        CollectModifierTags(skillEntity, tags);
        if (!skill.SkillContainer.IsNull)
        {
            CollectModifierTags(skill.SkillContainer, tags);
        }

        return ResolveTags(tags);
    }

    public SkillVfxElementAsset Resolve(SkillContainer container, Entity containerEntity)
    {
        var tags = NewTagSet();
        CollectAssetTags(container.Asset, tags);
        CollectModifierTags(containerEntity, tags);
        return ResolveTags(tags);
    }

    public SkillVfxElementAsset Resolve(SkillEntityAsset asset)
    {
        var tags = NewTagSet();
        CollectAssetTags(asset, tags);
        return ResolveTags(tags);
    }

    public SkillVfxElementAsset Resolve(ElementComposition element)
    {
        var tags = NewTagSet();
        CollectElementTags(element, tags);
        return ResolveTags(tags);
    }

    private SkillVfxElementAsset ResolveTags(HashSet<string> tags)
    {
        var bestScore = -1;
        SkillVfxElementAsset best = null;
        foreach (var element in list)
        {
            var score = element.ScoreTags(tags);
            if (score <= bestScore) continue;

            bestScore = score;
            best = element;
        }

        return bestScore < 0 ? get(SkillVfxElementIds.Generic) : best;
    }

    private static HashSet<string> NewTagSet()
    {
        return new HashSet<string>(System.StringComparer.Ordinal);
    }

    private static void CollectAssetTags(SkillEntityAsset asset, HashSet<string> tags)
    {
        foreach (var tag in asset.SeriesTags)
        {
            tags.Add(tag);
        }

        CollectElementTags(asset.Element, tags);
    }

    private static void CollectModifierTags(Entity entity, HashSet<string> tags)
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

    private static void CollectElementTags(ElementComposition element, HashSet<string> tags)
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
