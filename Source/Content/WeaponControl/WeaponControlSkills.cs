using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.Components.AnimOverwrite;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Core.Semantics;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using strings;
using UnityEngine;

namespace Cultiway.Content.WeaponControl;

/// <summary>注册筑基后由仙道体系直接授予的只读御器技能与统一主动能力入口。</summary>
[Dependency(typeof(SkillCastResources), typeof(SkillMotionProfiles), typeof(SkillVfxElements),
    typeof(WeaponControlTrajectories))]
public sealed class WeaponControlSkills : ExtendLibrary<SkillEntityAsset, WeaponControlSkills>
{
    private const string SourceDetailLocaleKey = "Cultiway.WeaponControl.SourceDetail";
    private static Entity skillContainer;

    /// <summary>承载真实武器近战执行体和远程投射步骤的御器技能资产。</summary>
    public static SkillEntityAsset WeaponControl { get; private set; }

    /// <summary>让公开静态技能属性自动注册到 SkillLib。</summary>
    protected override bool AutoRegisterAssets() => true;

    /// <summary>保持技能 ID 为 Cultiway.WeaponControl，供本地化与战斗句柄稳定引用。</summary>
    protected override string Prefix() => "Cultiway";

    /// <summary>配置技能实体、来源容器、主动能力 Provider 和跨模块生命周期挂钩。</summary>
    protected override void OnInit()
    {
        var impact = new SkillImpactProfileAsset
        {
            id = "Cultiway.WeaponControl.Impact",
            Kind = SkillImpactKind.Wave,
            CollisionRadius = 0.72f,
            DamageMultiplier = 0.55f,
            ContinueAfterHit = true,
            HitOncePerTarget = true,
            Lifetime = 0.62f,
            CostMultiplier = 1f,
            ExpectedTargets = 2f,
        };

        WeaponControl.Element = new ElementComposition();
        WeaponControl.AddSemantics(
            SkillSemantics.Element.Generic,
            SkillSemantics.Role.Offensive);
        WeaponControl.Type = SkillEntityType.Attack;
        WeaponControl.EditorSelectable = false;
        WeaponControl.EditorCategoryKey = "Cultiway.SkillEntity.Category.Attack";
        WeaponControl.EditorDescriptionKey = "Cultiway.WeaponControl.Description";
        WeaponControl
            .RequireCastResource(SkillCastResources.Wakan)
            .SetBaseCastDemand(1f)
            .SetDealsBaseDamage(false)
            .SetIcon("ui/icons/items/icon_sword_iron")
            .SetupCommonPrefab("cultiway/effect/flying_sword/1/runtime", 1f, true)
            .SetupImpactProfile(impact, new ColliderConfig
            {
                Enabled = false,
                Actor = true,
                Building = true,
                Enemy = true,
            })
            .SetupDefaultTraj(WeaponControlTrajectories.WeaponMotion)
            .SetupUseProfile(SkillUseProfileLibrary.EnemyObjectOrPoint)
            .AcceptTrajectoryDomains(SkillTrajectoryDomain.Melee)
            .SetupVisualRotation(VisualRotation.FollowRotation());
        WeaponControl.PrefabEntity.AddComponent(new AnimRuntimeFrames());
        WeaponControl.PrefabEntity.AddComponent(new AnimAfterimageOverride());
        WeaponControl.PrefabEntity.AddComponent(new MotionRibbonTrail());
        WeaponControl.PrefabEntity.AddComponent(new MotionRibbonTrailBinder());

        skillContainer = new SkillContainerBuilder(WeaponControl)
            .Build(SkillContainerBuildMode.SourceGranted);
        skillContainer.GetComponent<SkillContainer>().OnSetup = ConfigureExecution;

        var provider = new WeaponControlAbilityProvider(skillContainer, SourceDetailLocaleKey);
        ActiveAbilityService.Register(provider);
        SourceGrantedSkillService.Register(provider);
        PatchActor.RegisterHideHandItemPredicate(WeaponControlRuntimeService.IsWeaponDetached);
        PatchMapBox.RegisterActionOnClearWorld(WeaponControlRuntimeService.ClearWorldState);
        WeaponControl.OnObjCollision = ResolveWeaponHit;
    }

