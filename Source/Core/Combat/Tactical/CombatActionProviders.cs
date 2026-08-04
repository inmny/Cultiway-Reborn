using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils.Extension;
using HarmonyLib;
using strings;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 汇总战斗动作 Provider，并保证成功启动后才写入通用攻击恢复和独立动作冷却。
/// </summary>
public static class CombatActionService
{
    private const string OriginalActionProviderId = "core.original_combat_action";
    private static readonly List<ICombatActionProvider> Providers =
    [
        new PhysicalCombatActionProvider(),
        new VanillaSpellCombatActionProvider(),
        new ActiveAbilityCombatActionProvider(),
        new AdvancedCombatActionProvider(),
    ];

    /// <summary>收集角色当前具备条件的全部战斗动作，并单独记录本轮冷却是否结束。</summary>
    public static void Collect(
        ActorExtend caster,
        BaseSimObject primaryEnemy,
        Actor preferredAlly,
        float threatRatio,
        IReadOnlyList<Actor> nearbyAllies,
        IList<CombatActionCandidate> output)
    {
        output.Clear();
        if (caster == null || caster.Base.isRekt()) return;

        var context = new CombatActionCollectionContext(
            caster,
            primaryEnemy,
            preferredAlly,
            threatRatio,
            nearbyAllies);
        for (int i = 0; i < Providers.Count; i++)
        {
            Providers[i].Collect(context, output);
        }
        for (int i = 0; i < output.Count; i++)
        {
            CombatActionCandidate candidate = output[i];
            output[i] = candidate.WithReadiness(
                CombatWorldService.IsActionReady(caster.Base, candidate.Key));
        }
    }

    /// <summary>
    /// 重新验证并执行动作；只有 Provider 确认效果已经启动后才开始公共攻击恢复和独立冷却。
    /// </summary>
    public static CombatExecutionStatus Execute(
        CombatActionCandidate candidate,
        in CombatActionExecutionContext context)
    {
        if (candidate == null || context.Caster == null || context.Caster.Base.isRekt())
            return CombatExecutionStatus.Invalid;
        if (!CombatWorldService.IsActionReady(context.Caster.Base, candidate.Key))
            return CombatExecutionStatus.TemporarilyBlocked;

        CombatExecutionStatus result = candidate.Provider.TryExecute(candidate, context);
        if (result != CombatExecutionStatus.Started) return result;

        Actor actor = context.Caster.Base;
        bool hostileAction =
            (candidate.Profile.HasPurpose(CombatActionPurpose.Offense) ||
             candidate.Profile.HasPurpose(CombatActionPurpose.Control)) &&
            context.ActionTarget == context.PrimaryEnemy;
        if (hostileAction)
        {
            context.Caster.NotifyCombatActionStarted(context.ActionTarget);
            if (context.ActionTarget.isActor())
                CombatWorldService.RecordThreateningAction(actor, context.ActionTarget.a);
        }
        actor.startAttackCooldown();
        CombatWorldService.StartActionCooldown(actor, candidate.Key, candidate.Profile.Cooldown);
        return CombatExecutionStatus.Started;
    }

    internal static float ResolveResourceRatio(Actor actor, float mana, float stamina)
    {
        float manaRatio = mana <= 0f
            ? 0f
            : mana / Mathf.Max(1f, actor.getMaxMana());
        float staminaRatio = stamina <= 0f
            ? 0f
            : stamina / Mathf.Max(1f, actor.getMaxStamina());
        return Mathf.Clamp01(manaRatio + staminaRatio);
    }

    /// <summary>让同一原版战斗动作在自主规划和即时反应入口之间共享自身冷却。</summary>
    internal static CombatActionKey CreateOriginalActionKey(CombatActionAsset action)
    {
        return new CombatActionKey(
            OriginalActionProviderId,
            0,
            action?.id ?? string.Empty);
    }
}

/// <summary>把原版近战和武器弹丸接入统一动作协议。</summary>
internal sealed class PhysicalCombatActionProvider : ICombatActionProvider
{
    internal const string ProviderId = "core.physical";

    public string Id => ProviderId;

