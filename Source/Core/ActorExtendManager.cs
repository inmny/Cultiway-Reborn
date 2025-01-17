using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class ActorExtendManager : ExtendComponentManager<ActorExtend>
{
    public readonly EntityStore                     World;
    private         Dictionary<string, ActorExtend> _data = new();

    internal ActorExtendManager(EntityStore world)
    {
        World = world;
    }
    [MethodImpl(MethodImplOptions.Synchronized)]
    public ActorExtend Get(string id, bool new_when_null = false)
    {
        if (!_data.TryGetValue(id, out var val) && new_when_null)
        {
            val = new ActorExtend(World.CreateEntity(new ActorBinder(id)));
            _data[id] = val;
        }

        return val;
    }

    internal void Destroy(string id)
    {
        if (!_data.Remove(id, out var val)) return;
        val.PrepareDestroy();
    }

    public void AllStatsDirty()
    {
        World.Query<ActorBinder>().ForEachEntity((ref ActorBinder ab, Entity e) => ab.Actor?.setStatsDirty());
    }
}