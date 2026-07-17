using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Semantics;
using Friflo.Engine.ECS;
using UnityEngine;

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
            .SetGrantDrop(WorldboxGame.Drops.WanfaEntropy)
            .SetImpactSound("event:/SFX/HIT/HitGeneric")
            .MatchAny(0, SkillSemantics.Element.Generic);
    }

    public SkillVfxElementAsset Resolve(Entity entity)
    {
        var semantics = SkillSemanticCollector.NewSet();
        if (entity.HasComponent<SkillEntity>())
        {
            var skill = entity.GetComponent<SkillEntity>();
            SkillSemanticCollector.CollectAssetSemantics(skill.Asset, semantics);
            SkillSemanticCollector.CollectModifierSemantics(entity, semantics);
            if (!skill.SkillContainer.IsNull)
            {
                SkillSemanticCollector.CollectModifierSemantics(skill.SkillContainer, semantics);
            }

            return ResolveSemantics(semantics);
        }

        if (entity.HasComponent<SkillContainer>())
        {
            var container = entity.GetComponent<SkillContainer>();
            SkillSemanticCollector.CollectAssetSemantics(container.Asset, semantics);
            SkillSemanticCollector.CollectModifierSemantics(entity, semantics);
            return ResolveSemantics(semantics);
        }

        return get(Generic.id);
    }

    public SkillVfxElementAsset Resolve(SkillEntity skill, Entity skillEntity)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectAssetSemantics(skill.Asset, semantics);
        SkillSemanticCollector.CollectModifierSemantics(skillEntity, semantics);
        if (!skill.SkillContainer.IsNull)
        {
            SkillSemanticCollector.CollectModifierSemantics(skill.SkillContainer, semantics);
        }

        return ResolveSemantics(semantics);
    }

    public SkillVfxElementAsset Resolve(SkillContainer container, Entity containerEntity)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectAssetSemantics(container.Asset, semantics);
        SkillSemanticCollector.CollectModifierSemantics(containerEntity, semantics);
        return ResolveSemantics(semantics);
    }

    public SkillVfxElementAsset Resolve(SkillEntityAsset asset)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectAssetSemantics(asset, semantics);
        return ResolveSemantics(semantics);
    }

    public SkillVfxElementAsset Resolve(ElementComposition element)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectElementSemantics(element, semantics);
        return ResolveSemantics(semantics);
    }

    private SkillVfxElementAsset ResolveSemantics(HashSet<SemanticAsset> semantics)
    {
        var bestScore = -1;
        SkillVfxElementAsset best = null;
        foreach (var element in list)
        {
            var score = element.ScoreSemantics(semantics);
            if (score <= bestScore) continue;

            bestScore = score;
            best = element;
        }

        return bestScore < 0 ? get(Generic.id) : best;
    }

}
