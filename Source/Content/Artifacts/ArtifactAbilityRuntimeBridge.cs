using Cultiway.Content.Events;
using Cultiway.Core;

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
        };
        ArtifactAbilityDispatcher.Dispatch(self.E, evt);
        damageComposition = evt.DamageComposition;
        attackType = evt.AttackType;
        damage = evt.Damage;
        ignoreDamageReduction = evt.IgnoreDamageReduction;
    }
}
