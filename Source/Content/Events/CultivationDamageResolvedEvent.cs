namespace Cultiway.Content.Events;

/// <summary>从并行伤害结算线程送往主逻辑线程的不可变战斗修炼快照。</summary>
public readonly struct CultivationDamageResolvedEvent
{
    /// <summary>冻结本次实际伤害及双方战力。</summary>
    public CultivationDamageResolvedEvent(
        long attackerId,
        long targetId,
        float actualDamage,
        float attackerPower,
        float targetPower,
        float targetMaxHealth)
    {
        AttackerId = attackerId;
        TargetId = targetId;
        ActualDamage = actualDamage;
        AttackerPower = attackerPower;
        TargetPower = targetPower;
        TargetMaxHealth = targetMaxHealth;
    }

    public long AttackerId { get; }
    public long TargetId { get; }
    public float ActualDamage { get; }
    public float AttackerPower { get; }
    public float TargetPower { get; }
    public float TargetMaxHealth { get; }
}
