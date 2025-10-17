using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct EventNameEntity : IComponent
{
    public Entity Target;
    public string Name;
}