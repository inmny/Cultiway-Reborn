using System.Collections.Concurrent;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.services;
using NeoModLoader.utils;

namespace Cultiway.Core;

public class ActorExtendManager : ExtendComponentManager<ActorExtend>
{
    public readonly EntityStore World;
    private readonly ConcurrentDictionary<ActorData, ActorExtend> _actor_to_extend = new();
    private readonly ConcurrentDictionary<ActorData, string> _actor_to_create_stacktrace = new();

    internal ActorExtendManager(EntityStore world)
    {
        World = world;
    }

    public ActorExtend Get(Actor actor)
    {
        var actorData = actor.data;
        var actorId = actorData.id;

        // 所有对EntityStore的访问都需要在锁的保护下进行
        lock (EntityStoreLock.GlobalLock)
        {
            if (_actor_to_extend.TryGetValue(actorData, out var val))
            {
                ref var binder = ref val.E.GetComponent<ActorBinder>();
                if (binder.ID == actorId)
                {
                    return val;
                }

                ModClass.LogWarning($"ActorBinder错位 {actorData.GetHashCode()} -> {val.GetHashCode()}, {binder._ae.GetHashCode()}. Actor {actorId} ({val.E}) Binder: {binder.ID}, Binder actor: {binder._actor?.id}");
                LogService.LogStackTraceAsWarning();

                LogService.LogWarning($"错位的ActorBinder创建于：\n{(_actor_to_create_stacktrace.TryGetValue(actorData, out var stacktrace) ? stacktrace : "未知")}");
                return val;
            }

            // 创建新的ActorExtend
            var newExtend = new ActorExtend(World.CreateEntity(new ActorBinder(actorId)));

            //ModClass.LogInfo($"Creating ActorExtend for Actor {actorId} ({newExtend.E}) binder id: {newExtend.E.GetComponent<ActorBinder>().Actor.id}. {actorData.GetHashCode()} -> {newExtend.GetHashCode()}");
            //_actor_to_create_stacktrace[actorData] = OtherUtils.GetStackTrace(1);
            _actor_to_extend[actorData] = newExtend;
            return newExtend;
        }
    }

    public bool Has(Actor actor)
    {
        return _actor_to_extend.TryGetValue(actor.data, out var val);
    }

    public void Remove(Actor actor)
    {
        _actor_to_extend.TryRemove(actor.data, out _);
        //_actor_to_create_stacktrace.TryRemove(actor.data, out _);
    }

    public void Clear()
    {
        lock (EntityStoreLock.GlobalLock)
        {
            _actor_to_extend.Clear();
            //_actor_to_create_stacktrace.Clear();
        }
    }

    public void AllStatsDirty()
    {
        World.Query<ActorBinder>().ForEachEntity((ref ActorBinder ab, Entity e) => ab.Actor?.setStatsDirty());
    }
}