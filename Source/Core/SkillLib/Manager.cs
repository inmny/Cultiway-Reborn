using Cultiway.Core.SkillLib.Components;
using Cultiway.Core.SkillLib.Systems.Logic;
using Cultiway.Core.SkillLib.Systems.Render;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using Position = Cultiway.Core.SkillLib.Components.Position;

namespace Cultiway.Core.SkillLib;

public class Manager
{
    private SystemRoot  _logic_system_root;
    private SystemRoot  _render_system_root;
    private EntityStore _world;

    internal Manager()
    {
        _world = new EntityStore();
        _logic_system_root = new SystemRoot(_world,  "SkillLib.Logic");
        _render_system_root = new SystemRoot(_world, "SkillLib.Render");

        _logic_system_root.Add(new SkillEntityRecycleSystem());
        _logic_system_root.Add(new AliveTimeUpdateSystem());
        _logic_system_root.Add(new TrajectorySystem());
        _logic_system_root.Add(new TimeIntervalSystem());
        _logic_system_root.Add(new TimeReachSystem());
        _logic_system_root.Add(new ObjCollisionSystem());
        _logic_system_root.Add(new AnimFrameUpdateSystem(_world));
        _logic_system_root.Add(new AnimLoopTriggerSystem());

        _render_system_root.Add(new AnimSystem(_world));
    }

    internal void UpdateLogic(UpdateTick update_tick)
    {
        _logic_system_root.Update(update_tick);
    }

    internal void UpdateRender(UpdateTick update_tick)
    {
        _render_system_root.Update(update_tick);
    }

    internal Entity NewPrefab()
    {
        var e = _world.CreateEntity();
        e.AddTag<PrefabTag>();
        return e;
    }

    internal Entity RequestSkillEntity(ActorExtend user, BaseSimObject target, WorldTile tile, float energy)
    {
        return _world.CreateEntity(new SkillInfo()
        {
            energy = energy,
            target = target,
            target_tile = tile,
            user = user
        }, new AliveTimer());
    }

    internal Entity RequestSkillEntity(ref SkillInfo skill_info)
    {
        return _world.CreateEntity(skill_info, new AliveTimer());
    }

    internal Entity RequestSkillEntity(SkillEntityAsset asset, ref SkillInfo skill_info, Position pos = default)
    {
        var e = _world.CreateEntity(skill_info, new SkillEntityComponent()
        {
            asset = asset
        }, new AliveTimer()
        {
            alive_time = 0
        });
        if (asset.anim_setting != null)
        {
            e.AddComponent(new SkillAnimData()
            {
                idx = 0, timer = 0
            });
            e.AddComponent(pos);
        }

        return e;
    }
}