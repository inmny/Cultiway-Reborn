using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct ObjCollisionContext : IEventContext
{
    public BaseSimObject obj;
    public bool          JustTriggered { get; set; }
}