using Cultiway.Core;

namespace Cultiway.Core.EventSystem.Events;

public struct GetHitEvent
{
    public long TargetID;
    public float Damage;
    public ElementComposition Element;
    public BaseSimObject Attacker;
    public bool IgnoreDamageReduction;
}

