using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Predefined.Triggers;

public struct StartSkillContext : IEventContext
{
    public ActorExtend   user;
    public BaseSimObject target;
    public float strength;
    public bool          JustTriggered { get; set; }
}