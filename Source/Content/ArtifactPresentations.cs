using System;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Content.Libraries;
using Cultiway.Content.Utils;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>
/// 基础器形的世界表现方案。方案只负责姿态与运动，不参与能力结算。
/// </summary>
public class ArtifactPresentations : ExtendLibrary<ArtifactPresentationAsset, ArtifactPresentations>
{
    private const int OrbitRingCapacity = 6;

    public static ArtifactPresentationAsset Sword { get; private set; }
    public static ArtifactPresentationAsset Seal { get; private set; }
    public static ArtifactPresentationAsset Robe { get; private set; }
    public static ArtifactPresentationAsset Mirror { get; private set; }
    public static ArtifactPresentationAsset Ding { get; private set; }

    protected override bool AutoRegisterAssets() => true;
    protected override string Prefix() => "Cultiway.ArtifactPresentation";

    protected override void OnInit()
    {
        Set(Sword, 0.11f, 1.45f, ResolveSword);
        Set(Seal, 0.18f, 1.25f, ResolveSeal);
        Set(Robe, 0.24f, 1.15f, ResolveRobe);
        Set(Mirror, 0.14f, 1.1f, ResolveMirror);
        Set(Ding, 0.22f, 1.35f, ResolveDing);
    }

    private static void Set(
        ArtifactPresentationAsset presentation,
        float bodyRadius,
        float activeWorldSize,
        Func<ArtifactPresentationContext, ArtifactPresentationPose> resolvePose)
    {
        presentation.body_radius = bodyRadius;
        presentation.active_world_size = activeWorldSize;
        presentation.ResolvePose = resolvePose;
    }

    private static ArtifactPresentationPose ResolveSword(ArtifactPresentationContext context)
    {
        float stateScale = context.control_state.GetStateScale();
        float activity = MotionActivity(context.control_state);
        if (context.control_state != ArtifactControlState.Cold)
        {
            Vector3 position = ResolveOrbit(
                context,
                0.34f + activity * 0.16f,
                0.58f + activity * 0.1f,
                0.75f + activity * 1.05f);
            return new ArtifactPresentationPose
            {
                position = position,
                rotation = 180f,
                world_size = context.actor_scale * 0.62f * stateScale,
                sorting_order = 9,
            };
        }

        float spread = CenteredIndex(context.index, context.count);
        float side = context.actor.flip ? -1f : 1f;
        float phase = context.time * 0.65f + context.index * 0.79f;
        return new ArtifactPresentationPose
        {
            position = context.actor.cur_transform_position + new Vector3(
                side * (0.36f + Mathf.Abs(spread) * 0.08f) * context.actor_scale,
                (0.57f + spread * 0.06f + Mathf.Sin(phase) * 0.05f) * context.actor_scale,
                0f),
            rotation = side * -8f + spread * 7f + Mathf.Sin(phase * 0.8f) * 4f,
            world_size = context.actor_scale * 0.7f * stateScale,
            flip_x = context.actor.flip,
            sorting_order = 7,
        };
    }

    private static ArtifactPresentationPose ResolveSeal(ArtifactPresentationContext context)
    {
        float spread = CenteredIndex(context.index, context.count);
        float activity = MotionActivity(context.control_state);
        float phase = context.time * (0.9f + activity * 0.65f) + context.index * 0.73f;
        float bobAmplitude = 0.06f + activity * 0.1f;
        return new ArtifactPresentationPose
        {
            position = context.actor.cur_transform_position + new Vector3(
                spread * 0.22f * context.actor_scale,
                (0.82f + Mathf.Sin(phase) * bobAmplitude) * context.actor_scale,
                0f),
            rotation = Mathf.Sin(phase * 0.65f) * (2f + activity * 7f),
            world_size = context.actor_scale * 0.56f * context.control_state.GetStateScale(),
            sorting_order = 10,
        };
    }

    private static ArtifactPresentationPose ResolveRobe(ArtifactPresentationContext context)
    {
        float spread = CenteredIndex(context.index, context.count);
        return new ArtifactPresentationPose
        {
            position = context.actor.cur_transform_position + new Vector3(
                spread * 0.08f * context.actor_scale,
                0.34f * context.actor_scale,
                0f),
            world_size = context.actor_scale * 0.78f * context.control_state.GetStateScale(),
            flip_x = context.actor.flip,
            sorting_order = 3,
        };
    }

    private static ArtifactPresentationPose ResolveMirror(ArtifactPresentationContext context)
    {
        float activity = MotionActivity(context.control_state);
        float phase = context.time * (0.65f + activity * 0.45f) + context.index * 1.17f;
        float side = context.index % 2 == 0 ? 1f : -1f;
        float horizontalAmplitude = 0.04f + activity * 0.08f;
        float verticalAmplitude = 0.07f + activity * 0.12f;
        return new ArtifactPresentationPose
        {
            position = context.actor.cur_transform_position + new Vector3(
                (side * 0.43f + Mathf.Cos(phase) * horizontalAmplitude) * context.actor_scale,
                (0.62f + Mathf.Sin(phase) * verticalAmplitude) * context.actor_scale,
                0f),
            rotation = Mathf.Sin(phase * 0.72f) * (4f + activity * 10f),
            world_size = context.actor_scale * 0.5f * context.control_state.GetStateScale(),
            sorting_order = 8,
        };
    }

    private static ArtifactPresentationPose ResolveDing(ArtifactPresentationContext context)
    {
        float spread = CenteredIndex(context.index, context.count);
        float activity = MotionActivity(context.control_state);
        float phase = context.time * (0.8f + activity * 0.55f) + context.index * 0.61f;
        float bobAmplitude = 0.04f + activity * 0.07f;
        return new ArtifactPresentationPose
        {
            position = context.actor.cur_transform_position + new Vector3(
                spread * 0.2f * context.actor_scale,
                (0.38f + activity * 0.18f + Mathf.Sin(phase) * bobAmplitude) * context.actor_scale,
                0f),
            rotation = Mathf.Sin(phase * 0.42f) * (1f + activity * 3f),
            world_size = context.actor_scale * 0.62f * context.control_state.GetStateScale(),
            sorting_order = 6,
        };
    }

    private static Vector3 ResolveOrbit(
        ArtifactPresentationContext context,
        float radius,
        float height,
        float speed)
    {
        int ring = context.index / OrbitRingCapacity;
        int indexInRing = context.index % OrbitRingCapacity;
        int ringCount = Math.Min(OrbitRingCapacity, context.count - ring * OrbitRingCapacity);
        float direction = ring % 2 == 0 ? 1f : -1f;
        float angularVelocity = direction * (speed + ring * 0.08f);
        float angle = context.time * angularVelocity + indexInRing * Mathf.PI * 2f / ringCount;
        float ringRadius = (radius + ring * 0.16f) * context.actor_scale;
        return context.actor.cur_transform_position + new Vector3(
            Mathf.Cos(angle) * ringRadius,
            (height + ring * 0.08f + Mathf.Sin(angle) * 0.2f) * context.actor_scale,
            0f);
    }

    private static float CenteredIndex(int index, int count)
    {
        return index - (count - 1) * 0.5f;
    }

    private static float MotionActivity(ArtifactControlState state)
    {
        return state switch
        {
            ArtifactControlState.Cold => 0.25f,
            ArtifactControlState.Ready => 0.65f,
            ArtifactControlState.Operating => 1f,
            ArtifactControlState.Overloaded => 1.15f,
            _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
        };
    }
}