    public void Collect(in CombatActionCollectionContext context, IList<CombatActionCandidate> output)
    {
        Actor actor = context.Caster.Base;
        float range = Mathf.Max(0.5f, actor.getAttackRange());
        float power = Mathf.Max(1f, actor.stats[S.damage]) *
                      Mathf.Max(1f, actor.stats[S.targets]);
        float control = Mathf.Max(0f, actor.stats[S.knockback]) * 0.15f;

        if (actor.hasMeleeAttack())
        {
            output.Add(new CombatActionCandidate(
                this,
                new CombatActionKey(Id, 0, WorldboxGame.CombatActions.AttackMelee.id),
                new CombatActionProfile(
                    CombatActionPurpose.Offense,
                    ActiveAbilityTargetMode.Object,
                    null,
                    0f,
                    range,
                    range * 0.75f,
                    actor.stats[S.area_of_effect],
                    actor.stats[S.targets],
                    power,
                    control,
                    0f,
                    0f,
                    0f,
                    1f,
                    WorldboxGame.CombatActions.AttackMelee.rate,
                    CombatActionMovementMode.BriefStop),
                WorldboxGame.CombatActions.AttackMelee));
        }

        if (actor.hasRangeAttack())
        {
            output.Add(new CombatActionCandidate(
                this,
                new CombatActionKey(Id, 0, WorldboxGame.CombatActions.AttackRange.id),
                new CombatActionProfile(
                    CombatActionPurpose.Offense,
                    ActiveAbilityTargetMode.Object,
                    SkillImpactKind.Projectile,
                    1f,
                    range,
                    range * 0.82f,
                    actor.stats[S.area_of_effect],
                    actor.stats[S.projectiles],
                    power * Mathf.Max(1f, actor.stats[S.projectiles]),
                    control,
                    0f,
                    0f,
                    0f,
                    Mathf.Clamp01(actor.stats[S.accuracy] * 0.1f),
                    WorldboxGame.CombatActions.AttackRange.rate,
                    CombatActionMovementMode.Mobile),
                WorldboxGame.CombatActions.AttackRange));
        }
    }

    public CombatExecutionStatus TryExecute(
        CombatActionCandidate candidate,
        in CombatActionExecutionContext context)
    {
        Actor actor = context.Caster.Base;
        BaseSimObject target = context.ActionTarget;
        if (!actor.isAttackPossible() || !CombatWorldService.CanEngageTarget(actor, target))
            return CombatExecutionStatus.Invalid;

        var action = candidate.Payload as CombatActionAsset;
        if (action == null) return CombatExecutionStatus.Invalid;
        bool melee = action == WorldboxGame.CombatActions.AttackMelee;
        if (melee && target.position_height > 0f) return CombatExecutionStatus.Invalid;
        float range = candidate.Profile.MaxRange + target.stats[S.size];
        if (Toolbox.SquaredDistVec2Float(actor.current_position, target.current_position) > range * range)
            return CombatExecutionStatus.TemporarilyBlocked;

        AttackData data = CreateAttackData(
            actor,
            target,
            context.KillAction,
            context.BonusAreaEffect);
        if (action.action == null) return CombatExecutionStatus.Invalid;
        action.action(data);

        actor.punchTargetAnimation(target.current_position, true, actor.hasRangeAttack());
        FinishOriginalAction(actor, action, target.current_position);
        return CombatExecutionStatus.Started;
    }

    internal static AttackData CreateAttackData(
        Actor actor,
        BaseSimObject target,
        Action killAction,
        float bonusAreaEffect)
    {
        float targetSize = target.stats[S.size];
        Vector3 targetPosition = target.current_position;
        if (target.isActor() && target.a.is_moving && target.isFlying())
        {
            targetPosition = Vector3.MoveTowards(
                targetPosition,
                target.a.next_step_position,
                targetSize * 3f);
        }

        float distance = Vector2.Distance(actor.current_position, target.current_position) + target.getHeight();
        Vector3 hitPosition = Toolbox.getNewPoint(
            actor.current_position.x,
            actor.current_position.y,
            targetPosition.x,
            targetPosition.y,
            distance - targetSize,
            true);
        return new AttackData(
            actor,
            target.current_tile,
            hitPosition,
            actor.current_position,
            target,
            actor.kingdom,
            AttackType.Weapon,
            actor.haveMetallicWeapon(),
            true,
            actor.hasRangeAttack(),
            actor.getWeaponAsset().projectile,
            killAction,
            bonusAreaEffect);
    }

    internal static void FinishOriginalAction(
        Actor actor,
        CombatActionAsset action,
        Vector2? recoilTarget = null)
    {
        actor.spendStamina(action.cost_stamina);
        actor.spendMana(action.cost_mana);
        FinishOriginalFeedback(actor, action.play_unit_attack_sounds, recoilTarget);
    }

