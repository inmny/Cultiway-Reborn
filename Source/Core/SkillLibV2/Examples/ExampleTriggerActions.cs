using Cultiway.Core.SkillLibV2.Components.Triggers;

namespace Cultiway.Core.SkillLibV2.Examples;

public static class ExampleTriggerActions
{
    public static TriggerActionMeta<TimeIntervalTrigger, TimeIntervalContext> TimeIntervalSpawnFireball { get; }

    public static TriggerActionMeta<ObjCollisionTrigger, ObjCollisionContext> ObjCollisionDamageAndExplosion { get; }

    public static void Init()
    {
    }
}