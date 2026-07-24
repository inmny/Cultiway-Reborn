using System;
using System.Runtime.CompilerServices;
using Cultiway.Const;
using UnityEngine;

namespace Cultiway.Core.Performance;

internal static class PresentationInterpolator
{
    private static ConditionalWeakTable<Actor, ActorPresentationState> states = new();
    private static int preparedFrame = -1;
    private static float presentationDelta = 1f / 60f;
    private static float presentationTimeScale = 1f;
    private static float presentationClock;

    public static void PrepareFrame()
    {
        preparedFrame = Time.frameCount;
        presentationDelta = Mathf.Clamp(Time.unscaledDeltaTime, 0f, 0.1f);
        presentationTimeScale = Mathf.Max(0f, Config.time_scale_asset?.multiplier ?? 1f);
        presentationClock = Time.unscaledTime;
    }

    public static void Apply(Actor actor, ref Vector3 result)
    {
        if (!PerformanceSettings.EnableFramePriorityScheduler ||
            !PerformanceSettings.EnablePresentationSmoothing ||
            actor == null ||
            (!actor.is_visible && !SelectedUnit.isSelected(actor) &&
             !ReferenceEquals(actor, ControllableUnit.getControllableUnit())))
        {
            return;
        }

        Vector2 target = actor.current_position;
        if (!IsFinite(target))
        {
            return;
        }

        ActorPresentationState state = states.GetValue(actor, static _ => new ActorPresentationState());
        Vector2 presented;
        lock (state)
        {
            if (!state.Initialized)
            {
                state.Initialized = true;
                state.Presented = target;
                state.Authoritative = target;
                state.AuthoritativeChangedAt = presentationClock;
                state.LastFrame = preparedFrame;
            }
            else
            {
                Vector2 authoritativeDelta = target - state.Authoritative;
                if (authoritativeDelta.sqrMagnitude > 64f)
                {
                    state.Presented = target;
                    state.AuthoritySampleCount = 0;
                    state.EstimatedAuthorityInterval = 0.25f;
                    state.AuthoritativeChangedAt = presentationClock;
                }
                else if (authoritativeDelta.sqrMagnitude > 0.000001f)
                {
                    float interval = presentationClock - state.AuthoritativeChangedAt;
                    if (interval is >= 0.005f and <= 2f)
                    {
                        state.EstimatedAuthorityInterval = state.AuthoritySampleCount == 0
                            ? interval
                            : Mathf.Lerp(state.EstimatedAuthorityInterval, interval, 0.25f);
                        state.AuthoritySampleCount++;
                    }

                    state.AuthoritativeChangedAt = presentationClock;
                }

                state.Authoritative = target;
                if (state.LastFrame != preparedFrame)
                {
                    state.LastFrame = preparedFrame;
                    bool controlled = ReferenceEquals(actor, ControllableUnit.getControllableUnit());
                    bool selected = SelectedUnit.isSelected(actor);
                    Vector2 movementTarget = actor.next_step_position;
                    bool canPredictMovement = actor.is_moving &&
                                              IsFinite(movementTarget) &&
                                              (movementTarget - target).sqrMagnitude > 0.0001f;

                    if (canPredictMovement)
                    {
                        float emphasis = controlled ? 1.25f : selected ? 1.1f : 1f;
                        float baseSpeed = Mathf.Max(0.4f, actor._current_combined_movement_speed) * emphasis;
                        float speed = baseSpeed * presentationTimeScale;
                        if (state.AuthoritySampleCount > 0)
                        {
                            float elapsedSinceAuthority = presentationClock - state.AuthoritativeChangedAt;
                            float remainingInterval = Mathf.Max(
                                presentationDelta,
                                state.EstimatedAuthorityInterval - elapsedSinceAuthority);
                            float cadenceSpeed = Vector2.Distance(state.Presented, movementTarget) /
                                                 remainingInterval;
                            speed = Mathf.Min(speed, Mathf.Max(baseSpeed, cadenceSpeed));
                        }

                        state.Presented = Vector2.MoveTowards(
                            state.Presented,
                            movementTarget,
                            speed * presentationDelta);
                    }
                    else
                    {
                        float responsiveness = controlled ? 45f : selected ? 30f : 18f;
                        float alpha = 1f - Mathf.Exp(-responsiveness * presentationDelta);
                        state.Presented = Vector2.LerpUnclamped(state.Presented, target, alpha);
                        if ((state.Presented - target).sqrMagnitude < 0.0001f)
                        {
                            state.Presented = target;
                        }
                    }
                }
            }

            presented = state.Presented;
        }

        Vector2 shake = actor.shake_offset;
        Vector2 jump = actor.move_jump_offset;
        actor.current_shadow_position.Set(presented.x + shake.x, presented.y + shake.y);
        actor.cur_transform_position.Set(
            presented.x + jump.x + shake.x,
            presented.y + jump.y + shake.y + actor.position_height,
            actor.position_height);
        result = actor.cur_transform_position;
    }

    public static void Reset()
    {
        states = new ConditionalWeakTable<Actor, ActorPresentationState>();
        preparedFrame = -1;
        presentationDelta = 1f / 60f;
        presentationTimeScale = 1f;
        presentationClock = 0f;
    }

    private static bool IsFinite(Vector2 value)
    {
        return !float.IsNaN(value.x) &&
               !float.IsInfinity(value.x) &&
               !float.IsNaN(value.y) &&
               !float.IsInfinity(value.y);
    }

    private sealed class ActorPresentationState
    {
        public bool Initialized;
        public Vector2 Presented;
        public Vector2 Authoritative;
        public float AuthoritativeChangedAt;
        public float EstimatedAuthorityInterval = 0.25f;
        public int AuthoritySampleCount;
        public int LastFrame;
    }
}
