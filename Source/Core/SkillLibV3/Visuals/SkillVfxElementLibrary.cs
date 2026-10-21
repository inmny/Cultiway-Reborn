using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Modifiers;
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
        Generic.SetVisualSemantic(SkillSemantics.Element.Generic, prefer: false)
            .SetAccent(Color.white)
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

            return ResolveForAsset(semantics,skill.Asset,entity,skill.SkillContainer);
        }

        if (entity.HasComponent<SkillContainer>())
        {
            var container = entity.GetComponent<SkillContainer>();
            SkillSemanticCollector.CollectAssetSemantics(container.Asset, semantics);
            SkillSemanticCollector.CollectModifierSemantics(entity, semantics);
            return ResolveForAsset(semantics,container.Asset,entity);
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

        return ResolveForAsset(semantics,skill.Asset,skillEntity,skill.SkillContainer);
    }

    public SkillVfxElementAsset Resolve(SkillContainer container, Entity containerEntity)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectAssetSemantics(container.Asset, semantics);
        SkillSemanticCollector.CollectModifierSemantics(containerEntity, semantics);
        return ResolveForAsset(semantics,container.Asset,containerEntity);
    }

    public SkillVfxElementAsset Resolve(SkillEntityAsset asset)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectAssetSemantics(asset, semantics);
        return ResolveForAsset(semantics,asset);
    }

    public SkillVfxElementAsset Resolve(ElementComposition element)
    {
        var semantics = SkillSemanticCollector.NewSet();
        SkillSemanticCollector.CollectElementSemantics(element, semantics);
        return ResolveSemantics(semantics);
    }

    /// <summary>只解析本体明确声明的元素，供爆炸等作用形式取回所属流派。</summary>
    /// <param name="asset">具有明确语义的法术本体。</param>
    public SkillVfxElementAsset ResolvePrimaryElement(SkillEntityAsset asset)
    {
        HashSet<SemanticAsset> declared=new();AddDeclaredElements(asset.Semantics,declared);
        return declared.Count>0?ResolveSemantics(declared):Resolve(asset.Element);
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

    /// <summary>有明确元素声明时保留声明的元素与词条，少量混合成分不覆盖主流派。</summary>
    /// <param name="semantics">已有的完整效果语义。</param><param name="asset">法术本体。</param>
    /// <param name="first">执行体或容器上的真实词条。</param><param name="second">另一层实际词条。</param>
    private SkillVfxElementAsset ResolveForAsset(HashSet<SemanticAsset> semantics,SkillEntityAsset asset,Entity first=default,Entity second=default)
    {
        HashSet<SemanticAsset> declared=new();
        AddDeclaredElements(asset.Semantics,declared);
        AddModifiers(first);AddModifiers(second);
        if(declared.Count>0)
            semantics.RemoveWhere(semantic=>semantic.Facet==ModClass.L.SemanticFacetLibrary.Element && !declared.Contains(semantic));
        return ResolveSemantics(semantics);

        void AddModifiers(Entity entity)
        {
            if(entity.IsNull)return;
            foreach(var type in entity.GetComponentTypes())
                if(typeof(IModifier).IsAssignableFrom(type))AddDeclaredElements(((IModifier)entity.GetComponent(type)).ModifierAsset.Semantics,declared);
        }
    }

    /// <summary>只收直接且正向的元素声明，弱蕴含仍可参与原来的伤害计算。</summary>
    /// <param name="descriptor">本体或真实词条的声明。</param><param name="output">直接元素集合。</param>
    private static void AddDeclaredElements(SemanticDescriptor descriptor,HashSet<SemanticAsset> output)
    {
        foreach(SemanticContribution contribution in descriptor.contributions)
            if(contribution.polarity==SemanticPolarity.Positive && contribution.strength>0f && contribution.confidence>0f &&
                ModClass.L.SemanticLibrary.TryResolve(contribution.semantic_id,out SemanticAsset semantic) &&
                semantic.Facet==ModClass.L.SemanticFacetLibrary.Element && semantic!=SkillSemantics.Element.Generic)
                output.Add(semantic);
    }

}
