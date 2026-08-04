using Cultiway.Core;
using Cultiway.Core.Combat;

namespace Cultiway.Core.EventSystem.Events;

public struct GetHitEvent
{
    public long TargetID;
    public float Damage;
    public ElementComposition Element;
    public BaseSimObject Attacker;
    public float? AttackerPowerLevel;
    public bool IgnoreDamageReduction;

    /// <summary>原版伤害类别，仅 PatchActor.getHit_prefix 路径会写入真实值，用于查无来源伤害等级。</summary>
    public AttackType AttackType;

    /// <summary>本次伤害是否属于不应再次触发同类被动的二次反应。</summary>
    public DamageOrigin DamageOrigin;

    /// <summary>伤害入队时所在的外部来源作用域；零表示没有需要跨事件保留的来源。</summary>
    public long SourceScopeId;
}