    /// <summary>把通用施放步骤按关联 ID 交给对应的御器会话配置。</summary>
    private static void ConfigureExecution(Entity execution)
    {
        long correlationId = execution.GetComponent<SkillContext>().RuntimeData.CorrelationId;
        if (correlationId == 0 ||
            !WeaponControlRuntimeService.TryGet(correlationId, out WeaponControlCastSession session))
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(execution.Id);
            return;
        }
        session.ConfigureExecution(execution);
    }

    /// <summary>让近战武器实体通过原版攻击结算造成伤害并触发当前装备的全部攻击效果。</summary>
    private static bool ResolveWeaponHit(
        ref SkillContext context,
        Entity container,
        Entity execution,
        BaseSimObject target)
    {
        if (context.SourceObj.isRekt() || !context.SourceObj.isActor() || target.isRekt()) return true;
        Actor owner = context.SourceObj.a;
        ref WeaponControlMotionState state = ref execution.GetComponent<WeaponControlMotionState>();
        if (!WeaponControlRuntimeService.IsCurrentWeapon(owner, state.Weapon) ||
            !owner.canAttackTarget(target)) return true;

        Vector3 hitPosition = new(
            target.current_position.x,
            target.current_position.y,
            target.getHeight());
        var attack = new AttackData(
            owner,
            target.current_tile,
            hitPosition,
            owner.current_position,
            target,
            context.ResolveAttackKingdom() ?? owner.kingdom,
            AttackType.Weapon,
            owner.haveMetallicWeapon(),
            true);
        float previousMultiplier = AttackDamageScaleContext.Enter(state.DamageMultiplier);
        try
        {
            MapBox.checkAttackFor(attack, target);
        }
        finally
        {
            AttackDamageScaleContext.Restore(previousMultiplier);
        }

        return true;
    }
}

/// <summary>把御器的来源展示、AI 决策和实际释放接入通用主动能力协议。</summary>
internal sealed class WeaponControlAbilityProvider : IActiveAbilityProvider, ISourceGrantedSkillProvider
{
    private const string ProviderId = "content.weapon_control";
    private const string EntryId = "weapon_control";
    private readonly Entity skillContainer;
    private readonly string sourceDetailLocaleKey;

    /// <summary>创建一个同时公开只读来源技能和战斗能力的 Provider。</summary>
    public WeaponControlAbilityProvider(Entity skillContainer, string sourceDetailLocaleKey)
    {
        this.skillContainer = skillContainer;
        this.sourceDetailLocaleKey = sourceDetailLocaleKey;
    }

    /// <summary>返回主动能力与来源技能注册表共同使用的稳定 ID。</summary>
    public string Id => ProviderId;

    /// <summary>筑基及以上角色始终能在技能页看到御器，即使当前没有装备武器。</summary>
    public void Collect(ActorExtend actor, ICollection<SourceGrantedSkillPresentation> output)
    {
        if (!WeaponControlRules.IsEligibleCultivator(actor)) return;
        output.Add(new SourceGrantedSkillPresentation(skillContainer, sourceDetailLocaleKey));
    }

    /// <summary>筑基及以上角色获得一个御器战斗能力句柄。</summary>
    public void Collect(ActorExtend caster, ICollection<ActiveAbilityHandle> output)
    {
        if (!WeaponControlRules.IsEligibleCultivator(caster)) return;
        output.Add(new ActiveAbilityHandle(Id, skillContainer, EntryId));
    }

    /// <summary>御器只参与战斗决策，不作为世界工具释放。</summary>
    public ActiveAbilityChannel GetChannels(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return IsValidHandle(handle) && WeaponControlRules.IsEligibleCultivator(caster)
            ? ActiveAbilityChannel.Combat
            : ActiveAbilityChannel.None;
    }

    /// <summary>返回玩家控制和战术规划使用的御器名称、图标与目标模式。</summary>
    public ActiveAbilityDescriptor Describe(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return new ActiveAbilityDescriptor(
            WeaponControlSkills.WeaponControl.id.Localize(),
            WeaponControlSkills.WeaponControl.Icon,
            ActiveAbilityChannel.Combat,
            ActiveAbilityTargetMode.Object,
            ActiveAbilityActivationMode.Sustained,
            ActiveAbilityCastMobility.StationaryDuringRecovery,
            SkillUseTargetRelation.Hostile);
    }

