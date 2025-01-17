using Friflo.Engine.ECS;

namespace Cultiway.Abstract;

public interface IAsForce : IHasForce
{
    public Entity GetSelf();
}