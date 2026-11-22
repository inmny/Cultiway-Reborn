using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Events;

/// <summary>
/// 角色正式结算伤害减免前发布的可变法器事件。
/// 防护、转移、反射和免伤类能力可以依次修改同一份伤害上下文。
/// </summary>
public sealed class ArtifactIncomingDamageEvent
{
    /// <summary>这次受击的实际人物，可能与法宝控制者不同。</summary>
    internal ActorExtend Target;

    /// <summary>这次伤害已确认的位置，未提供物理接触的原版伤害为空。</summary>
    internal Vector3? ContactPoint;
    internal bool UsesDefenseSurface;

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

/// <summary>一次已经真实扣除生命的攻击，包含致死伤害。</summary>
internal sealed class ArtifactHealthDamageEvent
{
    internal Actor Target;
    internal BaseSimObject Attacker;
    internal int HealthLost;
    internal Vector3? ContactPoint;
}

/// <summary>驾驭者完成一次击杀。能力可以据此充能、成长或产生后续效果。</summary>
public sealed class ArtifactKillEvent
{
    public Actor Victim;
    public Kingdom VictimKingdom;
}

/// <summary>驾驭者完成旧施放序列，或自行支付并成功启动一次构造。</summary>
public sealed class ArtifactSkillCastEvent
{
    public Entity SkillContainer;
    public int EmittedCount;
    public SkillCastFundingSource FundingSource;
}
