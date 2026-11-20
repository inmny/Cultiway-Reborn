using System;
using Cultiway.Content.Combat;
using Cultiway.Content.Persistence;
using Cultiway.Core;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>原人物活着到达真实承载位置，并进入深影的时点。</summary>
public readonly struct ConstructedShadowBurial
{
    public readonly long CastId, TargetId;
    public readonly Vector3 Entry, Foot;
    public readonly double WorldTime;

    internal ConstructedShadowBurial(long castId, long targetId, Vector3 entry, Vector3 foot, double worldTime)
    {
        CastId = castId;
        TargetId = targetId;
        Entry = entry;
        Foot = foot;
        WorldTime = worldTime;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>确认到达后只发布一次；没有真正到达时不宣称已拖入。</summary>
    public static event Action<ConstructedShadowBurial> ShadowBurialConfirmed;

    private sealed partial class ShadowHold
    {
        internal BodySurfaceBinding Binding;
        internal BodySurfacePose CaughtBody, Pose;
        internal Vector3 CaughtFoot, DragOffset;
        internal float Cover;
        internal bool Arrived;
    }

    private static void AdvanceShadowBurial(ShadowHold hold, CompiledShadowHold rule, double clock)
    {
        Actor actor = hold.Running.Target;
        if (hold.Binding.ChangedReason != null)
        {
            ReleaseShadowHold(hold.Running, "BodyChanged", clock);
            return;
        }

        float elapsed = Mathf.Clamp01((float)(clock - hold.HeldAt) / rule.HoldSeconds);
        float amount = Mathf.SmoothStep(0f, 1f, (elapsed - .08f) / .44f);
        Vector3 destination = hold.CaughtFoot + hold.Forward * (rule.DragDistance * amount);
        if (TryDragShadow(actor, hold.Foot, destination, out Vector3 next))
        {
            hold.Foot = next;
            hold.DragOffset = next - hold.CaughtFoot;
            PublishBodyPosition(actor, next);
            ActorPositionSaveService.Remember(actor.GetExtend());
            NotifyActorChanged(actor);
        }

        float travel = Vector3.Dot(hold.DragOffset, hold.Forward) / rule.DragDistance;
        float recover = Mathf.SmoothStep(0f, 1f, (elapsed - .79f) / .21f);
        hold.Cover = Mathf.SmoothStep(0f, 1f, (travel - .42f) / .55f) * (1f - recover);
        float struggle = Mathf.Sin(elapsed * 35f) * 8f * hold.Cover;
        Vector3 axis = Vector3.Cross(Vector3.forward, hold.Forward);
        Quaternion rotation = Quaternion.AngleAxis(56f * hold.Cover + struggle, axis) *
                              Quaternion.AngleAxis(struggle * .52f, hold.Forward);
        hold.Pose = hold.CaughtBody.Transformed(new BodySurfaceTransform(hold.CaughtBody.Foot,
            hold.CaughtBody.Foot + hold.DragOffset + Vector3.forward * (hold.Height * .09f * hold.Cover), rotation));

        if (hold.Arrived || travel < .985f || elapsed < .50f) return;
        hold.Arrived = true;
        ShadowBurialConfirmed?.Invoke(new ConstructedShadowBurial(hold.Running.Id, hold.Running.TargetId,
            hold.CaughtFoot, hold.Foot, clock));
    }

    // 小步也逐段检查地块和屏障，堵住时停在最后一个可站的点。
    private static bool TryDragShadow(Actor actor, Vector3 from, Vector3 desired, out Vector3 safe)
    {
        safe = from;
        float distance = Vector2.Distance(from, desired);
        if (distance < .011f) return false;
        int steps = Mathf.CeilToInt(distance / .18f);
        for (int i = 1; i <= steps; i++)
        {
            Vector3 next = Vector3.Lerp(from, desired, (float)i / steps);
            if (!simulation.CanLandActor(actor, safe, ref next) ||
                !GroundMovementAllowed(actor, safe, next, SpatialActorPose.Height(actor))) break;
            safe = next;
        }

        return safe != from;
    }

    private static bool TryShadowBurialPose(long id, out BodySurfaceBinding binding, out BodySurfacePose pose)
    {
        binding = null;
        pose = null;
        if (!heldByShadow.TryGetValue(id, out var hold) || hold.Pose == null || !hold.Lease.IsHeld ||
            hold.Binding.Actor.data?.id != id || !hold.Binding.Actor.isAlive()) return false;
        binding = hold.Binding;
        pose = hold.Pose;
        return true;
    }

    private static void ReleaseDeceasedShadowTarget(ActorExtend actor)
    {
        if (heldByShadow.TryGetValue(actor.Base.id, out var hold) && hold.Running.Target == actor.Base)
            ReleaseShadowHold(hold.Running, "TargetDied", LogicTime);
    }

    private void AppendShadowPocket(ShadowHold hold, float rise, float fade)
    {
        FormResource resource = FormResources.Load("yin_field_basin");
        Vector3 centre = hold.Origin + hold.DragOffset + hold.Forward * (hold.Height * .30f * hold.Cover);
        centre.z += .045f;
        float spread = hold.Height * (.22f + .12f * hold.Cover) * (.6f + .4f * fade);
        var scale = new Vector3(spread, spread * (1f - .27f * hold.Cover), .07f + .18f * hold.Cover);
        var pose = Matrix4x4.TRS(centre, Quaternion.LookRotation(Vector3.forward, hold.Forward), scale);
        visuals.Add(new FormVisualInstance(resource, pose, surface: "yin_basin",
            tint: new Color(.21f, .12f, .37f, (.57f + .32f * rise) * fade), terrain: terrain));
    }
}
