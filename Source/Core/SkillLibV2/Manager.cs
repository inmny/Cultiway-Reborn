using System;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Systems;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV2;

public class Manager
{
    private readonly SystemRoot    _logic;
    private readonly SystemRoot    _observer_logic;
    private          EntityStore[] _observer_worlds;
    private readonly SystemRoot    _render;

    internal Manager()
    {
        World = new EntityStore();
        _logic = new SystemRoot(World,          "SkillLibV2.Logic");
        _observer_logic = new SystemRoot(World, "SkillLibV2.Logic.Observer");
        _render = new SystemRoot(World,         "SkillLibV2.Render");

        _observer_worlds = [];

        _logic.Add(new LogicTriggerStartSkillSystem());
        _logic.Add(new LogicTriggerTimeIntervalSystem());
        _logic.Add(new LogicTriggerObjCollisionSystem(World));
        _logic.Add(new LogicSkillEntityAliveTimeSystem());
        _logic.Add(new LogicRecycleSkillEntitySystem());
        _logic.Add(new LogicTrajectorySystem(World));
        _logic.Add(new LogicAnimFrameUpdateSystem(World));
        _logic.Add(_observer_logic);

        _observer_logic.Add(new LogicObserverUnuseCheckSystem());
        _observer_logic.Add(new LogicObserverReuseCheckSystem());
        _observer_logic.Add(new LogicObserverRecycleTimerSystem());
        _observer_logic.Add(new LogicObserverRecycleSystem());

        _render.Add(new RenderAnimFrameSystem(World));
    }

    public EntityStore World { get; }

    internal void UpdateLogic(UpdateTick update_tick)
    {
        _logic.Update(update_tick);
    }

    public void NewStartSkill(string id, ActorExtend user, BaseSimObject init_target)
    {
        throw new NotImplementedException();
    }

    public ref TObserver GetObserver<TObserver>(int observer_code)
        where TObserver : struct, IEventObserverBase
    {
        EntityStore world = GetObserverWorld<TObserver>();
        if (world.TryGetEntityById(observer_code, out Entity e)) return ref e.GetComponent<TObserver>();

        var observer = new TObserver();
        observer.Setup(observer_code);
        e = world.CreateEntity(observer_code);
        e.AddComponent(observer);

        return ref e.GetComponent<TObserver>();
    }

    private EntityStore GetObserverWorld<TObserver>()
    {
        var world_idx = TypeIndex<ObserverWorldType, TObserver>.Index;
        if (world_idx > _observer_worlds.Length)
        {
            var new_array = new EntityStore[world_idx + 1];
            _observer_worlds.CopyTo(new_array, 0);
            _observer_worlds = new_array;

            var new_world = new EntityStore();
            _observer_worlds[world_idx] = new_world;
            _observer_logic.AddStore(new_world);
        }

        return _observer_worlds[world_idx];
    }

    internal void UpdateRender(UpdateTick update_tick)
    {
        _render.Update(update_tick);
    }

    private record ObserverWorldType
    {
    }
}