using System;
using System.Text;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Core.Systems.Logic;
using Cultiway.Core.Systems.Render;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3;

public class Manager
{
    
    public WorldboxGame Game { get; private set; }

    public EntityStore World { get; }
    public SkillEntityLibrary SkillLib { get; } = new SkillEntityLibrary();
    public TrajectoryLibrary TrajLib { get; } = new TrajectoryLibrary();
    private readonly SystemRoot _logic;
    private readonly SystemRoot _render;
    internal Manager(WorldboxGame game)
    {
        Game = game;
        World = new EntityStore()
        {
            JobRunner = new ParallelJobRunner(Environment.ProcessorCount)
        };
        _logic = new SystemRoot(World, "SkillLibV3.Logic");
        _render = new SystemRoot(World, "SkillLibV3.Render");
        AssetManager._instance.add(SkillLib, "cultiway.skills");
        AssetManager._instance.add(TrajLib, "cultiway.trajectories");
        
        
        _logic.Add(new AliveTimerSystem());
        _logic.Add(new AliveTimerCheckSystem());
        
        _logic.Add(new RecycleAnimRendererSystem());
        _logic.Add(new RecycleDefaultEntitySystem());

        _logic.Add(new LogicTrajectorySystem());
        _logic.Add(new AnimFrameUpdateSystem(World));
        
        _logic.Add(new LogicActorCollisionSystem());
        
        
        _render.Add(new RenderAnimFrameSystem(World));
    }

    internal void Init()
    {
        
    }

    public void SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, float strength)
    {
        var entity = skill_container.GetComponent<SkillContainer>().Asset.NewEntity();
        var data = entity.Data;
        ref var context = ref data.Get<SkillContext>();
        context.Strength = strength;
        context.SourceObj = source;
        context.TargetObj = target;
        var target_pos = target.GetSimPos();
        context.TargetPos = target_pos;
        context.TargetDir = (target_pos - source.GetSimPos()).normalized;
        ref var pos = ref data.Get<Position>();
        pos.value = source.current_position;
    }

    internal void UpdateLogic(UpdateTick update_tick)
    {
        _logic.Update(update_tick);
    }

    internal void UpdateRender(UpdateTick update_tick)
    {
        _render.Update(update_tick);
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
}