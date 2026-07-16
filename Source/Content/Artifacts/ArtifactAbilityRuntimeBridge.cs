using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

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
        ArtifactIncomingDamageEvent evt = new()
        {
            Attacker = attacker,
            DamageComposition = damageComposition,
            AttackType = attackType,
            Damage = damage,
            IgnoreDamageReduction = ignoreDamageReduction,
            IsRetaliation = ArtifactDamageEffects.IsResolvingRetaliation,
        };
        ArtifactAbilityDispatcher.Dispatch(self.E, evt);
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
        ArtifactAbilityDispatcher.Dispatch(target.E, new ArtifactDamageTakenEvent
        {
            Attacker = attacker,
            Damage = damage,
            DamageComposition = composition,
            AttackType = attackType,
        });

        if (attacker == null || attacker.isRekt() || !attacker.isActor() || attacker.a == target.Base) return;
        ArtifactAbilityDispatcher.Dispatch(attacker.a.GetExtend().E, new ArtifactDamageDealtEvent
        {
            Target = target.Base,
            Damage = damage,
            DamageComposition = composition,
            AttackType = attackType,
        });
    }

    private static void Killed(ActorExtend killer, Actor victim, Kingdom victimKingdom)
    {
        ArtifactAbilityDispatcher.Dispatch(killer.E, new ArtifactKillEvent
        {
            Victim = victim,
            VictimKingdom = victimKingdom,
        });
    }

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
