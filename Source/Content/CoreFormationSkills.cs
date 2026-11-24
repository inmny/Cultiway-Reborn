using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Visuals;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 金丹与元婴特殊机制使用的明确 SkillEntity 资产。
/// 每个属性都是一个普通来源授予技能，不通过同步执行器或命名表现通道结算。
/// </summary>
[Dependency(typeof(SkillEntities), typeof(SkillVfxElements), typeof(SkillTrajectories), typeof(SkillCastResources), typeof(StatusEffects))]
internal sealed class CoreFormationSkills : ICanInit
{
    internal const string SkillAssetIdPrefix = "Cultiway.CoreFormation.Skill.";
    internal const float AnimationScaleMultiplier = 0.25f;
    private const float FrameInterval = 0.05f;
    private static readonly List<Entity> SkillContainers = new();

    internal static IReadOnlyList<Entity> RegisteredContainers => SkillContainers;

    internal static Entity IronSeverance { get; private set; }
    internal static Entity WoodVenomBloom { get; private set; }
    internal static Entity WoodLifeReturn { get; private set; }
    internal static Entity WaterFrostBind { get; private set; }
    internal static Entity FireBrand { get; private set; }
    internal static Entity FireEmberBurst { get; private set; }
    internal static Entity EarthWard { get; private set; }
    internal static Entity EarthWardImpact { get; private set; }
    internal static Entity YinDrain { get; private set; }
    internal static Entity MysteriousYinDrain { get; private set; }
    internal static Entity YangCleanse { get; private set; }
    internal static Entity PureYangCleanse { get; private set; }
    internal static Entity ChaosEcho { get; private set; }
    internal static Entity ChaosRebirthEcho { get; private set; }
    internal static Entity BalancedAdaptation { get; private set; }
    internal static Entity ReservoirOrb { get; private set; }
    internal static Entity CondensedRelease { get; private set; }
    internal static Entity SpiritEcho { get; private set; }
    internal static Entity SwordChase { get; private set; }
    internal static Entity SwordEmbryoStrike { get; private set; }
    internal static Entity BodyCounter { get; private set; }
    internal static Entity PrimalBodyCounter { get; private set; }
    internal static Entity IllusionDecoy { get; private set; }
    internal static Entity DragonMight { get; private set; }
    internal static Entity DragonAspectMight { get; private set; }
    internal static Entity InfantGuard { get; private set; }
    internal static Entity ChaosRebirth { get; private set; }
    internal static Entity FivePhaseStrike { get; private set; }

    internal static Entity SwordEmbryoAura { get; private set; }
    internal static Entity DragonAspectBurst { get; private set; }
    internal static Entity SpiritPlatform { get; private set; }
    internal static Entity PrimalBody { get; private set; }
    internal static Entity FivePhase { get; private set; }
    internal static Entity PureYangDomain { get; private set; }
    internal static Entity MysteriousYinDomain { get; private set; }