    /// <summary>补齐原版攻击入口在成功启动动作后统一执行的声音、饥饿和后坐力。</summary>
    internal static void FinishOriginalFeedback(
        Actor actor,
        bool playAttackSound,
        Vector2? recoilTarget = null)
    {
        if (playAttackSound && actor.asset.has_sound_attack)
        {
            MusicBox.playSound(actor.asset.sound_attack, actor.current_tile.x, actor.current_tile.y);
        }
        if (actor.needsFood() && Randy.randomBool()) actor.decreaseNutrition();
        float recoil = actor.stats["recoil"];
        if (recoilTarget.HasValue && recoil > 0f)
        {
            Vector2 target = recoilTarget.Value;
            actor.calculateForce(
                actor.current_position.x,
                actor.current_position.y,
                target.x,
                target.y,
                recoil);
        }
    }
}

/// <summary>枚举并执行角色实际拥有的每一个原版 SpellAsset，而不是再次随机抽取。</summary>
internal sealed class VanillaSpellCombatActionProvider : ICombatActionProvider
{
    private static readonly AccessTools.FieldRef<Actor, SpellHolder> SpellHolderRef =
        AccessTools.FieldRefAccess<Actor, SpellHolder>("_spells");
    internal const string ProviderId = "core.vanilla_spell";

    public string Id => ProviderId;

    public void Collect(in CombatActionCollectionContext context, IList<CombatActionCandidate> output)
    {
        Actor actor = context.Caster.Base;
        if (!actor.hasSpells() || !actor.canUseSpells()) return;

        using var spells = new ListPool<SpellAsset>();
        var unique = new HashSet<SpellAsset>();
        AddSpells(SpellHolderRef(actor), spells, unique);
        if (actor.hasSubspecies()) AddSpells(actor.subspecies.spells, spells, unique);
        if (actor.canUseReligionSpells()) AddSpells(actor.religion.spells, spells, unique);
        if (actor.asset.hasDefaultSpells()) AddSpells(actor.asset.spells, spells, unique);

        for (int i = 0; i < spells.Count; i++)
        {
            SpellAsset spell = spells[i];
            BaseSimObject target = spell.cast_target == CastTarget.Himself
                ? actor
                : context.PrimaryEnemy;
            if (!CanPrepare(actor, spell, target)) continue;

            bool self = spell.cast_target == CastTarget.Himself;
            float range = self ? 0f : context.Caster.GetSkillCastRange(context.PrimaryEnemy);
            float reliability = Mathf.Clamp01(
                spell.chance + spell.chance * actor.stats[S.skill_spell]);
            float power = 1f + spell.cost_mana * 0.25f;
            output.Add(new CombatActionCandidate(
                this,
                new CombatActionKey(Id, 0, spell.id),
                new CombatActionProfile(
                    self
                        ? CombatActionPurpose.Defense | CombatActionPurpose.Support
                        : CombatActionPurpose.Offense,
                    self ? ActiveAbilityTargetMode.Self : ActiveAbilityTargetMode.Object,
                    null,
                    spell.min_distance,
                    range,
                    self ? 0f : range * 0.7f,
                    0f,
                    1f,
                    self ? power * 0.25f : power,
                    0f,
                    self ? power : 0f,
                    CombatActionService.ResolveResourceRatio(
                        actor,
                        spell.cost_mana,
                        WorldboxGame.CombatActions.CastVanillaSpell.cost_stamina),
                    0f,
                    reliability,
                    Mathf.Max(1, Mathf.RoundToInt(
                        WorldboxGame.CombatActions.CastVanillaSpell.rate * reliability)),
                    CombatActionMovementMode.Mobile),
                spell));
        }
    }

    public CombatExecutionStatus TryExecute(
        CombatActionCandidate candidate,
        in CombatActionExecutionContext context)
    {
        var spell = candidate.Payload as SpellAsset;
        Actor actor = context.Caster.Base;
        BaseSimObject target = spell?.cast_target == CastTarget.Himself
            ? actor
            : context.ActionTarget;
        if (spell == null || !CanPrepare(actor, spell, target))
            return CombatExecutionStatus.Invalid;

        if (!IsInRange(actor, target, candidate.Profile.MinRange, candidate.Profile.MaxRange))
            return CombatExecutionStatus.TemporarilyBlocked;
        float chance = Mathf.Clamp01(spell.chance + spell.chance * actor.stats[S.skill_spell]);
        if (!Randy.randomChance(chance)) return CombatExecutionStatus.TemporarilyBlocked;

        bool started = spell.action != null &&
                       spell.action.RunAnyTrue(actor, target, target.current_tile);
        if (!started) return CombatExecutionStatus.TemporarilyBlocked;

        actor.spendMana(spell.cost_mana);
        actor.spendStamina(WorldboxGame.CombatActions.CastVanillaSpell.cost_stamina);
        actor.doCastAnimation();
        actor.addStatusEffect("recovery_spell");
        PhysicalCombatActionProvider.FinishOriginalFeedback(
            actor,
            WorldboxGame.CombatActions.CastVanillaSpell.play_unit_attack_sounds,
            target.current_position);
        return CombatExecutionStatus.Started;
    }

