using Cultiway.Content.Components;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Combat;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Artifacts;

/// <summary>
/// 将角色核心事件和属性缓存接到 Content 法器能力，不令 Core 反向依赖具体法器内容。
/// </summary>
internal static class ArtifactAbilityRuntimeBridge
{
    internal static void Init()
    {
        ActorExtend.RegisterCachedStatsBuilder(ArtifactAbilityLifecycle.ContributeStats);
        ActorExtend.RegisterActionBeforeBeAttacked(BeforeBeAttacked);
        ActorExtend.RegisterActionOnDamageResolved(DamageResolved);
        ActorExtend.HealthLossConfirmed += HealthLost;
        ActorExtend.RegisterActionOnKill(Killed);
        ActorExtend.RegisterActionOnSkillCastCompleted(SkillCastCompleted);
        ActorExtend.RegisterActionOnDeath(actor => ArtifactAbilityLifecycle.InterruptController(actor.E));
    }

    private static void BeforeBeAttacked(
        ActorExtend self,
        BaseSimObject attacker,
        ref ElementComposition damageComposition,
        ref AttackType attackType,
        ref float damage,
        ref bool ignoreDamageReduction)
    {
        SkillCasterContext context = SkillCasterContextService.Resolve(self);
        if (context.IsValid && context.Kind == SkillCarrierKind.Soul && damageComposition.neg <= 0f) return;
        ActorExtend controller = context.IsValid ? context.Owner : self;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(in context);
        ArtifactIncomingDamageEvent evt = new()
        {
            Target = self,
            ContactPoint = ShieldAbsorptionScope.ContactFor(self),
            Attacker = attacker,
            DamageComposition = damageComposition,
            AttackType = attackType,
            Damage = damage,
            IgnoreDamageReduction = ignoreDamageReduction,
            IsRetaliation = CombatDamageEffects.IsResolvingReaction,
        };
        ArtifactAbilityDispatcher.Dispatch(controller.E, evt);
        damageComposition = evt.DamageComposition;
        attackType = evt.AttackType;
        damage = evt.Damage;
        ignoreDamageReduction = evt.IgnoreDamageReduction;
    }

    private static void DamageResolved(
        ActorExtend target,
        BaseSimObject attacker,
        float damage,
        ElementComposition composition,
        AttackType attackType)
    {
        SkillCasterContext targetContext = SkillCasterContextService.Resolve(target);
        ActorExtend targetController = targetContext.IsValid ? targetContext.Owner : target;
        using (SkillCasterContextService.Enter(in targetContext))
        {
            ArtifactAbilityDispatcher.Dispatch(targetController.E, new ArtifactDamageTakenEvent
            {
                Attacker = attacker,
                Damage = damage,
                DamageComposition = composition,
                AttackType = attackType,
            });
        }

        ActorExtend attackerController = AttackOwner(target.Base, attacker, out SkillCasterContext attackerContext);
        if (attackerController == null) return;
        using SkillCasterContextService.Scope attackerScope =
            SkillCasterContextService.Enter(in attackerContext);
        ArtifactAbilityDispatcher.Dispatch(attackerController.E, new ArtifactDamageDealtEvent
        {
            Target = target.Base,
            Damage = damage,
            DamageComposition = composition,
            AttackType = attackType,
        });
    }

    private static void HealthLost(Actor target, BaseSimObject attacker, int lost, Vector3? contactPoint)
    {
        ActorExtend owner = AttackOwner(target, attacker, out SkillCasterContext context);
        if (owner == null || owner.E.GetRelations<EquippedArtifactRelation>().Length == 0) return;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(in context);
        ArtifactAbilityDispatcher.Dispatch(owner.E, new ArtifactHealthDamageEvent
        {
            Target = target,
            Attacker = attacker,
            HealthLost = lost,
            ContactPoint = contactPoint,
        });
    }

    private static ActorExtend AttackOwner(Actor target, BaseSimObject attacker, out SkillCasterContext context)
    {
        context = default;
        if (attacker == null || attacker.isRekt() || !attacker.isActor() || attacker.a == target) return null;
        ActorExtend requester = attacker.a.GetExtend();
        context = SkillCasterContextService.Resolve(requester);
        return context.IsValid ? context.Owner : requester;
    }

    private static void Killed(ActorExtend killer, Actor victim, Kingdom victimKingdom)
    {
        if (killer == null) return;
        SkillCasterContext context = SkillCasterContextService.Resolve(killer);
        ActorExtend controller = context.IsValid ? context.Owner : killer;
        using SkillCasterContextService.Scope scope = SkillCasterContextService.Enter(in context);
        ArtifactAbilityDispatcher.Dispatch(controller.E, new ArtifactKillEvent
        {
            Victim = victim,
            VictimKingdom = victimKingdom,
        });
    }

    /// <summary>一个构造只有一次共同启动，内部部位和后续接触不增加发射计数。</summary>
    internal static void ConstructedCastCommitted(ActorExtend payer) =>
        SkillCastCompleted(payer, default, 1, SkillCastFundingSource.CasterResources);

    private static void SkillCastCompleted(
        ActorExtend caster,
        Entity skillContainer,
        int emittedCount,
        SkillCastFundingSource fundingSource)
    {
        ArtifactAbilityDispatcher.Dispatch(caster.E, new ArtifactSkillCastEvent
        {
            SkillContainer = skillContainer,
            EmittedCount = emittedCount,
            FundingSource = fundingSource,
        });
    }
}
