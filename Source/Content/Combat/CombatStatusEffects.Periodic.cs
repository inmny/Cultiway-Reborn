using Cultiway.Content.Skills;
using Cultiway.Core.Combat;
using Cultiway.Core.Components;
using Cultiway.Core.EventSystem;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Libraries;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Combat;

public static partial class CombatStatusEffects
{
    /// <summary>按原伤害类别提交每次状态伤害。回调在状态登记时建立一次。</summary>
    /// <param name="attackType">交给共同受击规则的实际类别。</param>
    internal static StatusTickAction DamageTick(AttackType attackType) =>
        (status, interval) => ApplyPeriod(status, interval, attackType);

    /// <summary>回春用状态记录的每秒量恢复原持有者。</summary>
    /// <param name="status">当前周期到期的状态。</param>
    /// <param name="interval">该状态原有的周期秒数。</param>
    internal static void RestorationTick(Entity status, float interval) => ApplyPeriod(status, interval, null);

    /// <summary>只作用于本次仍然持有状态的活体，伤害和回春用各自的共同结算。</summary>
    /// <param name="status">当前仍有效的状态。</param>
    /// <param name="interval">实际周期秒数。</param>
    /// <param name="attackType">伤害类别，回春时为空。</param>
    private static void ApplyPeriod(Entity status, float interval, AttackType? attackType)
    {
        StatusTickState tick = status.GetComponent<StatusTickState>();
        float amount = tick.Value * interval;
        if (amount <= 0f) return;

        GetHitEvent damage = default;
        if (attackType.HasValue)
        {
            StatusComponent effect = status.GetComponent<StatusComponent>();
            BaseSimObject source = effect.Source;
            if (source.isRekt() || source is Actor actual && actual.data?.id != effect.SourceActorId)
                source = null;

            // 强度和境界来自施加时保存的状态，续跳不再套用施法载体此刻的倍率。
            damage = new GetHitEvent
            {
                Damage = amount, Element = tick.Element, AttackType = attackType.Value,
                AttackerPowerLevel = effect.SourcePowerLevel ?? (source as Actor)?.GetExtend().GetPowerLevel() ?? 0f,
                DamageOrigin = DamageOrigin.Reaction, SourceScopeId = DamageResolutionContext.CurrentSourceScopeId
            };
            damage.BindAttacker(source);
        }

        foreach (Entity owner in status.GetIncomingLinks<StatusRelation>().Entities)
        {
            if (!owner.TryGetComponent(out ActorBinder binder)) continue;
            Actor target = binder.Actor;
            if (target == null || !target.isAlive() || !target.hasHealth()) continue;

            if (attackType.HasValue)
            {
                damage.TargetID = target.id;
                EventSystemHub.Publish(damage);
            }
            else ConstructedAbilityService.RestoreLivingActor(target, amount);
        }
    }
}
