using Cultiway.Core;
using Cultiway.Core.Combat;

namespace Cultiway.Core.EventSystem.Events;

public struct GetHitEvent
{
    public long TargetID;
    public float Damage;
    public ElementComposition Element;
    public long AttackerID;
    public bool AttackerIsActor;
    public bool HasAttacker;
    public float? AttackerPowerLevel;
    public bool IgnoreDamageReduction;

    /// <summary>原版或模组明确写入的伤害类别；未指定时沿用枚举默认值。</summary>
    public AttackType AttackType;

    /// <summary>本次伤害是否属于不应再次触发同类被动的二次反应。</summary>
    public DamageOrigin DamageOrigin;

    /// <summary>伤害入队时所在的外部来源作用域；零表示没有需要跨事件保留的来源。</summary>
    public long SourceScopeId;

    /// <summary>在事件边界只保存攻击源身份，不持有可能被回收的世界对象。</summary>
    public void BindAttacker(BaseSimObject attacker, long? stableID = null)
    {
        if (attacker == null) return;
        BaseObjectData data = attacker.getData();
        long attackerID = stableID ?? data?.id ?? LongExtension.NULL;
        if (!attackerID.hasValue()) return;
        AttackerID = attackerID;
        AttackerIsActor = attacker.isActor();
        HasAttacker = true;
    }
}
