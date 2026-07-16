using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;

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

    /// <summary>
    /// 本次伤害是否由受击反应产生。反射、反击和伤害转移能力应据此截断递归反应链，
    /// 普通护盾与减伤仍可照常处理这类伤害。
    /// </summary>
    public bool IsRetaliation;
}

/// <summary>一次已经完成常规减伤、即将真正扣除生命的伤害。</summary>
public sealed class ArtifactDamageTakenEvent
{
    public BaseSimObject Attacker;
    public float Damage;
    public ElementComposition DamageComposition;
    public AttackType AttackType;
}

/// <summary>驾驭者对其他对象造成的一次最终伤害。</summary>
public sealed class ArtifactDamageDealtEvent
{
    public BaseSimObject Target;
    public float Damage;
    public ElementComposition DamageComposition;
    public AttackType AttackType;
}

/// <summary>驾驭者完成一次击杀。能力可以据此充能、成长或产生后续效果。</summary>
public sealed class ArtifactKillEvent
{
    public Actor Victim;
    public Kingdom VictimKingdom;
}

/// <summary>驾驭者完成一次 SkillLibV3 施放序列。</summary>
public sealed class ArtifactSkillCastEvent
{
    public Entity SkillContainer;
    public int EmittedCount;
    public SkillCastFundingSource FundingSource;
}
