using System;
using System.Text;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Core.SkillLibV3.Visuals;
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
    public SkillVfxProfileLibrary VfxProfileLib { get; } = new();
    public SkillVfxManager Vfx { get; } = new();
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
        SkillLogicSystemGroup.Add(new LogicSkillVfxTravelSystem());
        SkillLogicSystemGroup.Add(new LogicSkillTravelSystem());
        SkillLogicSystemGroup.Add(new LogicActorCollisionSystem());
        SkillLogicSystemGroup.Add(new LogicSkillVfxFlushSystem());
    }

    internal void Init()
    {
        AssetManager._instance.add(SkillLib, "cultiway.skills");
        AssetManager._instance.add(ModifierLib, "cultiway.skill_modifiers");
        AssetManager._instance.add(TrajLib, "cultiway.trajectories");
        AssetManager._instance.add(VfxProfileLib, "cultiway.skill_vfx_profiles");
    }

    public void SpawnAnim(string path, Vector3 pos, Vector3 rot, float scale = 0.1f, Color? tint = null,
        float frameInterval = 0.1f, bool loop = false, float? lifeTime = null, VisualRotation? visualRotation = null)
    {
        var entity = SkillEntityLibrary.RawAnim.NewEntity();
        var frames = SkillEntityAsset.LoadOrderedFrames(path);
        if (frames.Length == 0)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
            return;
        }

        var data = entity.Data;
        data.Get<Position>().value = pos;
        data.Get<Rotation>().value = rot;
        data.Get<Scale>().value = scale * Vector3.one;
        data.Get<AnimData>().frames = frames;
        data.Get<AnimData>().frame_idx = 0;
        data.Get<AnimData>().frame_timer = 0f;
        data.Get<AnimController>().meta.frame_interval = Mathf.Max(0.01f, frameInterval);
        data.Get<AnimController>().meta.loop = loop;
        data.Get<AliveTimer>().value = 0f;
        data.Get<AliveTimeLimit>().value = lifeTime ?? frames.Length * data.Get<AnimController>().meta.frame_interval;

        if (tint.HasValue)
        {
            entity.AddComponent(new AnimTint(tint.Value));
        }

        if (visualRotation.HasValue)
        {
            entity.AddComponent(visualRotation.Value);
        }
    }

    public bool StartSkillSequence(ActorExtend caster, Entity skill_container, SkillCastPlan plan, float strength,
        float? power_level = null, SkillCastCostSource cost_source = SkillCastCostSource.CasterWakan,
        Kingdom attack_kingdom = null)
    {
        if (caster == null || plan == null || plan.Steps.Count == 0) return false;
        if (!SkillCastCost.TryPay(caster, skill_container, plan, cost_source)) return false;

        World.CreateEntity(new SkillCastSequence()
        {
            Caster = caster,
            SkillContainer = skill_container,
            Steps = plan.Steps.ToArray(),
            AttackKingdom = attack_kingdom,
            Strength = strength,
            PowerLevel = power_level ?? caster.GetPowerLevel(),
            MaxEmitPerTick = 8
        });
        return true;
    }

    public void SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, float strength,
        float delay = 0f, float? power_level = null, float initial_angle_offset_degrees = 0f)
    {
        var target_pos = target.isRekt() ? source.GetSimPos() + Vector3.right : target.GetSimPos();
        SpawnSkill(skill_container, source, target, target_pos, strength, delay, power_level,
            initial_angle_offset_degrees);
    }

    public void SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, Vector3 target_pos,
        float strength, float delay = 0f, float? power_level = null, float initial_angle_offset_degrees = 0f,
        Kingdom attack_kingdom = null)
    {
        ref var container = ref skill_container.GetComponent<SkillContainer>();
        var source_pos = source.GetSimPos();

        var base_dir = target_pos - source_pos;
        if (base_dir.sqrMagnitude < 0.0001f)
        {
            base_dir = Vector3.right;
        }
        else
        {
            base_dir.Normalize();
        }
        var initial_dir = ApplyInitialAngleOffset(base_dir, initial_angle_offset_degrees);

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
        context.TargetObj = target.isRekt() ? null : target;
        context.AttackKingdom = attack_kingdom;
        ref var skill_entity = ref data.Get<SkillEntity>();
        skill_entity.SkillContainer = skill_container;
        context.TargetPos = target_pos;
        context.TargetDir = base_dir;
        ref var pos = ref data.Get<Position>();
        pos.value = source_pos;
        ref var rot = ref data.Get<Rotation>();
        rot.value = initial_dir;

        if (delay > 0f)
        {
            entity.Add(new DelayActive()
            {
                LeftTime = delay
            }, Tags.Get<TagInactive>());
        }

        Vfx.AttachRuntime(entity, skill_container, container.Asset, context.PowerLevel, strength);
        Vfx.QueueCastStart(source, skill_container, container.Asset, initial_dir, context.PowerLevel, strength);
        container.OnSetup?.Invoke(entity);
    }

    private static Vector3 ApplyInitialAngleOffset(Vector3 base_dir, float angle_degrees)
    {
        if (Mathf.Abs(angle_degrees) < 0.01f) return base_dir;

        var rotated = Quaternion.AngleAxis(angle_degrees, Vector3.forward) * base_dir;
        return rotated.sqrMagnitude < 0.0001f ? base_dir : rotated.normalized;
    }
}
