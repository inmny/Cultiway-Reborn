using Cultiway.Core.SkillLibV2.Api;

namespace Cultiway.Core.SkillLibV2.Components.Triggers;

public struct EntityCollisionContext : IEventContext
{
    public bool JustTriggered { get; set; }
}