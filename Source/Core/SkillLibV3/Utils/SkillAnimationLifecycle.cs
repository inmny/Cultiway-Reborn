using Cultiway.Core.Components;
using Cultiway.Core.Components.AnimOverwrite;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3.Utils;

/// <summary>
/// 法术动画阶段的唯一切换入口。
/// </summary>
internal static class SkillAnimationLifecycle
{
    public static void Initialize(Entity entity, SkillEntityAnimation animation, float baseFrameInterval,
        bool baseLoop)
    {
        if (!animation.HasLifecycle)
        {
            ApplyRuntimeClip(entity, animation.Runtime, baseFrameInterval, baseLoop);
            return;
        }

        var state = new SkillAnimationLifecycleState
        {
            Animation = animation,
            Phase = animation.HasAppearance ? SkillAnimationPhase.Appearance : SkillAnimationPhase.Runtime,
            BaseFrameInterval = Mathf.Max(0.01f, baseFrameInterval),
            BaseLoop = baseLoop,
        };
        if (entity.TryGetComponent(out AnimLoop runtimeLoop))
        {
            state.HasRuntimeLoopOverride = true;
            state.RuntimeLoopOverride = runtimeLoop.value;
        }

        entity.AddComponent(state);
        if (animation.HasAppearance)
        {
            EnterAppearance(entity);
        }
        else
        {
            EnterRuntime(entity);
        }
    }

    public static void EnterRuntime(Entity entity)
    {
        ref SkillAnimationLifecycleState state = ref entity.GetComponent<SkillAnimationLifecycleState>();
        state.Phase = SkillAnimationPhase.Runtime;
        ApplyRuntimeClip(entity, state.Animation.Runtime, state.BaseFrameInterval, state.BaseLoop);
        RestoreRuntimeLoopOverride(entity, ref state);
        SetTag<TagSkillAnimationNoMovement>(entity, false);
        SetTag<TagSkillAnimationNoCollision>(entity, false);
        SetTag<TagSkillAnimationNoTravelEffects>(entity, false);
        SetTag<TagSuppressAnimAfterimage>(entity, false);
        SetTag<TagSuspendAliveTimeLimit>(entity, false);
        entity.GetComponent<AliveTimer>().value = 0f;
        entity.GetComponent<SkillImpactFeedbackState>().NextAllowedTime = 0f;
        ResetTravelSampling(entity);
    }

    public static void EnterDissipation(Entity entity)
    {
        ref SkillAnimationLifecycleState state = ref entity.GetComponent<SkillAnimationLifecycleState>();
        state.Phase = SkillAnimationPhase.Dissipation;
        CancelDelayedActivation(entity);
        ApplyTransientClip(entity, state.Animation.Dissipation, state.BaseFrameInterval);
        SetTag<TagSkillAnimationNoMovement>(entity, true);
        SetTag<TagSkillAnimationNoCollision>(entity, true);
        SetTag<TagSkillAnimationNoTravelEffects>(entity, true);
        SetTag<TagSuppressAnimAfterimage>(entity, true);
        SetTag<TagSuspendAliveTimeLimit>(entity, true);
    }

    public static void MarkRecycleReady(Entity entity)
    {
        ref SkillAnimationLifecycleState state = ref entity.GetComponent<SkillAnimationLifecycleState>();
        state.Phase = SkillAnimationPhase.RecycleReady;
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(entity.Id);
    }

    public static float ResolveCurrentFrameInterval(Entity entity)
    {
        if (entity.TryGetComponent(out AnimFrameInterval interval))
        {
            return Mathf.Max(0.01f, interval.value);
        }

        return Mathf.Max(0.01f, entity.GetComponent<AnimController>().meta.frame_interval);
    }

    private static void EnterAppearance(Entity entity)
    {
        ref SkillAnimationLifecycleState state = ref entity.GetComponent<SkillAnimationLifecycleState>();
        state.Phase = SkillAnimationPhase.Appearance;
        ApplyTransientClip(entity, state.Animation.Appearance, state.BaseFrameInterval);

        SkillAnimationGameplayFlags gameplay = state.Animation.AppearanceGameplay;
        bool movement = (gameplay & SkillAnimationGameplayFlags.Movement) != 0;
        SetTag<TagSkillAnimationNoMovement>(entity, !movement);
        SetTag<TagSkillAnimationNoCollision>(
            entity,
            (gameplay & SkillAnimationGameplayFlags.Collision) == 0);
        SetTag<TagSkillAnimationNoTravelEffects>(
            entity,
            (gameplay & SkillAnimationGameplayFlags.TravelEffects) == 0);
        SetTag<TagSuppressAnimAfterimage>(entity, !movement);
        SetTag<TagSuspendAliveTimeLimit>(entity, true);
    }

    private static void ApplyRuntimeClip(Entity entity, SkillEntityAnimationClip clip, float baseFrameInterval,
        bool baseLoop)
    {
        ResetFrames(entity, clip);
        ref AnimController controller = ref entity.GetComponent<AnimController>();
        controller.meta.frame_interval = clip.Settings.ResolveFrameInterval(baseFrameInterval);
        controller.meta.loop = clip.Settings.ResolveLoop(baseLoop);
    }

    private static void ApplyTransientClip(Entity entity, SkillEntityAnimationClip clip, float baseFrameInterval)
    {
        ResetFrames(entity, clip);
        ref AnimController controller = ref entity.GetComponent<AnimController>();
        controller.meta.frame_interval = clip.Settings.ResolveFrameInterval(baseFrameInterval);
        controller.meta.loop = false;
        if (entity.HasComponent<AnimLoop>())
        {
            entity.GetComponent<AnimLoop>().value = false;
        }
    }

    private static void ResetFrames(Entity entity, SkillEntityAnimationClip clip)
    {
        ref AnimData animData = ref entity.GetComponent<AnimData>();
        animData.frames = clip.Frames;
        animData.frame_idx = 0;
        animData.frame_timer = 0f;
    }

    private static void RestoreRuntimeLoopOverride(Entity entity, ref SkillAnimationLifecycleState state)
    {
        if (!state.HasRuntimeLoopOverride) return;
        entity.GetComponent<AnimLoop>().value = state.RuntimeLoopOverride;
    }

    private static void ResetTravelSampling(Entity entity)
    {
        Position position = entity.GetComponent<Position>();
        ref SkillGroundFxState groundFxState = ref entity.GetComponent<SkillGroundFxState>();
        groundFxState.DistanceAccumulator = 0f;
        groundFxState.LastX = position.x;
        groundFxState.LastY = position.y;
    }

    private static void CancelDelayedActivation(Entity entity)
    {
        if (entity.Tags.Has<TagInactive>())
        {
            entity.RemoveTag<TagInactive>();
        }
        if (entity.HasComponent<DelayActive>())
        {
            entity.RemoveComponent<DelayActive>();
        }
    }

    private static void SetTag<TTag>(Entity entity, bool enabled)
        where TTag : struct, ITag
    {
        if (enabled)
        {
            if (!entity.Tags.Has<TTag>()) entity.AddTag<TTag>();
            return;
        }

        if (entity.Tags.Has<TTag>()) entity.RemoveTag<TTag>();
    }
}
