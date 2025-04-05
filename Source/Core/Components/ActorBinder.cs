using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

public struct ActorBinder(long id) : IComponent
{
    public long ID = id;
    [Ignore]
    public Actor Actor
    {
        get
        {
            if (_actor != null) return _actor;

            _actor = World.world.units.get(ID);

            return _actor;
        }
    }

    [Ignore]
    public   ActorExtend AE => _ae;
    [Ignore]
    internal ActorExtend _ae;
    [Ignore]
    private  Actor       _actor;

}