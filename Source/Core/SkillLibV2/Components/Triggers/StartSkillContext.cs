using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Components.Triggers;

public struct StartSkillContext : IEventContext
{
    public ActorExtend   user;
    public BaseSimObject target;
    public bool          JustTriggered { get; set; }
}