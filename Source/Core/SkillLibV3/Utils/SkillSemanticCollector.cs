using System;
using System.Collections.Generic;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 收集法术资产、连续元素组成、实际词条和最终轨迹的规范语义。
/// </summary>
public static class SkillSemanticCollector
{
    public static HashSet<SemanticAsset> NewSet()
    {
        return new HashSet<SemanticAsset>();
    }

    public static void CollectAssetSemantics(SkillEntityAsset asset, HashSet<SemanticAsset> semantics)
    {
        CollectDescriptorSemantics(asset.Semantics, semantics);
        CollectElementSemantics(asset.Element, semantics);
    }

    public static void CollectModifierSemantics(Entity entity, HashSet<SemanticAsset> semantics)
    {
        foreach (var type in entity.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            var modifier = (IModifier)entity.GetComponent(type);
            CollectDescriptorSemantics(modifier.ModifierAsset.Semantics, semantics);
        }
    }

    public static void CollectTrajectorySemantics(
        SkillEntityAsset asset,
        Entity containerEntity,
        HashSet<SemanticAsset> semantics)
    {
        var trajectory = containerEntity.HasComponent<Trajectory>()
            ? containerEntity.GetComponent<Trajectory>().Asset
            : asset.PrefabEntity.GetComponent<Trajectory>().Asset;
        CollectDescriptorSemantics(trajectory.Semantics, semantics);
    }

    public static void CollectElementSemantics(ElementComposition element, HashSet<SemanticAsset> semantics)
    {
        var any = false;
        any |= AddElement(semantics, element.iron, SkillSemantics.Element.Iron);
        any |= AddElement(semantics, element.wood, SkillSemantics.Element.Wood);
        any |= AddElement(semantics, element.water, SkillSemantics.Element.Water);
        any |= AddElement(semantics, element.fire, SkillSemantics.Element.Fire);
        any |= AddElement(semantics, element.earth, SkillSemantics.Element.Earth);
        any |= AddElement(semantics, element.neg, SkillSemantics.Element.Neg);
        any |= AddElement(semantics, element.pos, SkillSemantics.Element.Pos);
        any |= AddElement(semantics, element.entropy, SkillSemantics.Element.Entropy);
        if (!any) AddExpanded(SkillSemantics.Element.Generic, semantics);
    }

    public static void CollectDescriptorSemantics(
        SemanticDescriptor descriptor,
        HashSet<SemanticAsset> semantics)
    {
        descriptor?.CollectExpanded(ModClass.L.SemanticLibrary, semantics);
    }

    private static bool AddElement(HashSet<SemanticAsset> semantics, float value, SemanticAsset semantic)
    {
        if (value <= 0f) return false;
        AddExpanded(semantic, semantics);
        return true;
    }

    private static void AddExpanded(SemanticAsset semantic, HashSet<SemanticAsset> semantics)
    {
        var expansion = ModClass.L.SemanticLibrary.Expand(semantic);
        for (var i = 0; i < expansion.Count; i++) semantics.Add(expansion[i].semantic);
    }
}
