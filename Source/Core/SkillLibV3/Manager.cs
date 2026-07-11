using System;
using System.Text;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Components.TrajParams;
using Cultiway.Core.SkillLibV3.Editor;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Motions;
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
    public SkillVfxElementLibrary VfxElementLib { get; } = new();
    public SkillMotionProfileLibrary MotionProfileLib { get; } = new();
    public WanfaPavilionPolicyLibrary WanfaPolicyLib { get; } = new();
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
        SkillLogicSystemGroup.Add(new LogicSkillGroundFxRecordSystem());
        SkillLogicSystemGroup.Add(new LogicSkillTravelSystem());
        SkillLogicSystemGroup.Add(new LogicActorCollisionSystem());
    }

    internal void Init()
    {
        AssetManager._instance.add(SkillLib, "cultiway.skills");
        AssetManager._instance.add(ModifierLib, "cultiway.skill_modifiers");
        AssetManager._instance.add(TrajLib, "cultiway.trajectories");
        AssetManager._instance.add(VfxElementLib, "cultiway.skill_vfx_elements");
        AssetManager._instance.add(MotionProfileLib, "cultiway.skill_motion_profiles");
        AssetManager._instance.add(WanfaPolicyLib, "cultiway.wanfa_pavilion_policy");
    }

    public void SpawnAnim(string path, Vector3 pos, Vector3 rot, float scale = 0.1f, Color? tint = null,
        float frameInterval = 0.1f, bool loop = false, float? lifeTime = null, VisualRotation? visualRotation = null)
    {
        var frames = SkillEntityAsset.LoadOrderedFrames(path);
        SpawnAnim(frames, pos, rot, scale, tint, frameInterval, loop, lifeTime, visualRotation);
    }

    public void SpawnAnim(Sprite[] frames, Vector3 pos, Vector3 rot, float scale = 0.1f, Color? tint = null,
        float frameInterval = 0.1f, bool loop = false, float? lifeTime = null, VisualRotation? visualRotation = null)
    {
        var entity = SkillEntityLibrary.RawAnim.NewEntity();
        if (frames == null || frames.Length == 0)
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

    public Entity SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, float strength,
        float delay = 0f, float? power_level = null, float initial_angle_offset_degrees = 0f)
    {
        var target_pos = target.isRekt() ? source.GetSimPos() + Vector3.right : target.GetSimPos();
        return SpawnSkill(skill_container, source, target, target_pos, strength, delay, power_level,
            initial_angle_offset_degrees);
    }

    public Entity SpawnSkill(Entity skill_container, BaseSimObject source, BaseSimObject target, Vector3 target_pos,
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
        skill_entity.VfxElement = container.VfxElement;
        context.TargetPos = target_pos;
        context.TargetDir = base_dir;
        ref var pos = ref data.Get<Position>();
        pos.value = source_pos;
        SyncRuntimeFxStartPosition(entity, source_pos);
        ref var rot = ref data.Get<Rotation>();
        rot.value = initial_dir;

        ApplyMotionProfile(entity, container.MotionProfile, source_pos, target_pos);

        if (delay > 0f)
        {
            entity.Add(new DelayActive()
            {
                LeftTime = delay
            }, Tags.Get<TagInactive>());
        }

        if (container.OnSetup != null)
        {
            container.OnSetup(entity);
        }

        ApplyTrajectoryAfterimage(entity, container.MotionProfile);
        return entity;
    }

    private static void ApplyMotionProfile(Entity entity, SkillMotionProfileAsset profile, Vector3 sourcePos,
        Vector3 targetPos)
    {
        var distance = Vector2.Distance(new Vector2(sourcePos.x, sourcePos.y), new Vector2(targetPos.x, targetPos.y));
        var velocity = new Velocity
        {
            Value = profile.ResolveSpeed(distance)
        };
        SetOrAdd(entity, velocity);
        SetOrAdd(entity, new TurnRate
        {
            Value = profile.TurnRate
        });
        SetOrAdd(entity, new SkillVelocityRamp
        {
            StartMultiplier = profile.LaunchMultiplier,
            EndMultiplier = profile.CruiseMultiplier,
            RampDuration = profile.RampDuration,
            Elapsed = 0f
        });

        ref var controller = ref entity.GetComponent<AnimController>();
        controller.meta.frame_interval = profile.FrameInterval;
    }

    private static void ApplyTrajectoryAfterimage(Entity entity, SkillMotionProfileAsset profile)
    {
        if (!entity.TryGetComponent(out Trajectory trajectory)) return;

        var trajectoryAsset = trajectory.Asset;
        var afterimageOrientations = TrajectoryOrientation.Horizontal | TrajectoryOrientation.Melee;
        if ((trajectoryAsset.Orientations & afterimageOrientations) == TrajectoryOrientation.None)
        {
            if (entity.HasComponent<AnimAfterimage>())
            {
                entity.RemoveComponent<AnimAfterimage>();
            }
            return;
        }

        var speed = entity.GetComponent<Velocity>().Value;
        var afterimage = profile.ResolveAfterimage(speed);
        if (entity.HasComponent<AnimAfterimage>())
        {
            ref var current = ref entity.GetComponent<AnimAfterimage>();
            current = afterimage;
        }
        else
        {
            entity.AddComponent(afterimage);
        }
    }

    private static void SetOrAdd<TComponent>(Entity entity, TComponent component)
        where TComponent : struct, IComponent
    {
        if (entity.HasComponent<TComponent>())
        {
            ref var current = ref entity.GetComponent<TComponent>();
            current = component;
            return;
        }

        entity.AddComponent(component);
    }

    private static void SyncRuntimeFxStartPosition(Entity entity, Vector3 sourcePos)
    {
        var planePos = new Vector2(sourcePos.x, sourcePos.y);
        if (entity.HasComponent<PrevPosition>())
        {
            ref var prevPosition = ref entity.GetComponent<PrevPosition>();
            prevPosition.Value = planePos;
        }

        if (entity.HasComponent<SkillGroundFxState>())
        {
            ref var groundFxState = ref entity.GetComponent<SkillGroundFxState>();
            groundFxState.DistanceAccumulator = 0f;
            groundFxState.LastX = sourcePos.x;
            groundFxState.LastY = sourcePos.y;
        }
    }

    private static Vector3 ApplyInitialAngleOffset(Vector3 base_dir, float angle_degrees)
    {
        if (Mathf.Abs(angle_degrees) < 0.01f) return base_dir;

        var rotated = Quaternion.AngleAxis(angle_degrees, Vector3.forward) * base_dir;
        return rotated.sqrMagnitude < 0.0001f ? base_dir : rotated.normalized;
    }
}
