using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV2.Api;

public interface IEventContext : IComponent
{
    public bool JustTriggered { get; set; }
}