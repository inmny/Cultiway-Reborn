using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class ActorExtendManager : ExtendComponentManager<ActorExtend>
{
    public readonly EntityStore                     World;
    private ConditionalWeakTable<Actor, ActorExtend> _actor_to_extend = new();

    internal ActorExtendManager(EntityStore world)
    {
        World = world;
    }
    public ActorExtend Get(Actor actor)
    {
        if (_actor_to_extend.TryGetValue(actor, out var val)) return val;
        val = new ActorExtend(World.CreateEntity(new ActorBinder(actor.data.id)));
        _actor_to_extend.Add(actor, val);
        return val;
    }

    public bool Has(Actor actor)
    {
        return _actor_to_extend.TryGetValue(actor, out _);
    }

    public void Remove(Actor actor)
    {
        _actor_to_extend.Remove(actor);
    }
    public void AllStatsDirty()
    {
        World.Query<ActorBinder>().ForEachEntity((ref ActorBinder ab, Entity e) => ab.Actor?.setStatsDirty());
    }
}