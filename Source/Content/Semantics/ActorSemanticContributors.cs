using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Semantics;

/// <summary>
/// 注册 Content 拥有的角色语义来源。各贡献器互不读取对方的内部状态。
/// </summary>
[Dependency(typeof(CultivationSemantics))]
public sealed class ActorSemanticContributors : ICanInit
{
    public void Init()
    {
        SemanticContributorService.Register(new ElementRootContributor());
        SemanticContributorService.Register(new CultibookContributor());
        SemanticContributorService.Register(new LearnedSkillContributor());
        SemanticContributorService.Register(new EquippedArtifactContributor());
        SemanticContributorService.Register(new JindanContributor());
        SemanticContributorService.Register(new YuanyingContributor());
    }
}

internal static class SemanticContributorTools
{
    public static void AddElements(
        SemanticProfileBuilder builder,
        ElementComposition composition,
        float multiplier,
        SemanticScope scope,
        SemanticSourceRef source)
    {
        var total = Mathf.Max(0f, composition.iron)
                    + Mathf.Max(0f, composition.wood)
                    + Mathf.Max(0f, composition.water)
                    + Mathf.Max(0f, composition.fire)
                    + Mathf.Max(0f, composition.earth)
                    + Mathf.Max(0f, composition.neg)
                    + Mathf.Max(0f, composition.pos)
                    + Mathf.Max(0f, composition.entropy);
        if (total <= 0f) builder.Add(SkillSemantics.Element.Generic, multiplier, scope, source);
        else
        {
            AddElement(builder, SkillSemantics.Element.Iron, composition.iron / total, multiplier, scope, source);
            AddElement(builder, SkillSemantics.Element.Wood, composition.wood / total, multiplier, scope, source);
            AddElement(builder, SkillSemantics.Element.Water, composition.water / total, multiplier, scope, source);
            AddElement(builder, SkillSemantics.Element.Fire, composition.fire / total, multiplier, scope, source);
            AddElement(builder, SkillSemantics.Element.Earth, composition.earth / total, multiplier, scope, source);
            AddElement(builder, SkillSemantics.Element.Neg, composition.neg / total, multiplier, scope, source);
            AddElement(builder, SkillSemantics.Element.Pos, composition.pos / total, multiplier, scope, source);
            AddElement(builder, SkillSemantics.Element.Entropy, composition.entropy / total, multiplier, scope, source);
        }
    }

    private static void AddElement(
        SemanticProfileBuilder builder,
        SemanticAsset semantic,
        float value,
        float multiplier,
        SemanticScope scope,
        SemanticSourceRef source)
    {
        var strength = Mathf.Max(0f, value);
        if (strength > 0f) builder.Add(semantic, strength * multiplier, scope, source);
    }

}

internal sealed class ElementRootContributor : IActorSemanticContributor
{
    public string Id => "content.element_root";
    public int Priority => 100;

    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!actor.E.TryGetComponent(out ElementRoot root)) return;
        var source = new SemanticSourceRef(Id, root.Type.id);
        var multiplier = 1f + Mathf.Log(1f + root.GetStrength(), 2f) * 0.25f;
        builder.Add(root.Type.Semantics, multiplier, SemanticScope.Intrinsic, source);
        SemanticContributorTools.AddElements(builder,
            new ElementComposition([root.Iron, root.Wood, root.Water, root.Fire, root.Earth, root.Neg, root.Pos,
                root.Entropy]),
            multiplier, SemanticScope.Intrinsic, source);
    }
}

internal sealed class CultibookContributor : IActorSemanticContributor
{
    public string Id => "content.cultibook";
    public int Priority => 200;

    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!actor.E.TryGetComponent(out ActorCultibookState state) || !state.HasMainCultibook) return;

        var book = state.MainCultibook;
        var mastery = Mathf.Clamp01(state.MainMastery / 100f);
        var source = new SemanticSourceRef(Id, book.id);
        if (book.Semantics.contributions.Length == 0)
            book.Semantics = CultibookRuleComposer.ComposeSemantics(book);
        builder.Add(book.Semantics, mastery, SemanticScope.Learned, source);
    }
}

internal sealed class LearnedSkillContributor : IActorSemanticContributor
{
    public string Id => "core.learned_skill";
    public int Priority => 300;

    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        var skills = actor.GetLearnedSkillsInOrder();
        for (var i = 0; i < skills.Count; i++) ContributeSkill(skills[i], builder);
    }

    private void ContributeSkill(Entity container, SemanticProfileBuilder builder)
    {
        var skill = container.GetComponent<SkillContainer>();
        var asset = skill.Asset;
        var levelMultiplier = container.TryGetComponent(out ItemLevel level) ? 1f + (int)level / 35f : 1f;
        var source = new SemanticSourceRef(Id, container, asset.id);

        builder.Add(asset.Semantics, levelMultiplier, SemanticScope.Learned, source);
        SemanticContributorTools.AddElements(builder, asset.Element, levelMultiplier,
            SemanticScope.Learned, source);

        foreach (var type in container.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(type)) continue;
            var modifier = (IModifier)container.GetComponent(type);
            var modifierAsset = modifier.ModifierAsset;
            var modifierSource = new SemanticSourceRef(Id, container, modifierAsset.id);
            builder.Add(modifierAsset.Semantics, levelMultiplier, SemanticScope.Learned, modifierSource);
        }

        var trajectory = container.HasComponent<Trajectory>()
            ? container.GetComponent<Trajectory>().Asset
            : asset.PrefabEntity.GetComponent<Trajectory>().Asset;
        builder.Add(trajectory.Semantics, levelMultiplier, SemanticScope.Learned,
            new SemanticSourceRef(Id, container, trajectory.id));
    }
}