    private static void AddSpells(
        SpellHolder holder,
        ICollection<SpellAsset> output,
        ISet<SpellAsset> unique)
    {
        if (holder == null || !holder.hasAny()) return;
        IReadOnlyList<SpellAsset> source = holder.spells;
        for (int i = 0; i < source.Count; i++)
        {
            SpellAsset spell = source[i];
            if (spell != null && spell.can_be_used_in_combat && unique.Add(spell)) output.Add(spell);
        }
    }

    private static bool CanPrepare(Actor actor, SpellAsset spell, BaseSimObject target)
    {
        if (spell == null || !spell.can_be_used_in_combat || target.isRekt()) return false;
        if (!actor.canUseSpells() || !actor.hasEnoughMana(spell.cost_mana) ||
            !actor.hasEnoughStamina(WorldboxGame.CombatActions.CastVanillaSpell.cost_stamina)) return false;
        if (spell.cast_entity == CastEntity.BuildingsOnly && target.isActor()) return false;
        if (spell.cast_entity == CastEntity.UnitsOnly && target.isBuilding()) return false;
        if (spell.health_ratio > 0f && spell.health_ratio <= actor.getHealthRatio()) return false;
        return true;
    }

    private static bool IsInRange(Actor actor, BaseSimObject target, float minRange, float maxRange)
    {
        if (target == actor) return true;
        float distanceSquared = Toolbox.SquaredDistVec2Float(
            actor.current_position,
            target.current_position);
        float min = Mathf.Max(0f, minRange);
        float max = Mathf.Max(min, maxRange) + target.stats[S.size];
        return distanceSquared >= min * min && distanceSquared <= max * max;
    }
}

/// <summary>把 Skill、法器、卷轴和核心形成能力统一适配为战斗动作。</summary>
internal sealed class ActiveAbilityCombatActionProvider : ICombatActionProvider
{
    internal const string ProviderId = "core.active_ability";

    public string Id => ProviderId;

    public void Collect(in CombatActionCollectionContext context, IList<CombatActionCandidate> output)
    {
        ActorExtend caster = context.Caster;
        using var handles = new ListPool<ActiveAbilityHandle>();
        ActiveAbilityService.Collect(caster, handles);
        for (int i = 0; i < handles.Count; i++)
        {
            ActiveAbilityHandle handle = handles[i];
            if ((ActiveAbilityService.GetChannels(caster, handle) & ActiveAbilityChannel.Combat) == 0)
                continue;

            ActiveAbilityDescriptor descriptor = ActiveAbilityService.Describe(caster, handle);
            ActiveAbilityTacticalProfile tactical =
                ActiveAbilityService.ResolveTacticalProfile(caster, handle, context.PrimaryEnemy);
            CombatActionPurpose purpose = ResolvePurpose(tactical);
            BaseSimObject prepareTarget = ResolveTarget(
                caster.Base,
                context.PrimaryEnemy,
                context.PreferredAlly,
                descriptor.TargetMode,
                purpose);
            long preferredTargetId = 0;
            if (descriptor.TargetRelation == SkillUseTargetRelation.Friendly &&
                ActiveAbilityService.HasTargetAdvisor(handle))
            {
                if (!ActiveAbilityService.TryResolvePreferredTarget(
                        caster,
                        handle,
                        context.NearbyAllies,
                        out prepareTarget)) continue;
                preferredTargetId = prepareTarget.getID();
            }
            if (!ActiveAbilityService.CanPrepare(caster, handle, prepareTarget)) continue;

            float range = ActiveAbilityService.ResolveRange(caster, handle, prepareTarget);
            float radius = ActiveAbilityService.ResolveEffectRadius(caster, handle);
            long sourcePid = handle.Source.IsNull ? 0 : handle.Source.Pid;
            float resourceRatio = tactical.ResourceDemand /
                                  Mathf.Max(1f, tactical.ResourceDemand + 20f);
            output.Add(new CombatActionCandidate(
                this,
                new CombatActionKey(
                    handle.ProviderId,
                    sourcePid,
                    handle.EntryId),
                new CombatActionProfile(
                    purpose,
                    descriptor.TargetMode,
                    tactical.ImpactKind,
                    0f,
                    range,
                    range * 0.72f,
                    radius,
                    tactical.ExpectedTargets,
                    tactical.Power + tactical.Offensive,
                    tactical.Control,
                    Mathf.Max(
                        tactical.Utility,
                        Mathf.Max(tactical.Support, tactical.Defensive)),
                    resourceRatio,
                    0f,
                    1f,
                    ActiveAbilityService.ResolveAiWeight(caster, handle, prepareTarget),
                    ResolveMovementMode(descriptor.CastMobility)),
                handle,
                preferredTargetId: preferredTargetId));
        }
    }

