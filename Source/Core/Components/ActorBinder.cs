using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct ActorBinder(string id) : IComponent
{
    public readonly string id = id;

    public Actor Actor
    {
        get
        {
            if (_actor != null) return _actor;

            _actor = World.world.units.get(id);

            return _actor;
        }
    }

    public   ActorExtend AE => _ae;
    internal ActorExtend _ae;
    private  Actor       _actor;
}