    /// <summary>建立全部明确技能资产及其世界级来源授予容器。</summary>
    public void Init()
    {
        SkillContainers.Clear();
        IronSeverance = BuildTarget(
            "IronSeverance", "iron_severance", 0.09f, ElementComposition.Static.Iron,
            0f, false, false);
        WoodVenomBloom = BuildTarget(
            "WoodVenomBloom", "wood_venom_bloom", 0.09f, ElementComposition.Static.Wood,
            0f, false, true);
        WoodLifeReturn = BuildTarget(
            "WoodLifeReturn", "wood_life_return", 0.08f, ElementComposition.Static.Wood,
            0f, false, true, useProfile: SkillUseProfileLibrary.CasterSelf);
        WaterFrostBind = BuildTarget(
            "WaterFrostBind", "water_frost_bind", 0.09f, ElementComposition.Static.Water,
            0f, false, true);
        FireBrand = BuildTarget(
            "FireBrand", "fire_brand", 0.075f, ElementComposition.Static.Fire,
            0f, false, true);
        FireEmberBurst = BuildTarget(
            "FireEmberBurst", "fire_ember_burst", 0.1f, ElementComposition.Static.Fire,
            0f, false, true);
        EarthWard = BuildVisual("EarthWard", "earth_ward", 0.09f, ElementComposition.Static.Earth);
        EarthWardImpact = BuildVisual(
            "EarthWardImpact", "earth_ward_impact", 0.085f, ElementComposition.Static.Earth);
        YinDrain = BuildTarget(
            "YinDrain", "yin_drain", 0.1f, Element(ElementIndex.Neg),
            0f, true, false);
        MysteriousYinDrain = BuildTarget(
            "MysteriousYinDrain", "yin_drain", 0.12f, Element(ElementIndex.Neg),
            0f, true, false);
        YangCleanse = BuildTarget(
            "YangCleanse", "yang_cleanse", 0.1f, Element(ElementIndex.Pos),
            0f, false, true, useProfile: SkillUseProfileLibrary.CasterSelf);
        PureYangCleanse = BuildTarget(
            "PureYangCleanse", "yang_cleanse", 0.12f, Element(ElementIndex.Pos),
            0f, false, true, useProfile: SkillUseProfileLibrary.CasterSelf);
        ChaosEcho = BuildTarget(
            "ChaosEcho", "chaos_echo", 0.1f, Element(ElementIndex.Entropy),
            0.3f, false, true);
        ChaosRebirthEcho = BuildTarget(
            "ChaosRebirthEcho", "chaos_echo", 0.13f, Element(ElementIndex.Entropy),
            0.3f, false, true);
        BalancedAdaptation = BuildVisual(
            "BalancedAdaptation", "balanced_adaptation", 0.09f, Element(ElementIndex.Entropy));
        ReservoirOrb = BuildVisual(
            "ReservoirOrb", "reservoir_orb", 0.08f, ElementComposition.Static.Wood);
        CondensedRelease = BuildTarget(
            "CondensedRelease", "condensed_release", 0.09f, Element(ElementIndex.Entropy),
            0f, false, true);
        SpiritEcho = BuildVisual(
            "SpiritEcho", "spirit_echo", 0.08f, Element(ElementIndex.Pos));
        SwordChase = BuildTarget(
            "SwordChase", "sword_chase", 0.09f, ElementComposition.Static.Iron,
            0.35f, true, false);
        SwordEmbryoStrike = BuildTarget(
            "SwordEmbryoStrike", "sword_embryo_strike", 0.11f, ElementComposition.Static.Iron,
            0.35f, true, false);
        BodyCounter = BuildTarget(
            "BodyCounter", "body_counter", 0.09f, ElementComposition.Static.Earth,
            0.4f, false, true);
        PrimalBodyCounter = BuildTarget(
            "PrimalBodyCounter", "primal_body_counter", 0.11f, ElementComposition.Static.Earth,
            0.4f, false, true);
        IllusionDecoy = BuildVisual(
            "IllusionDecoy", "illusion_decoy", 0.1f, Element(ElementIndex.Neg));
        DragonMight = BuildArea(
            "DragonMight", "dragon_might", 0.11f, ElementComposition.Static.Earth,
            4f, 0f);
        DragonAspectMight = BuildArea(
            "DragonAspectMight", "dragon_might", 0.13f, ElementComposition.Static.Earth,
            4f, 0f);
        InfantGuard = BuildVisual(
            "InfantGuard", "infant_guard", 0.1f, Element(ElementIndex.Pos));
        ChaosRebirth = BuildVisual(
            "ChaosRebirth", "chaos_rebirth", 0.16f, Element(ElementIndex.Entropy));
        FivePhaseStrike = BuildTarget(
            "FivePhaseStrike", "five_phase_strike", 0.09f, Element(ElementIndex.Entropy),
            0.25f, false, true);

        SwordEmbryoAura = BuildTarget(
            "SwordEmbryoAura", "sword_embryo_aura", 0.1f, ElementComposition.Static.Iron,
            0f, false, true, useProfile: SkillUseProfileLibrary.CasterSelf, cost: 32f,
            type: SkillEntityType.Defense);
        DragonAspectBurst = BuildArea(
            "DragonAspectBurst", "dragon_aspect_burst", 0.16f, ElementComposition.Static.Earth,
            4f, 0f, 40f);
        SpiritPlatform = BuildTarget(
            "SpiritPlatform", "spirit_platform", 0.13f, Element(ElementIndex.Pos),
            0f, false, true, useProfile: SkillUseProfileLibrary.CasterSelf, cost: 48f,
            type: SkillEntityType.Defense);
        PrimalBody = BuildTarget(
            "PrimalBody", "primal_body", 0.12f, ElementComposition.Static.Earth,
            0f, false, true, useProfile: SkillUseProfileLibrary.CasterSelf, cost: 40f,
            type: SkillEntityType.Defense);
        FivePhase = BuildTarget(
            "FivePhase", "five_phase", 0.14f, Element(ElementIndex.Entropy),
            0f, false, true, useProfile: SkillUseProfileLibrary.CasterSelf, cost: 48f,
            type: SkillEntityType.Defense);
        PureYangDomain = BuildArea(
            "PureYangDomain", "pure_yang_domain", 0.17f, Element(ElementIndex.Pos),
            5f, 0f, 56f);
        MysteriousYinDomain = BuildArea(
            "MysteriousYinDomain", "mysterious_yin_domain", 0.17f, Element(ElementIndex.Neg),
            5f, 0f, 56f);
    }

