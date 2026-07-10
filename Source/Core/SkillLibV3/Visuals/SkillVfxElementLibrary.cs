using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Friflo.Engine.ECS;
using UnityEngine;
using ElementTag = Cultiway.Core.SkillLibV3.SkillTags.Element;

namespace Cultiway.Core.SkillLibV3.Visuals;

public class SkillVfxElementLibrary : AssetLibrary<SkillVfxElementAsset>
{
    public static SkillVfxElementAsset Generic { get; private set; }

    public override void init()
    {
        base.init();
        Generic = add(new SkillVfxElementAsset
        {
            id = "Cultiway.SkillVfxElement.Generic"
        });
        Generic.SetAccent(Color.white)
            .SetImpactSound("event:/SFX/HIT/HitGeneric")
            .MatchAny(0, ElementTag.Generic);
    }

    public SkillVfxElementAsset Resolve(Entity entity)
    {
        var tags = SkillSemanticTags.NewSet();
        if (entity.HasComponent<SkillEntity>())
        {
            var skill = entity.GetComponent<SkillEntity>();
            SkillSemanticTags.CollectAssetTags(skill.Asset, tags);
            SkillSemanticTags.CollectModifierTags(entity, tags);
            if (!skill.SkillContainer.IsNull)
            {
                SkillSemanticTags.CollectModifierTags(skill.SkillContainer, tags);
            }

            return ResolveTags(tags);
        }

        if (entity.HasComponent<SkillContainer>())
        {
            var container = entity.GetComponent<SkillContainer>();
            SkillSemanticTags.CollectAssetTags(container.Asset, tags);
            SkillSemanticTags.CollectModifierTags(entity, tags);
            return ResolveTags(tags);
        }

        return get(Generic.id);
    }

    public SkillVfxElementAsset Resolve(SkillEntity skill, Entity skillEntity)
    {
        var tags = SkillSemanticTags.NewSet();
        SkillSemanticTags.CollectAssetTags(skill.Asset, tags);
        SkillSemanticTags.CollectModifierTags(skillEntity, tags);
        if (!skill.SkillContainer.IsNull)
        {
            SkillSemanticTags.CollectModifierTags(skill.SkillContainer, tags);
        }

        return ResolveTags(tags);
    }

    public SkillVfxElementAsset Resolve(SkillContainer container, Entity containerEntity)
    {
        var tags = SkillSemanticTags.NewSet();
        SkillSemanticTags.CollectAssetTags(container.Asset, tags);
        SkillSemanticTags.CollectModifierTags(containerEntity, tags);
        return ResolveTags(tags);
    }

    public SkillVfxElementAsset Resolve(SkillEntityAsset asset)
    {
        var tags = SkillSemanticTags.NewSet();
        SkillSemanticTags.CollectAssetTags(asset, tags);
        return ResolveTags(tags);
    }

    public SkillVfxElementAsset Resolve(ElementComposition element)
    {
        var tags = SkillSemanticTags.NewSet();
        SkillSemanticTags.CollectElementTags(element, tags);
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

        return bestScore < 0 ? get(Generic.id) : best;
    }

}
