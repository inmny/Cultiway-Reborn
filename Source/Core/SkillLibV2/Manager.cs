using System;
using System.Collections.Generic;
using System.Text;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Predefined;
using Cultiway.Core.SkillLibV2.Predefined.Triggers;
using Cultiway.Core.SkillLibV2.Systems;
using Cultiway.Core.Systems.Logic;
using Cultiway.Core.Systems.Render;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Core.SkillLibV2;

public class Manager
{
    private readonly SystemRoot _logic;
    private readonly SystemRoot _observer_logic;

    private readonly HashSet<string> _registered_custom_value_reach_systems = new();
    private readonly SystemRoot      _render;
    private readonly SystemGroup     _trigger_logic;
    private          EntityStore[]   _observer_worlds;

    internal Manager(WorldboxGame game)
    {
        Game = game;
        World = new EntityStore();
        _logic = new SystemRoot(World, "SkillLibV2.Logic");
        _observer_logic = new SystemRoot("SkillLibV2.Logic.Observer");
        _trigger_logic = new SystemGroup("SkillLibV2.Logic.Trigger");
        _render = new SystemRoot(World, "SkillLibV2.Render");

        _observer_worlds = [];

        
        _logic.Add(new AliveTimerSystem());
        _logic.Add(new AliveTimerCheckSystem());
        _logic.Add(new LogicCheckCasterSystem());
        
        _logic.Add(new RecycleAnimRendererSystem());
        _logic.Add(new RecycleDefaultEntitySystem());

        _logic.Add(new LogicTrajectorySystem(World));
        _logic.Add(new AnimFrameUpdateSystem(World));
        _logic.Add(_trigger_logic);
        _trigger_logic.Add(new LogicTriggerStartSkillSystem());
        _trigger_logic.Add(new LogicTriggerTimeIntervalSystem());
        _trigger_logic.Add(new LogicTriggerTimeReachSystem());
        _trigger_logic.Add(new LogicTriggerPositionReachSystem());
        _trigger_logic.Add(new LogicTriggerObjCollisionSystem(World));

        _observer_logic.Add(new LogicObserverUnuseCheckSystem());
        _observer_logic.Add(new LogicObserverReuseCheckSystem());
        _observer_logic.Add(new LogicObserverRecycleTimerSystem());
        _observer_logic.Add(new LogicObserverRecycleSystem());

        _render.Add(new RenderAnimFrameSystem(World));
        _render.Add(new RenderTrailSystem(World));
    }

    public WorldboxGame Game { get; private set; }

    public EntityStore World { get; }

    internal void Init()
    {
        TriggerActions.Init();
        Trajectories.Init();
        SkillEntities.Init();
    }
    public void SetMonitorPerf(bool enable)
    {
        _logic.SetMonitorPerf(enable);
        _render.SetMonitorPerf(enable);
    }
    public void AppendPerfLog(StringBuilder sb)
    {
        sb.Append('\n');
        _logic.AppendPerfLog(sb);
        sb.Append('\n');
        _render.AppendPerfLog(sb);
        sb.Append('\n');
    }

    public void RegisterCustomValueReachSystem<TTrigger, TContext, TValue>()
        where TValue : IComparable<TValue>
        where TContext : struct, ICustomValueReachContext<TValue>
        where TTrigger : struct, ICustomValueReachTrigger<TTrigger, TContext, TValue>
    {
        if (!_registered_custom_value_reach_systems.Add($"{typeof(TTrigger)}-{typeof(TContext)}-{typeof(TValue)}"))
            return;

        _trigger_logic.Add(new LogicTriggerCustomValueReachSystem<TTrigger, TContext, TValue>());
    }

    [Hotfixable]
    internal void UpdateLogic(UpdateTick update_tick)
    {
        _logic.Update(update_tick);
        _observer_logic.Update(update_tick);
    }
    [Hotfixable]
    public void NewSkillStarter(string id, ActorExtend user, BaseSimObject init_target, float strength)
    {
        World.CreateEntity(new StartSkillTrigger
        {
            TriggerActionMeta =
                TriggerActionBaseMeta.AllDict[id] as TriggerActionMeta<StartSkillTrigger, StartSkillContext>
        }, new StartSkillContext
        {
            user = user,
            target = init_target,
            strength = strength
        });
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