    /// <summary>建立只播放一次动画、不参与碰撞的目标显现技能。</summary>
    private static Entity BuildVisual(
        string id,
        string folder,
        float scale,
        ElementComposition element)
    {
        return Build(
            id,
            folder,
            scale,
            element,
            SkillTrajectories.AppearAtTarget,
            SkillTrajectoryDomain.TargetManifest,
            SkillUseProfileLibrary.CasterSelf,
            CreateImpact(id, SkillImpactKind.Projectile, 0f, 0f, false, true, false, folder),
            default,
            0f,
            SkillEntityType.Defense,
            true);
    }

    /// <summary>建立只命中施法步骤明确目标的单目标技能。</summary>
    private static Entity BuildTarget(
        string id,
        string folder,
        float scale,
        ElementComposition element,
        float damageMultiplier,
        bool moving,
        bool fixedUpright,
        SkillUseProfileAsset useProfile = null,
        float cost = 0f,
        SkillEntityType type = SkillEntityType.Attack)
    {
        TrajectoryAsset trajectory = moving ? SkillTrajectories.TowardsTarget : SkillTrajectories.AppearAtTarget;
        SkillTrajectoryDomain domain = moving
            ? SkillTrajectoryDomain.FlyingBody
            : SkillTrajectoryDomain.TargetManifest;
        SkillImpactProfileAsset impact = CreateImpact(
            id,
            moving ? SkillImpactKind.Projectile : SkillImpactKind.Wave,
            1f,
            damageMultiplier,
            moving,
            !moving,
            true,
            folder);
        return Build(
            id,
            folder,
            scale,
            element,
            trajectory,
            domain,
            useProfile ?? SkillUseProfileLibrary.EnemyObjectOrPoint,
            impact,
            new ColliderConfig
            {
                Enabled = true,
                Actor = true,
                Enemy = true,
                Alias = true,
                ExplicitTargetOnly = true,
            },
            cost,
            type,
            fixedUpright);
    }

    /// <summary>建立在目标点显现并对范围内每个单位至多结算一次的技能。</summary>
    private static Entity BuildArea(
        string id,
        string folder,
        float scale,
        ElementComposition element,
        float radius,
        float damageMultiplier,
        float cost = 0f)
    {
        return Build(
            id,
            folder,
            scale,
            element,
            SkillTrajectories.AppearAtTarget,
            SkillTrajectoryDomain.TargetManifest,
            SkillUseProfileLibrary.EnemyArea,
            CreateImpact(id, SkillImpactKind.Wave, radius, damageMultiplier, false, true, true, folder, radius),
            new ColliderConfig
            {
                Enabled = true,
                Actor = true,
                Enemy = true,
                Alias = true,
            },
            cost,
            SkillEntityType.Attack,
            true);
    }