internal sealed class EquippedArtifactContributor : IActorSemanticContributor
{
    public string Id => "content.equipped_artifact";
    public int Priority => 400;

    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        var relations = actor.E.GetRelations<EquippedArtifactRelation>();
        for (var i = 0; i < relations.Length; i++) ContributeArtifact(relations[i].artifact, builder);
    }

    private void ContributeArtifact(Entity artifact, SemanticProfileBuilder builder)
    {
        var attunement = artifact.GetComponent<ArtifactAttunement>();
        var multiplier = 0.5f + Mathf.Clamp01(attunement.mastery / 100f) * 0.75f +
                         (attunement.life_bound ? 0.25f : 0f);
        var source = new SemanticSourceRef(Id, artifact);

        var shape = (ArtifactShapeAsset)artifact.GetComponent<ItemShape>().Type;
        builder.Add(shape.semantics, multiplier, SemanticScope.Equipped,
            new SemanticSourceRef(Id, artifact, shape.id));

        var material = artifact.GetComponent<ArtifactMaterialData>();
        for (var i = 0; i < material.traits.Length; i++)
        {
            var trait = material.traits[i];
            if (trait.value <= 0f || !ModClass.L.SemanticLibrary.TryResolve(trait.key, out var semantic)) continue;
            builder.Add(semantic, Mathf.Log(1f + trait.value, 2f) * multiplier,
                SemanticScope.Equipped, source);
        }

        var abilitySet = artifact.GetComponent<ArtifactAbilitySet>();
        for (var i = 0; i < abilitySet.abilities.Length; i++)
        {
            var ability = Libraries.Manager.ArtifactAbilityLibrary.get(abilitySet.abilities[i].ability_id);
            var abilitySource = new SemanticSourceRef(Id, artifact, ability.id);
            builder.Add(ability.semantics, multiplier, SemanticScope.Equipped, abilitySource);
            AddUseProfile(builder, ability.use_profile, multiplier, abilitySource);
        }
    }

    private static void AddUseProfile(
        SemanticProfileBuilder builder,
        ArtifactUseProfile profile,
        float multiplier,
        SemanticSourceRef source)
    {
        if (profile.offensive > 0f)
            builder.Add(SkillSemantics.Role.Offensive, profile.offensive * multiplier, SemanticScope.Equipped, source);
        if (profile.defensive > 0f)
            builder.Add(SkillSemantics.Role.Defensive, profile.defensive * multiplier, SemanticScope.Equipped, source);
        if (profile.support > 0f)
            builder.Add(SkillSemantics.Role.Support, profile.support * multiplier, SemanticScope.Equipped, source);
        if (profile.production > 0f)
            builder.Add(SkillSemantics.Role.Production, profile.production * multiplier, SemanticScope.Equipped, source);
        if (profile.cultivate > 0f)
            builder.Add(CultivationSemantics.Role.Cultivation, profile.cultivate * multiplier,
                SemanticScope.Equipped, source);
    }
}

internal sealed class JindanContributor : IActorSemanticContributor
{
    public string Id => "content.jindan";
    public int Priority => 500;

    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!actor.E.TryGetComponent(out Jindan jindan)) return;
        var asset = jindan.Type;
        var multiplier = 1f + Mathf.Log(1f + Mathf.Max(0f, jindan.strength), 2f) * 0.25f + jindan.stage * 0.2f;
        var source = new SemanticSourceRef(Id, asset.id);
        builder.Add(asset.Semantics, multiplier, SemanticScope.Intrinsic, source);
        SemanticContributorTools.AddElements(builder, asset.composition, multiplier,
            SemanticScope.Intrinsic, source);
    }
}

internal sealed class YuanyingContributor : IActorSemanticContributor
{
    public string Id => "content.yuanying";
    public int Priority => 600;

    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!actor.E.TryGetComponent(out Yuanying yuanying)) return;
        var asset = yuanying.Type;
        var multiplier = 1.25f + Mathf.Log(1f + Mathf.Max(0f, yuanying.strength), 2f) * 0.3f +
                         yuanying.stage * 0.25f;
        var source = new SemanticSourceRef(Id, asset.id);
        builder.Add(asset.Semantics, multiplier, SemanticScope.Intrinsic, source);
        SemanticContributorTools.AddElements(builder, asset.composition, multiplier,
            SemanticScope.Intrinsic, source);
    }
}
