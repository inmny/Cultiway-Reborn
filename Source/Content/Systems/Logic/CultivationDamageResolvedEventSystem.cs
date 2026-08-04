using System.Collections.Generic;
using Cultiway.Content.Events;
using Cultiway.Content.Libraries;
using Cultiway.Core.EventSystem;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Systems.Logic;

/// <summary>在主逻辑线程把最终伤害事件分别分派给攻击者和承伤者。</summary>
public sealed class CultivationDamageResolvedEventSystem
    : GenericEventSystem<CultivationDamageResolvedEvent>
{
    private readonly Dictionary<
        (long attackerId, long targetId, float attackerPower, float targetPower, float targetMaxHealth),
        DamageAccumulator> accumulated = new();

    protected override int MaxEventsPerUpdate => 8192;

    /// <summary>同一逻辑帧内同一对手组合的多段伤害先合并，避免多段技能反复写角色数据。</summary>
    protected override void HandleEvents(IReadOnlyList<CultivationDamageResolvedEvent> events)
    {
        accumulated.Clear();
        for (var i = 0; i < events.Count; i++)
        {
            CultivationDamageResolvedEvent evt = events[i];
            var key = (evt.AttackerId, evt.TargetId, evt.AttackerPower, evt.TargetPower, evt.TargetMaxHealth);
            if (accumulated.TryGetValue(key, out DamageAccumulator value))
            {
                value.ActualDamage += evt.ActualDamage;
                accumulated[key] = value;
            }
            else
            {
                accumulated.Add(key, new DamageAccumulator(evt));
            }
        }

        foreach (DamageAccumulator value in accumulated.Values)
        {
            try
            {
                HandleEvent(value.ToEvent());
            }
            catch (System.Exception exception)
            {
                ModClass.LogErrorConcurrent(exception.ToString());
            }
        }
    }

    protected override void HandleEvent(CultivationDamageResolvedEvent evt)
    {
        Actor attacker = World.world.units.get(evt.AttackerId);
        if (attacker != null && attacker.isAlive() && attacker.TryGetExtend(out var attackerExtend))
        {
            var context = new CultivationTriggerContext(
                attackerExtend,
                CultivationTriggerKind.DamageDealt,
                actualDamage: evt.ActualDamage,
                practitionerPower: evt.AttackerPower,
                opponentPower: evt.TargetPower,
                referenceMaxHealth: evt.TargetMaxHealth);
            CultivateMethods.TryDispatch(in context);
        }

        Actor target = World.world.units.get(evt.TargetId);
        if (target == null || !target.isAlive() || !target.TryGetExtend(out var targetExtend)) return;
        var takenContext = new CultivationTriggerContext(
            targetExtend,
            CultivationTriggerKind.DamageTaken,
            actualDamage: evt.ActualDamage,
            practitionerPower: evt.TargetPower,
            opponentPower: evt.AttackerPower,
            referenceMaxHealth: evt.TargetMaxHealth);
        CultivateMethods.TryDispatch(in takenContext);
    }

    private struct DamageAccumulator
    {
        public readonly long AttackerId;
        public readonly long TargetId;
        public readonly float AttackerPower;
        public readonly float TargetPower;
        public readonly float TargetMaxHealth;
        public float ActualDamage;

        public DamageAccumulator(CultivationDamageResolvedEvent evt)
        {
            AttackerId = evt.AttackerId;
            TargetId = evt.TargetId;
            AttackerPower = evt.AttackerPower;
            TargetPower = evt.TargetPower;
            TargetMaxHealth = evt.TargetMaxHealth;
            ActualDamage = evt.ActualDamage;
        }

        public CultivationDamageResolvedEvent ToEvent()
        {
            return new CultivationDamageResolvedEvent(
                AttackerId,
                TargetId,
                ActualDamage,
                AttackerPower,
                TargetPower,
                TargetMaxHealth);
        }
    }
}