    /// <summary>建立技能资产、普通执行体和不可学习的来源授予容器。</summary>
    private static Entity Build(
        string id,
        string folder,
        float scale,
        ElementComposition element,
        TrajectoryAsset trajectory,
        SkillTrajectoryDomain domain,
        SkillUseProfileAsset useProfile,
        SkillImpactProfileAsset impact,
        ColliderConfig collider,
        float cost,
        SkillEntityType type,
        bool fixedUpright)
    {
        string assetId = $"{SkillAssetIdPrefix}{id}";
        SkillEntityAnimation animation = SkillEntityAnimation.Create(
            $"cultiway/effect/core_formation/{folder}",
            scale * AnimationScaleMultiplier,
            SkillEntityAnimationSettings.Inherit
                .WithFrameInterval(FrameInterval)
                .WithLoop(false));
        var asset = new SkillEntityAsset
        {
            id = assetId,
            Element = element,
            Type = type,
            EditorSelectable = false,
            EditorCategoryKey = "Cultiway.CoreFormation.Page.Effects",
            EditorDescriptionKey = $"{assetId}.Description",
        };
        asset.RequireCastResource(SkillCastResources.Wakan)
            .SetupCommonPrefab(animation, false)
            .SetupImpactProfile(impact, collider)
            .SetupDefaultTraj(trajectory)
            .SetupUseProfile(useProfile)
            .AcceptTrajectoryDomains(domain)
            .SetupVisualRotation(fixedUpright
                ? VisualRotation.FixedUpright()
                : VisualRotation.FollowRotation());
        if (id is "BodyCounter" or "PrimalBodyCounter" or "PrimalBody")
            asset.RequireCarrier(SkillCarrierRequirement.PhysicalBody);
        OriginalSkillGrowthRules.Add(asset);
        ModClass.I.SkillV3.SkillLib.add(asset);

        Entity container = new SkillContainerBuilder(asset).Build(SkillContainerBuildMode.SourceGranted);
        ref SkillCastParameters parameters = ref container.GetComponent<SkillCastParameters>();
        parameters.CostMultiplier = Mathf.Max(0f, cost);
        SkillContainerEvaluator.Refresh(container);
        SkillContainers.Add(container);
        return container;
    }

    /// <summary>清除角色身上全部形成来源技能的冷却，不影响其他体系或已学技能。</summary>
    internal static void ClearCooldowns(ActorExtend actor)
    {
        for (var i = 0; i < SkillContainers.Count; i++)
            SkillCooldownService.Clear(actor, SkillContainers[i]);
    }

    /// <summary>按动画长度建立技能的碰撞、伤害和完整播放寿命。</summary>
    private static SkillImpactProfileAsset CreateImpact(
        string id,
        SkillImpactKind kind,
        float collisionRadius,
        float damageMultiplier,
        bool recycleOnHit,
        bool continueAfterHit,
        bool hitOncePerTarget,
        string folder,
        float effectRadius = 0f)
    {
        int frameCount = SkillEntityAsset
            .LoadOrderedFrames($"cultiway/effect/core_formation/{folder}")
            .Length;
        return new SkillImpactProfileAsset
        {
            id = $"{SkillAssetIdPrefix}{id}.Impact",
            Kind = kind,
            CollisionRadius = collisionRadius,
            EffectRadius = effectRadius,
            DamageMultiplier = damageMultiplier,
            RecycleOnHit = recycleOnHit,
            ContinueAfterHit = continueAfterHit,
            HitOncePerTarget = hitOncePerTarget,
            Lifetime = Mathf.Max(FrameInterval, frameCount * FrameInterval),
            CostMultiplier = 1f,
            ExpectedTargets = collisionRadius > 1f ? 3f : 1f,
        };
    }

    /// <summary>构造单一元素组成。</summary>
    private static ElementComposition Element(int index)
    {
        var composition = new ElementComposition();
        composition[index] = 1f;
        return composition;
    }
}