    /// <summary>检查境界、当前武器、飞行状态、技能冷却与至少一发灵气。</summary>
    public bool CanPrepare(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!IsValidHandle(handle) || !WeaponControlRules.IsEligibleCultivator(caster) ||
            caster.Base.isFlying() || WeaponControlRuntimeService.IsCasting(caster) ||
            !SkillCooldownService.IsReady(caster, skillContainer) ||
            !SkillCastCost.CanPayStep(caster, skillContainer) || target.isRekt() ||
            !caster.Base.canAttackTarget(target) ||
            !WeaponControlRules.TryResolveWeapon(caster, out _, out _, out WeaponControlCategory category))
            return false;
        return category == WeaponControlCategory.Ranged || !target.isFlying();
    }

    /// <summary>在准备条件之外检查目标是否已经进入当前器形的实际控制距离。</summary>
    public bool CanUse(ActorExtend caster, ActiveAbilityHandle handle, in ActiveAbilityTarget target)
    {
        if (!CanPrepare(caster, handle, target.Object) ||
            !WeaponControlRules.TryResolveWeapon(caster, out _, out _, out WeaponControlCategory category))
            return false;
        float range = WeaponControlRules.ResolveSelectionRange(caster, category) + target.Object.stats[S.size];
        return (target.Object.current_position - caster.Base.current_position).sqrMagnitude <= range * range;
    }

    /// <summary>按境界和本轮可负担发数提高御器在高压、多目标战斗中的选择权重。</summary>
    public int ResolveAiWeight(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        if (!WeaponControlRules.TryResolveWeapon(caster, out _, out _, out WeaponControlCategory category))
            return 0;
        int expected = WeaponControlRules.ResolveExpectedEmissionCount(
            caster, skillContainer, target, category);
        int realm = WeaponControlRules.ResolveRealm(caster);
        return Mathf.Clamp(6 + realm * 4 + Mathf.CeilToInt(Mathf.Sqrt(expected)), 1, 32);
    }

    /// <summary>根据真实武器伤害、预计发数与器形公开战术强度，不复用静态技能评级。</summary>
    public ActiveAbilityTacticalProfile ResolveTacticalProfile(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        BaseSimObject target)
    {
        if (!WeaponControlRules.TryResolveWeapon(caster, out _, out _, out WeaponControlCategory category))
            return default;
        int expected = WeaponControlRules.ResolveExpectedEmissionCount(
            caster, skillContainer, target, category);
        float strikeMultiplier = category == WeaponControlCategory.Ranged ? 0.22f : 0.58f;
        float power = Mathf.Max(1f, caster.Base.stats[S.damage]) * strikeMultiplier * expected;
        float expectedTargets = category == WeaponControlCategory.Ranged
            ? Mathf.Clamp(expected * 0.28f, 1f, 12f)
            : Mathf.Clamp(expected * 0.35f, 1f, 8f);
        SkillImpactKind impact = category switch
        {
            WeaponControlCategory.Ranged => SkillImpactKind.Projectile,
            WeaponControlCategory.Spear => SkillImpactKind.Piercing,
            _ => SkillImpactKind.Wave,
        };
        return new ActiveAbilityTacticalProfile(
            power,
            0f,
            0f,
            category == WeaponControlCategory.Hammer ? power * 0.12f : 0f,
            power,
            expected,
            expectedTargets,
            impact);
    }

    /// <summary>返回当前装备器形独立于原版近战范围的御器控制距离。</summary>
    public float ResolveRange(ActorExtend caster, ActiveAbilityHandle handle, BaseSimObject target)
    {
        return WeaponControlRules.TryResolveWeapon(caster, out _, out _, out WeaponControlCategory category)
            ? WeaponControlRules.ResolveSelectionRange(caster, category)
            : 0f;
    }

    /// <summary>返回玩家预览使用的招式展开半径。</summary>
    public float ResolveEffectRadius(ActorExtend caster, ActiveAbilityHandle handle)
    {
        return WeaponControlRules.TryResolveWeapon(caster, out _, out _, out WeaponControlCategory category) &&
               category == WeaponControlCategory.Ranged
            ? 4f
            : 2.25f;
    }

    /// <summary>构造动态目标计划，并通过通用 QueueSkillSequence 逐发支付和生成执行体。</summary>
    public bool TryUse(
        ActorExtend caster,
        ActiveAbilityHandle handle,
        in ActiveAbilityTarget target,
        ActiveAbilityUseOrigin origin)
    {
        if (!CanUse(caster, handle, target) ||
            !WeaponControlRules.TryPrepareCast(
                caster,
                skillContainer,
                target.Object,
                target.AttackKingdom,
                out WeaponControlPreparedCast prepared)) return false;

        long correlationId = WeaponControlRuntimeService.NextCorrelationId();
        SkillCastRuntimeData runtimeData = target.RuntimeData;
        if (runtimeData.EffectScale <= 0f) runtimeData.EffectScale = 1f;
        runtimeData.DamageOrigin = DamageOrigin.Primary;
        runtimeData.CorrelationId = correlationId;
        var session = new WeaponControlCastSession(
            caster,
            prepared.Weapon,
            prepared.WeaponAsset,
            prepared.Category,
            prepared.Mode,
            skillContainer,
            target.AttackKingdom ?? caster.Base.kingdom,
            prepared.Range,
            prepared.Duration,
            correlationId);
        return ModClass.I.SkillV3.QueueSkillSequence(
            caster,
            skillContainer,
            prepared.Plan,
            caster.Base.stats[S.damage],
            caster.GetPowerLevel(),
            SkillCastFundingSource.CasterResources,
            target.AttackKingdom ?? caster.Base.kingdom,
            runtimeData,
            new SkillCastSequenceOptions
            {
                PaymentTiming = SkillCastPaymentTiming.PerEmission,
                Hooks = session,
                MaxEmitPerTick = 8,
            });
    }

    /// <summary>验证句柄来自当前 Provider、共享来源容器和唯一御器条目。</summary>
    private bool IsValidHandle(ActiveAbilityHandle handle)
    {
        return handle.ProviderId == Id && handle.Source == skillContainer && handle.EntryId == EntryId;
    }
}