    public CombatExecutionStatus TryExecute(
        CombatActionCandidate candidate,
        in CombatActionExecutionContext context)
    {
        if (candidate.Payload is not ActiveAbilityHandle handle)
            return CombatExecutionStatus.Invalid;

        BaseSimObject target = candidate.Profile.TargetMode == ActiveAbilityTargetMode.Self
            ? context.Caster.Base
            : context.ActionTarget;
        Vector3 position = target.isRekt() ? context.TargetPosition : target.GetSimPos();
        var abilityTarget = new ActiveAbilityTarget(
            target,
            position,
            attackKingdom: context.Caster.Base.kingdom);
        if (!ActiveAbilityService.CanUse(context.Caster, handle, abilityTarget))
            return target.isRekt()
                ? CombatExecutionStatus.Invalid
                : CombatExecutionStatus.TemporarilyBlocked;
        if (!ActiveAbilityService.TryUse(
                context.Caster,
                handle,
                abilityTarget,
                ActiveAbilityUseOrigin.Autonomous))
            return CombatExecutionStatus.TemporarilyBlocked;
        return CombatExecutionStatus.Started;
    }

    private static BaseSimObject ResolveTarget(
        Actor caster,
        BaseSimObject enemy,
        Actor ally,
        ActiveAbilityTargetMode targetMode,
        CombatActionPurpose purpose)
    {
        if (targetMode == ActiveAbilityTargetMode.Self) return caster;
        if ((purpose & (CombatActionPurpose.Defense | CombatActionPurpose.Support)) != 0 &&
            (purpose & CombatActionPurpose.Offense) == 0)
            return !ally.isRekt() ? ally : caster;
        return enemy;
    }

    private static CombatActionPurpose ResolvePurpose(ActiveAbilityTacticalProfile tactical)
    {
        CombatActionPurpose purpose = CombatActionPurpose.None;
        if (tactical.Offensive > 0f) purpose |= CombatActionPurpose.Offense;
        if (tactical.Defensive > 0f) purpose |= CombatActionPurpose.Defense;
        if (tactical.Support > 0f) purpose |= CombatActionPurpose.Support;
        if (tactical.Control > 0f) purpose |= CombatActionPurpose.Control;
        if (tactical.ImpactKind is SkillImpactKind.Wall or SkillImpactKind.Shield)
            purpose |= CombatActionPurpose.Barrier;
        if (tactical.ImpactKind == SkillImpactKind.Field)
            purpose |= CombatActionPurpose.Field;
        return purpose == CombatActionPurpose.None
            ? CombatActionPurpose.Offense
            : purpose;
    }

    /// <summary>将主动能力公开的施法移动约束映射为战术动作约束。</summary>
    private static CombatActionMovementMode ResolveMovementMode(
        ActiveAbilityCastMobility mobility)
    {
        return mobility switch
        {
            ActiveAbilityCastMobility.BriefStop => CombatActionMovementMode.BriefStop,
            ActiveAbilityCastMobility.StationaryDuringRecovery =>
                CombatActionMovementMode.StationaryDuringRecovery,
            _ => CombatActionMovementMode.Mobile
        };
    }
}

/// <summary>把原版战斗动作池中的冲刺、后撤、投掷等动作纳入独立冷却和统一选择。</summary>
internal sealed class AdvancedCombatActionProvider : ICombatActionProvider
{
    internal const string ProviderId = "core.advanced_action";

    public string Id => ProviderId;

