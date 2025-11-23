using Friflo.Engine.ECS;

namespace Cultiway.Core.EventSystem.Events;

public struct EntityNameGeneratedEvent
{
    public Entity Target;
    public string Name;
}
