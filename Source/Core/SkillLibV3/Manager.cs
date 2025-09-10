using System;
using System.Text;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Core.Systems.Logic;
using Cultiway.Core.Systems.Render;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public class Manager
{
    
    public WorldboxGame Game { get; private set; }

    public EntityStore World { get; }
    public SkillEntityLibrary SkillLib { get; } = new SkillEntityLibrary();
    public SkillModifierLibrary ModifierLib { get; } = new();
    public TrajectoryLibrary TrajLib { get; } = new TrajectoryLibrary();
    private readonly SystemRoot _logic;
    private readonly SystemRoot _render;
    internal Manager(WorldboxGame game)
    {
        Game = game;
        World = ModClass.I.W;
        _logic = new SystemRoot(World, "SkillLibV3.Logic");
        _render = new SystemRoot(World, "SkillLibV3.Render");
        
        
        _logic.Add(new AliveTimerSystem());
        _logic.Add(new AliveTimerCheckSystem());
        _logic.Add(new DelayActiveCheckSystem());
        
        _logic.Add(new RecycleSkillContainerSystem());
        _logic.Add(new RecycleAnimRendererSystem());
        _logic.Add(new RecycleDefaultEntitySystem());

        _logic.Add(new LogicTrajectorySystem());
        _logic.Add(new AnimFrameUpdateSystem(World));
        
        _logic.Add(new LogicActorCollisionSystem());
        
        
        _render.Add(new RenderAnimFrameSystem(World));
    }

    internal void Init()
    {
        AssetManager._instance.add(SkillLib, "cultiway.skills");
        AssetManager._instance.add(ModifierLib, "cultiway.skill_modifiers");
        AssetManager._instance.add(TrajLib, "cultiway.trajectories");
    }

    public void SpawnAnim(string path, Vector3 pos, Vector3 rot, float scale = 0.1f)
    {
        var entity = SkillEntityLibrary.RawAnim.NewEntity();
        var data = entity.Data;
        data.Get<Position>().value = pos;
        data.Get<Rotation>().value = rot;
        data.Get<Scale>().value = scale * Vector3.one;
        data.Get<AnimData>().frames = SpriteTextureLoader.getSpriteList(path);
    }
    public void SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, float strength)
    {
        ref var container = ref skill_container.GetComponent<SkillContainer>();
        var salvo_count = skill_container.TryGetComponent(out SalvoCount salvo_modifier) ? salvo_modifier.Value : 1;
        var burst_count = skill_container.TryGetComponent(out BurstCount burst_modifier) ? burst_modifier.Value : 1;

        for (int i = 0; i < salvo_count; i++)
        {
            var delay = i * 0.5f;
            for (int j = 0; j < burst_count; j++)
            {
                var entity = container.Asset.NewEntity();
                entity.AddRelation(new SkillMasterRelation()
                {
                    SkillContainer = skill_container
                });
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

                if (delay > 0)
                {
                    entity.Add(new DelayActive()
                    {
                        LeftTime = delay
                    }, Tags.Get<TagInactive>());
                }
        
                container.OnSetup?.Invoke(entity);
            }
        }
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