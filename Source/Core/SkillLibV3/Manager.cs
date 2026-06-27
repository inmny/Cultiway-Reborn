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
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public class Manager
{
    
    public WorldboxGame Game { get; private set; }

    public EntityStore World { get; }
    public SkillEntityLibrary SkillLib { get; } = new SkillEntityLibrary();
    public SkillModifierLibrary ModifierLib { get; } = new();
    public TrajectoryLibrary TrajLib { get; } = new TrajectoryLibrary();
    public SystemGroup SkillLogicSystemGroup { get; } = new SystemGroup("SkillLibV3");
    internal Manager(WorldboxGame game)
    {
        Game = game;
        World = ModClass.I.W;
        
        //ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleSkillContainerSystem());
        ModClass.I.LogicPrepareRecycleSystemGroup.Add(new RecycleNonMasteredSkillContainerSystem());
        ModClass.I.GeneralLogicSystems.Add(SkillLogicSystemGroup);

        SkillLogicSystemGroup.Add(new LogicSkillCastSequenceSystem());
        SkillLogicSystemGroup.Add(new LogicTrajectorySystem());
        SkillLogicSystemGroup.Add(new LogicSkillTravelSystem());
        SkillLogicSystemGroup.Add(new LogicActorCollisionSystem());
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
        var frames = SkillEntityAsset.LoadOrderedFrames(path);
        var data = entity.Data;
        data.Get<Position>().value = pos;
        data.Get<Rotation>().value = rot;
        data.Get<Scale>().value = scale * Vector3.one;
        data.Get<AnimData>().frames = frames;
        data.Get<AliveTimeLimit>().value = frames.Length * data.Get<AnimController>().meta.frame_interval;
    }

    public bool StartSkillSequence(ActorExtend caster, Entity skill_container, SkillCastPlan plan, float strength,
        float? power_level = null)
    {
        if (caster == null || plan == null || plan.Steps.Count == 0) return false;
        if (!SkillCastCost.CanAffordStepWakan(caster, skill_container)) return false;

        World.CreateEntity(new SkillCastSequence()
        {
            Caster = caster,
            SkillContainer = skill_container,
            Steps = plan.Steps.ToArray(),
            Strength = strength,
            PowerLevel = power_level ?? caster.GetPowerLevel(),
            MaxEmitPerTick = 8
        });
        return true;
    }

    public void SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, float strength,
        float delay = 0f, float? power_level = null)
    {
        ref var container = ref skill_container.GetComponent<SkillContainer>();
        var source_pos = source.GetSimPos();
        var target_pos = target.GetSimPos();

        var base_dir = target_pos - source_pos;
        if (base_dir.sqrMagnitude < 0.0001f)
        {
            base_dir = Vector3.right;
        }
        else
        {
            base_dir.Normalize();
        }

        var entity = container.Asset.NewEntity();
        entity.AddRelation(new SkillMasterRelation()
        {
            SkillContainer = skill_container
        });
        var data = entity.Data;
        ref var context = ref data.Get<SkillContext>();
        context.Strength = strength;
        context.PowerLevel = power_level ?? ((source?.isActor() ?? false) && !source.isRekt()
            ? source.a.GetExtend().GetPowerLevel()
            : 0f);
        context.SourceObj = source;
        context.TargetObj = target;
        ref var skill_entity = ref data.Get<SkillEntity>();
        skill_entity.SkillContainer = skill_container;
        context.TargetPos = target_pos;
        context.TargetDir = base_dir;
        ref var pos = ref data.Get<Position>();
        pos.value = source_pos;
        ref var rot = ref data.Get<Rotation>();
        rot.value = base_dir;

        if (delay > 0f)
        {
            entity.Add(new DelayActive()
            {
                LeftTime = delay
            }, Tags.Get<TagInactive>());
        }

        container.OnSetup?.Invoke(entity);
    }
}
