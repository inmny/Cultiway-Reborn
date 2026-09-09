using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.Semantics;
using Cultiway.Core.SkillLibV3.Utils;
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
        SemanticContributorService.Register(new XianAchievementContributor());
        SemanticContributorService.Register(new CultivationPracticeContributor());
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
        ElementSemanticProfileService.Contribute(builder, composition, multiplier, scope, source);
    }
}

internal sealed class ElementRootContributor : IActorSemanticContributor
{
    public string Id => "content.element_root";
    public int Priority => 100;

    /// <summary>将角色灵根的类型与元素组成写入固有语义画像。</summary>
    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!actor.E.TryGetComponent(out ElementRoot root)) return;
        var source = new SemanticSourceRef(Id, root.Type.id);
        var multiplier = 1f + Mathf.Log(1f + root.GetStrength(), 2f) * 0.25f;
        ElementSemanticProfileService.Contribute(
            builder,
            root,
            multiplier,
            SemanticScope.Intrinsic,
            source);
    }
}

internal sealed class CultibookContributor : IActorSemanticContributor
{
    public string Id => "content.cultibook";
    public int Priority => 200;

    /// <summary>将角色主修功法和其他已学功法按掌握度写入已学习语义画像。</summary>
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
        var levelMultiplier = container.TryGetComponent(out ItemLevel level) ? 1f + (int)level / 35f : 1f;
        SkillSemanticCollector.ContributeProfile(container, builder, levelMultiplier, SemanticScope.Learned, Id);
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

/// <summary>只把角色当前境界的仙道成果写入固有语义，历史成果保持归档而不重复贡献。</summary>
internal sealed class XianAchievementContributor : IActorSemanticContributor
{
    public string Id => "content.xian_achievement";
    public int Priority => 500;

    /// <summary>按当前成果境界、强度和精炼次数计算有界语义权重。</summary>
    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!CoreFormationEffectResolver.TryGetFormation(actor, out var current)) return;
        float realmScale = current.Snapshot.realm switch
        {
            CoreFormationRealm.QiRefinement => 0.55f,
            CoreFormationRealm.Foundation => 0.75f,
            CoreFormationRealm.Jindan => 1f,
            CoreFormationRealm.Yuanying => 1.25f,
            CoreFormationRealm.Yuanshen => 1.5f,
            _ => throw new ArgumentOutOfRangeException(nameof(current.Snapshot.realm), current.Snapshot.realm,
                "未知核心形成境界。")
        };
        float multiplier = realmScale +
                           Mathf.Log(1f + Mathf.Max(0f, current.Strength), 2f) * 0.25f +
                           Mathf.Min(0.5f, current.Stage * 0.02f);
        var source = new SemanticSourceRef(Id, current.Snapshot.signature);
        builder.Add(SemanticDescriptor.Weighted(current.Snapshot.semantics), multiplier,
            SemanticScope.Intrinsic, source);
    }
}

/// <summary>把角色按修炼方式累计的实践量转换为历史语义。</summary>
internal sealed class CultivationPracticeContributor : IActorSemanticContributor
{
    private const float MaximumSemanticStrength = 4f;
    private const float SaturationMonths = 20f;

    public string Id => "content.cultivation_practice";
    public int Priority => 600;

    /// <summary>逐种修炼方式压缩长期实践，再通过当前资产派生语义。</summary>
    public void Contribute(ActorExtend actor, SemanticProfileBuilder builder)
    {
        if (!actor.TryGetComponent(out CultivationPracticeState state) || state.methods == null) return;
        for (var i = 0; i < state.methods.Length; i++)
        {
            CultivationMethodPracticeEntry entry = state.methods[i];
            if (entry.effective_months <= 0f ||
                !Libraries.Manager.CultivateMethodLibrary.dict.TryGetValue(
                    entry.method_id,
                    out CultivateMethodAsset method))
                continue;

            float strength = MaximumSemanticStrength *
                             (1f - Mathf.Exp(-entry.effective_months / SaturationMonths));
            builder.Add(method.Semantics, strength, SemanticScope.Historical,
                new SemanticSourceRef(Id, method.id));
        }
    }
}
