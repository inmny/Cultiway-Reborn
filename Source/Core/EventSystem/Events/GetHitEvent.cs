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

    /// <summary>本次伤害是否属于不应再次触发同类被动的二次反应。</summary>
    public DamageOrigin DamageOrigin;
}
