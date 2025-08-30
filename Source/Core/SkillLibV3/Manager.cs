using System;
using System.Text;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3;

public class Manager
{
    
    public WorldboxGame Game { get; private set; }

    public EntityStore World { get; }
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
    }

    internal void Init()
    {
        
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