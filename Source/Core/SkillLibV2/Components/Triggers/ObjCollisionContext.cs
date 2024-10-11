using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Components.Triggers;

public struct ObjCollisionContext : IEventContext
{
    public BaseSimObject obj;
    public float         dist;
    public bool          JustTriggered { get; set; }
}