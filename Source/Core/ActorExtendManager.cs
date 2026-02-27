using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.services;

namespace Cultiway.Core;

public class ActorExtendManager : ExtendComponentManager<ActorExtend>
{
    public readonly EntityStore                     World;
    private object _lock = new();
    private ConditionalWeakTable<ActorData, ActorExtend> _actor_to_extend = new();

    internal ActorExtendManager(EntityStore world)
    {
        World = world;
    }
    public ActorExtend Get(Actor actor)
    {
        lock(_lock)
        {
            if (_actor_to_extend.TryGetValue(actor.data, out var val))
            {
                // 检查ActorBinder.ID是否匹配，防止ActorData被重用导致的问题
                ref var binder = ref val.E.GetComponent<ActorBinder>();
                if (binder.ID != actor.data.id)
                {
                    ModClass.LogWarning($"ActorExtend for Actor {actor.data.id} ({val.E}) has mismatched ID {binder.ID}, expected {actor.data.id}.");
                    LogService.LogStackTraceAsWarning();
                }
            }
            else
            {
                val = new ActorExtend(World.CreateEntity(new ActorBinder(actor.data.id)));
                ModClass.LogInfo($"Creating ActorExtend for Actor {actor.data.id} ({val.E.GetComponent<ActorBinder>().ID}) ({val.E})");
                _actor_to_extend.Add(actor.data, val);
            }
            if (val.Base == null)
            {
                ModClass.LogInfo($"ActorExtend for Actor {actor.data.id} ({val.E.GetComponent<ActorBinder>().ID}) ({val.E}) has null Base, this should not happen.");
            }
            return val;
        }
    }
    public bool Has(Actor actor)
    {
        return _actor_to_extend.TryGetValue(actor.data, out var val);
    }
    public void Remove(Actor actor)
    {
        if (_actor_to_extend.TryGetValue(actor.data, out var val))
        {
            _actor_to_extend.Remove(actor.data);
        }
    }
    public void Clear()
    {
        _actor_to_extend = new ConditionalWeakTable<ActorData, ActorExtend>();
    }
    public void AllStatsDirty()
    {
        World.Query<ActorBinder>().ForEachEntity((ref ActorBinder ab, Entity e) => ab.Actor?.setStatsDirty());
    }
}