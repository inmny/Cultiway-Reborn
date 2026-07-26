using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Combat;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 金丹与元婴特殊机制使用的明确 SkillEntity 资产。
/// 每个属性都是一个普通来源授予技能，不通过同步执行器或命名表现通道结算。
/// </summary>
[Dependency(typeof(SkillVfxElements), typeof(SkillTrajectories), typeof(SkillCastResources), typeof(StatusEffects))]
internal sealed class CoreFormationSkills : ICanInit
{
    internal const string SkillAssetIdPrefix = "Cultiway.CoreFormation.Skill.";
    internal const float AnimationScaleMultiplier = 0.25f;
    private const float FrameInterval = 0.05f;
    private static readonly List<Entity> SkillContainers = new();

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
            0f, false, false, OnIronSeverance);
        WoodVenomBloom = BuildTarget(
            "WoodVenomBloom", "wood_venom_bloom", 0.09f, ElementComposition.Static.Wood,
            0f, false, true, OnWoodVenomBloom);
        WoodLifeReturn = BuildTarget(
            "WoodLifeReturn", "wood_life_return", 0.08f, ElementComposition.Static.Wood,
            0f, false, true, OnWoodLifeReturn, SkillUseProfileLibrary.CasterSelf);
        WaterFrostBind = BuildTarget(
            "WaterFrostBind", "water_frost_bind", 0.09f, ElementComposition.Static.Water,
            0f, false, true, OnWaterFrostBind);
        FireBrand = BuildTarget(
            "FireBrand", "fire_brand", 0.075f, ElementComposition.Static.Fire,
            0f, false, true, OnFireBrand);
        FireEmberBurst = BuildTarget(
            "FireEmberBurst", "fire_ember_burst", 0.1f, ElementComposition.Static.Fire,
            0f, false, true, OnFireEmberBurst);
        EarthWard = BuildVisual("EarthWard", "earth_ward", 0.09f, ElementComposition.Static.Earth);
        EarthWardImpact = BuildVisual(
            "EarthWardImpact", "earth_ward_impact", 0.085f, ElementComposition.Static.Earth);
        YinDrain = BuildTarget(
            "YinDrain", "yin_drain", 0.1f, Element(ElementIndex.Neg),
            0f, true, false, OnYinDrain);
        MysteriousYinDrain = BuildTarget(
            "MysteriousYinDrain", "yin_drain", 0.12f, Element(ElementIndex.Neg),
            0f, true, false, OnYinDrain);
        YangCleanse = BuildTarget(
            "YangCleanse", "yang_cleanse", 0.1f, Element(ElementIndex.Pos),
            0f, false, true, OnYangCleanse, SkillUseProfileLibrary.CasterSelf);
        PureYangCleanse = BuildTarget(
            "PureYangCleanse", "yang_cleanse", 0.12f, Element(ElementIndex.Pos),
            0f, false, true, OnYangCleanse, SkillUseProfileLibrary.CasterSelf);
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
            0f, false, true, OnCondensedRelease);
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
            0.4f, false, true, OnBodyCounter);
        PrimalBodyCounter = BuildTarget(
            "PrimalBodyCounter", "primal_body_counter", 0.11f, ElementComposition.Static.Earth,
            0.4f, false, true, OnBodyCounter);
        IllusionDecoy = BuildVisual(
            "IllusionDecoy", "illusion_decoy", 0.1f, Element(ElementIndex.Neg));
        DragonMight = BuildArea(
            "DragonMight", "dragon_might", 0.11f, ElementComposition.Static.Earth,
            4f, 0f, OnDragonMight);
        DragonAspectMight = BuildArea(
            "DragonAspectMight", "dragon_might", 0.13f, ElementComposition.Static.Earth,
            4f, 0f, OnDragonMight);
        InfantGuard = BuildVisual(
            "InfantGuard", "infant_guard", 0.1f, Element(ElementIndex.Pos));
        ChaosRebirth = BuildVisual(
            "ChaosRebirth", "chaos_rebirth", 0.16f, Element(ElementIndex.Entropy));
        FivePhaseStrike = BuildTarget(
            "FivePhaseStrike", "five_phase_strike", 0.09f, Element(ElementIndex.Entropy),
            0.25f, false, true);

        SwordEmbryoAura = BuildTarget(
            "SwordEmbryoAura", "sword_embryo_aura", 0.1f, ElementComposition.Static.Iron,
            0f, false, true, OnActivateSwordEmbryo, SkillUseProfileLibrary.CasterSelf, 32f,
            SkillEntityType.Defense);
        DragonAspectBurst = BuildArea(
            "DragonAspectBurst", "dragon_aspect_burst", 0.16f, ElementComposition.Static.Earth,
            4f, 0f, OnDragonAspectBurst, 40f);
        SpiritPlatform = BuildTarget(
            "SpiritPlatform", "spirit_platform", 0.13f, Element(ElementIndex.Pos),
            0f, false, true, OnActivateSpiritPlatform, SkillUseProfileLibrary.CasterSelf, 48f,
            SkillEntityType.Defense);
        PrimalBody = BuildTarget(
            "PrimalBody", "primal_body", 0.12f, ElementComposition.Static.Earth,
            0f, false, true, OnActivatePrimalBody, SkillUseProfileLibrary.CasterSelf, 40f,
            SkillEntityType.Defense);
        FivePhase = BuildTarget(
            "FivePhase", "five_phase", 0.14f, Element(ElementIndex.Entropy),
            0f, false, true, OnActivateFivePhase, SkillUseProfileLibrary.CasterSelf, 48f,
            SkillEntityType.Defense);
        PureYangDomain = BuildArea(
            "PureYangDomain", "pure_yang_domain", 0.17f, Element(ElementIndex.Pos),
            5f, 0f, OnPureYangDomain, 56f);
        MysteriousYinDomain = BuildArea(
            "MysteriousYinDomain", "mysterious_yin_domain", 0.17f, Element(ElementIndex.Neg),
            5f, 0f, OnMysteriousYinDomain, 56f);
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
            null,
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
        EffectObjAction onEffect = null,
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
            onEffect,
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
        EffectObjAction onEffect,
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
            onEffect,
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
        EffectObjAction onEffect,
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
        ModClass.I.SkillV3.SkillLib.add(asset);

        Entity container = new SkillContainerBuilder(asset).Build(SkillContainerBuildMode.SourceGranted);
        ref SkillContainer skill = ref container.GetComponent<SkillContainer>();
        skill.OnEffectObj = onEffect;
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

    /// <summary>取得仍存活的施法者与受影响角色，并读取本次技能上下文。</summary>
    private static bool TryGetActors(
        Entity skillEntity,
        BaseSimObject value,
        out SkillContext context,
        out Actor source,
        out Actor target)
    {
        context = skillEntity.GetComponent<SkillContext>();
        source = null;
        target = null;
        if (context.SourceObj.isRekt() || !context.SourceObj.isActor() ||
            value.isRekt() || !value.isActor()) return false;
        source = context.SourceObj.a;
        target = value.a;
        return true;
    }

    /// <summary>破甲；目标已被同源破甲时追加金行反应伤害。</summary>
    private static void OnIronSeverance(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        bool alreadyBroken = CombatStatusEffects.HasStatus(target, StatusEffects.ArmorBreak, source);
        CombatStatusEffects.ApplyStatus(target, StatusEffects.ArmorBreak, 4f, source);
        if (alreadyBroken)
            CombatDamageEffects.DealReactionDamage(
                source,
                target,
                context.Strength * context.EffectScale * 0.25f,
                ElementComposition.Static.Iron,
                attackerPowerLevel: context.PowerLevel);
    }

    /// <summary>把触发伤害的一部分转换成五秒木行毒伤。</summary>
    private static void OnWoodVenomBloom(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        float totalDamage = context.Strength * context.EffectScale * 0.35f;
        CombatStatusEffects.ApplyTickingStatus(
            target,
            StatusEffects.Poison,
            5f,
            totalDamage / 5f,
            ElementComposition.Static.Wood,
            source);
    }

    /// <summary>恢复施法者的生命与灵气。</summary>
    private static void OnWoodLifeReturn(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out _)) return;
        CombatResourceEffects.RestoreHealth(source, source.stats[S.health] * 0.025f * context.EffectScale);
        CombatResourceEffects.RestoreWakan(source, 6f * context.EffectScale);
    }

    /// <summary>首次命中减速，再次命中同源减速时改为短暂冻结。</summary>
    private static void OnWaterFrostBind(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out _, out Actor source, out Actor target)) return;
        bool slowed = CombatStatusEffects.HasStatus(target, StatusEffects.Slow, source);
        CombatStatusEffects.ApplyStatus(
            target,
            slowed ? StatusEffects.Freeze : StatusEffects.Slow,
            slowed ? 0.75f : 3f,
            source);
    }

    /// <summary>在目标身上施加四秒火行灼烧。</summary>
    private static void OnFireBrand(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        float totalDamage = context.Strength * context.EffectScale * 0.2f;
        CombatStatusEffects.ApplyTickingStatus(
            target,
            StatusEffects.Burn,
            4f,
            totalDamage / 4f,
            ElementComposition.Static.Fire,
            source);
    }

    /// <summary>消耗目标的同源灼烧，并在目标周围产生火行爆发。</summary>
    private static void OnFireEmberBurst(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        CombatStatusEffects.RemoveStatus(target, StatusEffects.Burn, source);
        CombatDamageEffects.DealAreaReactionDamage(
            source,
            target.current_position,
            2.5f,
            context.Strength * context.EffectScale * 0.35f,
            ElementComposition.Static.Fire);
    }

    /// <summary>汲取目标灵气；无灵气可取时施加衰弱和阴行伤害。</summary>
    private static void OnYinDrain(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        float drained = CombatResourceEffects.DrainWakan(target, 8f * context.EffectScale);
        if (drained > 0f)
        {
            CombatResourceEffects.RestoreWakan(source, drained);
            return;
        }
        CombatStatusEffects.ApplyStatus(target, StatusEffects.Weaken, 4f, source);
        CombatDamageEffects.DealReactionDamage(
            source,
            target,
            context.Strength * context.EffectScale * 0.25f,
            Element(ElementIndex.Neg),
            attackerPowerLevel: context.PowerLevel);
    }

    /// <summary>净化一个负面状态并恢复施法者生命。</summary>
    private static void OnYangCleanse(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out _)) return;
        CombatStatusEffects.CleanseNegativeStatuses(source, 1);
        float maxHealth = Mathf.Max(1f, source.stats[S.health]);
        float heal = Mathf.Min(maxHealth * 0.075f, maxHealth * 0.03f * context.EffectScale);
        CombatResourceEffects.RestoreHealth(source, heal);
    }

    /// <summary>在明确目标处释放凝元范围伤害。</summary>
    private static void OnCondensedRelease(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        CombatDamageEffects.DealAreaReactionDamage(
            source,
            target.current_position,
            2f,
            context.Strength * context.EffectScale * 0.35f,
            context.ResolveElement(Element(ElementIndex.Entropy)));
    }

    /// <summary>把炼体反击碰撞命中的明确攻击者推离施法者。</summary>
    private static void OnBodyCounter(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        CombatForceEffects.ApplyRadialForce(
            source,
            target,
            source.current_position,
            2f * context.EffectScale,
            false);
    }

    /// <summary>龙威实体对每个实际敌对目标施加震慑和击退。</summary>
    private static void OnDragonMight(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target) ||
            target == source || !source.canAttackTarget(target)) return;
        CombatStatusEffects.ApplyStatus(target, StatusEffects.Daze, 0.6f, source);
        Vector2 center = skillEntity.GetComponent<Position>().v2;
        CombatForceEffects.ApplyRadialForce(source, target, center, 2.5f * context.EffectScale, false);
    }

    /// <summary>龙相主动震击只对实际可攻击目标造成伤害、眩晕和击退。</summary>
    private static void OnDragonAspectBurst(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target) ||
            target == source || !source.canAttackTarget(target)) return;
        CombatDamageEffects.DealDamage(
            source,
            target,
            context.Strength * context.EffectScale * 1.2f,
            context.ResolveElement(ElementComposition.Static.Earth),
            attackerPowerLevel: context.PowerLevel,
            damageOrigin: context.RuntimeData.DamageOrigin);
        CombatStatusEffects.ApplyStatus(target, StatusEffects.Daze, 0.8f, source);
        Vector2 center = skillEntity.GetComponent<Position>().v2;
        CombatForceEffects.ApplyRadialForce(source, target, center, 3f * context.EffectScale, false);
    }

    /// <summary>剑胎主动技能建立持续形态，并清除被动追击的一秒节流。</summary>
    private static void OnActivateSwordEmbryo(Entity skillEntity, BaseSimObject value)
    {
        if (!TryResolveActive(skillEntity, value, CoreFormationEffectFamilies.Sword,
                out ActorExtend owner, out CoreFormationResolvedEffect effect)) return;
        Entity status = CoreFormationStateService.Activate(
            owner,
            effect,
            effect.Definition.active.duration,
            true,
            out CoreFormationEffectState state);
        if (status.IsNull) return;
        CoreFormationStateService.Save(status, state);
        SkillCooldownService.Clear(owner, effect.Definition.CooldownSkill);
    }

    /// <summary>灵台主动技能建立持续形态并补充四次回响。</summary>
    private static void OnActivateSpiritPlatform(Entity skillEntity, BaseSimObject value)
    {
        if (!TryResolveActive(skillEntity, value, CoreFormationEffectFamilies.Spiritual,
                out ActorExtend owner, out CoreFormationResolvedEffect effect)) return;
        Entity status = CoreFormationStateService.Activate(
            owner,
            effect,
            effect.Definition.active.duration,
            true,
            out CoreFormationEffectState state);
        if (status.IsNull) return;
        state.charges = 4;
        CoreFormationStateService.Save(status, state);
    }

    /// <summary>真身主动技能建立提供伤害上限和抗击退的持续状态。</summary>
    private static void OnActivatePrimalBody(Entity skillEntity, BaseSimObject value)
    {
        if (!TryResolveActive(skillEntity, value, CoreFormationEffectFamilies.Body,
                out ActorExtend owner, out CoreFormationResolvedEffect effect)) return;
        Entity status = CoreFormationStateService.Activate(
            owner,
            effect,
            effect.Definition.active.duration,
            true,
            out CoreFormationEffectState state);
        if (!status.IsNull) CoreFormationStateService.Save(status, state);
    }

    /// <summary>五相主动技能从金相开始建立两秒一相的持续轮转。</summary>
    private static void OnActivateFivePhase(Entity skillEntity, BaseSimObject value)
    {
        if (!TryResolveActive(skillEntity, value, CoreFormationEffectFamilies.FivePhase,
                out ActorExtend owner, out CoreFormationResolvedEffect effect)) return;
        Entity status = CoreFormationStateService.Activate(
            owner,
            effect,
            effect.Definition.active.duration,
            true,
            out CoreFormationEffectState state);
        if (status.IsNull) return;
        state.phase = 0;
        state.auxiliary_timer = 2f;
        state.secondary_value = 0f;
        CoreFormationStateService.Save(status, state);
    }

    /// <summary>纯阳领域分别处理同国友军和施法者实际可攻击的目标。</summary>
    private static void OnPureYangDomain(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target)) return;
        if (target == source || target.kingdom == source.kingdom)
        {
            CombatStatusEffects.CleanseNegativeStatuses(target, 2);
            CombatResourceEffects.RestoreHealth(
                target,
                target.stats[S.health] * 0.05f * context.EffectScale);
        }
        if (target == source || !source.canAttackTarget(target)) return;
        float burnPerSecond = source.stats[S.damage] * 0.05f * context.EffectScale;
        CombatStatusEffects.ApplyTickingStatus(
            target,
            StatusEffects.Burn,
            4f,
            burnPerSecond,
            Element(ElementIndex.Pos),
            source);
    }

    /// <summary>玄阴领域冻结、沉默并汲取每个实际敌对目标的灵气。</summary>
    private static void OnMysteriousYinDomain(Entity skillEntity, BaseSimObject value)
    {
        if (!TryGetActors(skillEntity, value, out SkillContext context, out Actor source, out Actor target) ||
            target == source || !source.canAttackTarget(target)) return;
        CombatStatusEffects.ApplyStatus(target, StatusEffects.Freeze, 1f, source);
        CombatStatusEffects.ApplyStatus(target, StatusEffects.Silence, 3f, source);
        float drained = CombatResourceEffects.DrainWakan(target, 12f * context.EffectScale);
        CombatResourceEffects.RestoreWakan(source, drained);
    }

    /// <summary>验证主动技能的明确自身目标，并解析当前仍生效的效果定义。</summary>
    private static bool TryResolveActive(
        Entity skillEntity,
        BaseSimObject value,
        string familyId,
        out ActorExtend owner,
        out CoreFormationResolvedEffect effect)
    {
        owner = null;
        effect = default;
        if (!TryGetActors(skillEntity, value, out _, out Actor source, out Actor target) ||
            source != target) return false;
        owner = source.GetExtend();
        return CoreFormationEffectResolver.TryResolveFamily(owner, familyId, out effect) &&
               effect.Definition.active != null;
    }

    /// <summary>构造单一元素组成。</summary>
    private static ElementComposition Element(int index)
    {
        var composition = new ElementComposition();
        composition[index] = 1f;
        return composition;
    }
}
