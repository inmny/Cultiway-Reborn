using Cultiway.Core;

namespace Cultiway.Content.Events;

/// <summary>
/// 角色正式结算伤害减免前发布的可变法器事件。
/// 防护、转移、反射和免伤类能力可以依次修改同一份伤害上下文。
/// </summary>
public sealed class ArtifactIncomingDamageEvent
{
    /// <summary>本次伤害的来源；环境伤害时可以为空。</summary>
    public BaseSimObject Attacker;

    /// <summary>本次伤害的元素构成。</summary>
    public ElementComposition DamageComposition;

    /// <summary>本次伤害的攻击类型。</summary>
    public AttackType AttackType;

    /// <summary>进入角色常规减伤流程前的伤害值。</summary>
    public float Damage;

    /// <summary>是否跳过角色常规伤害减免。</summary>
    public bool IgnoreDamageReduction;
}