    public void Collect(in CombatActionCollectionContext context, IList<CombatActionCandidate> output)
    {
        Actor actor = context.Caster.Base;
        var seen = new HashSet<CombatActionAsset>();
        AddPool(actor, context.PrimaryEnemy, CombatActionPool.BEFORE_ATTACK_MELEE, false, seen, output);
        if (actor.hasRangeAttack())
            AddPool(actor, context.PrimaryEnemy, CombatActionPool.BEFORE_ATTACK_RANGE, true, seen, output);
    }

    public CombatExecutionStatus TryExecute(
        CombatActionCandidate candidate,
        in CombatActionExecutionContext context)
    {
        if (candidate.Payload is not AdvancedActionPayload payload)
            return CombatExecutionStatus.Invalid;
        Actor actor = context.Caster.Base;
        BaseSimObject target = context.ActionTarget;
        CombatActionAsset action = payload.Action;
        if (target.isRekt() || action?.action_actor_target_position == null)
            return CombatExecutionStatus.Invalid;
        if (!actor.hasEnoughStamina(action.cost_stamina) ||
            !actor.hasEnoughMana(action.cost_mana))
            return CombatExecutionStatus.TemporarilyBlocked;
        if (action.can_do_action != null && !action.can_do_action(actor, target))
            return CombatExecutionStatus.TemporarilyBlocked;
        float chance = Mathf.Clamp01(action.chance + action.chance * actor.stats[S.skill_combat]);
        if (!Randy.randomChance(chance)) return CombatExecutionStatus.TemporarilyBlocked;

        bool started = action.action_actor_target_position(
            actor,
            target.current_position,
            target.current_tile);
        if (!started) return CombatExecutionStatus.TemporarilyBlocked;
        actor.spendStamina(action.cost_stamina);
        actor.spendMana(action.cost_mana);
        return CombatExecutionStatus.Started;
    }

    private void AddPool(
        Actor actor,
        BaseSimObject target,
        CombatActionPool pool,
        bool rangedPool,
        ISet<CombatActionAsset> seen,
        ICollection<CombatActionCandidate> output)
    {
        List<CombatActionAsset> actions = actor.getCombatActionPool(pool);
        if (actions == null) return;
        for (int i = 0; i < actions.Count; i++)
        {
            CombatActionAsset action = actions[i];
            if (action == null || action.action_actor_target_position == null || !seen.Add(action))
                continue;
            if (!actor.hasEnoughStamina(action.cost_stamina) ||
                !actor.hasEnoughMana(action.cost_mana))
                continue;
            if (!target.isRekt() && action.can_do_action != null &&
                !action.can_do_action(actor, target))
                continue;

            bool advance = action == CombatActionLibrary.combat_action_dash;
            bool escape = action == CombatActionLibrary.combat_action_backstep;
            bool mobility = advance || escape;
            CombatActionPurpose purpose = CombatActionPurpose.Offense;
            if (advance)
                purpose = CombatActionPurpose.Mobility | CombatActionPurpose.Advance;
            else if (escape)
                purpose = CombatActionPurpose.Mobility | CombatActionPurpose.Escape;
            float range = rangedPool
                ? Mathf.Max(actor.getAttackRange(), 8f)
                : Mathf.Max(actor.getAttackRange(), 5f);
            output.Add(new CombatActionCandidate(
                this,
                CombatActionService.CreateOriginalActionKey(action),
                new CombatActionProfile(
                    purpose,
                    ActiveAbilityTargetMode.Object,
                    null,
                    0f,
                    mobility ? 50f : range,
                    mobility ? range : range * 0.75f,
                    0f,
                    1f,
                    mobility ? 0f : 1f + action.cost_stamina * 0.1f,
                    0f,
                    mobility ? 1f : 0f,
                    CombatActionService.ResolveResourceRatio(
                        actor,
                        action.cost_mana,
                        action.cost_stamina),
                    action.cooldown,
                    Mathf.Clamp01(action.chance + action.chance * actor.stats[S.skill_combat]),
                    Mathf.Max(1, Mathf.RoundToInt(action.chance * 10f)),
                    mobility || rangedPool
                        ? CombatActionMovementMode.Mobile
                        : CombatActionMovementMode.BriefStop),
                new AdvancedActionPayload(action, rangedPool)));
        }
    }

    private sealed class AdvancedActionPayload
    {
        internal readonly CombatActionAsset Action;
        internal readonly bool RangedPool;

        internal AdvancedActionPayload(CombatActionAsset action, bool rangedPool)
        {
            Action = action;
            RangedPool = rangedPool;
        }
    }
}
