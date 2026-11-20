using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals.Forms;
using Cultiway.Utils.Extension;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Cultiway.Content.Skills;

/// <summary>两个真实接触成立后取得的抱持，或这一次抱持的结束。</summary>
public readonly struct ConstructedShadowGrip
{
    public readonly long CastId, TargetId;
    public readonly Vector3 LeftContact, RightContact;
    public readonly double WorldTime;
    public readonly string ReleaseReason;

    internal ConstructedShadowGrip(long castId, long targetId, Vector3 left, Vector3 right, double time, string reason)
    {
        CastId = castId;
        TargetId = targetId;
        LeftContact = left;
        RightContact = right;
        WorldTime = time;
        ReleaseReason = reason;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>确认双侧接触并取得控制，或归还本次控制时发布。</summary>
    public static event Action<ConstructedShadowGrip> ShadowGripChanged;

    private static readonly List<ShadowHold> shadowHolds = new();
    private static readonly Dictionary<long, ShadowHold> heldByShadow = new();

    private sealed class ShadowHold
    {
        internal RunningCast Running;
        internal Mesh Body, Arms;
        internal ShadowArms ArmMesh;
        internal Vector3 Origin, Forward, Torso, Foot, LeftContact, RightContact;
        internal float Height, HalfWidth;
        internal double Began, HeldAt;
        internal double? ReleasedAt;
        internal BodyControlLease Lease;
    }

    private static void AdvanceShadowHold(RunningCast running, double clock)
    {
        int node = running.Cast.Plan.ShadowHoldNode;
        if (node < 0 || running.ShadowHold?.ReleasedAt != null) return;

        Actor target = running.InitialOwner.IsLive ? CorrespondenceReceiver(running, node) : null;
        var rule = running.Cast.Plan.Nodes[node].Correspondence.ShadowHold;
        ShadowHold hold = running.ShadowHold;
        if (hold == null)
        {
            if (target == null || !running.InitialOwner.Matches(running.Cast.Impact(node).Targets, target)) return;
            BodySurfacePose pose = BodySelectionSurface(target);
            BodySurfacePose torso = pose?.SelectParts(new[] { "torso" });
            if (torso == null || torso.Parts.Length == 0) return;
            Mesh mesh = BuildShadowMesh(pose);
            if (mesh == null) return;

            Vector3 toward = pose.Foot - BodyFoot(running.Source);
            toward.z = 0f;
            Vector3 forward = toward.sqrMagnitude > .001f ? toward.normalized : Vector3.right;
            Vector3 side = Vector3.Cross(Vector3.forward, forward);
            Bounds bounds = torso.Bounds();
            hold = new ShadowHold
            {
                Running = running,
                Body = mesh,
                Arms = new Mesh { name = "Cultiway Shadow Arms" },
                ArmMesh = new ShadowArms(),
                Origin = pose.Foot,
                Foot = pose.Foot,
                Forward = forward,
                Torso = bounds.center - pose.Foot,
                Height = Mathf.Max(.35f, pose.Bounds().max.z - pose.Foot.z),
                HalfWidth = Vector3.Dot(bounds.extents, new Vector3(Mathf.Abs(side.x), Mathf.Abs(side.y), 0f)),
                Began = clock
            };
            hold.Arms.MarkDynamic();
            running.ShadowHold = hold;
            shadowHolds.Add(hold);
        }

        if (target == null)
        {
            ReleaseShadowHold(running, !running.InitialOwner.IsLive ? "SourceLost" :
                running.CorrespondenceEnded[node] ? "TargetLost" : "Ended", clock);
            return;
        }

        if (hold.Lease != null)
        {
            if (!hold.Lease.IsHeld || target.under_forces || target.isFlying() ||
                Vector3.Distance(hold.Foot, PhysicsFoot(target)) > rule.EscapeDistance)
                ReleaseShadowHold(running, "Escaped", clock);
            else if (clock - hold.HeldAt >= rule.HoldSeconds)
                ReleaseShadowHold(running, "Released", clock);
            return;
        }

        if (clock - hold.Began < rule.RiseSeconds + rule.CloseSeconds) return;
        if (!TryCloseShadow(hold, node, target, clock))
            ReleaseShadowHold(running, "Missed", clock);
    }

    /// <summary>两侧都要实碰冻结时点的原身。只在成功取得占用后停止人物行动。</summary>
    private static bool TryCloseShadow(ShadowHold hold, int node, Actor target, double clock)
    {
        if (target.isImmovable() || !target.asset.can_be_moved_by_powers || target.isFlying() ||
            target.under_forces || target.position_height > .0002f && !OnSurface(target) ||
            target.GetExtend().ForceResponseScale(hold.Running.Source,
                hold.Running.InitialOwner.Damage.AttackerPowerLevel) <= 0f) return false;

        BodySurfacePose body = BodySelectionSurface(target);
        if (body == null) return false;
        ShadowArm left = ArmAt(hold, -1f, 1f, 1f);
        ShadowArm right = ArmAt(hold, 1f, 1f, 1f);
        AbilityCapsule leftHand = left.Palm(node), rightHand = right.Palm(node);
        if (!BodySurfacePose.Find(body, body, leftHand, leftHand, "torso", out _, out var a, out _) ||
            !BodySurfacePose.Find(body, body, rightHand, rightHand, "torso", out _, out var b, out _)) return false;

        BodyControlLease lease = BodyControlState.TryAcquire(target.id, BodyOccupancy.Stance);
        if (lease == null) return false;
        hold.Lease = lease;
        hold.HeldAt = clock;
        hold.Foot = PhysicsFoot(target);
        hold.LeftContact = a.Point(body);
        hold.RightContact = b.Point(body);
        heldByShadow.Add(target.id, hold);
        target.cancelAllBeh();
        target.stopMovement();
        NotifyActorChanged(target);
        PublishShadowGrip(hold, clock, null);
        return true;
    }

    private static void ReleaseShadowHold(RunningCast running, string reason, double clock)
    {
        ShadowHold hold = running.ShadowHold;
        if (hold == null || hold.ReleasedAt.HasValue) return;
        hold.ReleasedAt = clock;
        if (hold.Lease == null) return;
        heldByShadow.Remove(running.TargetId);
        hold.Lease.Dispose();
        hold.Lease = null;
        PublishShadowGrip(hold, clock, reason);
    }

    private static void PublishShadowGrip(ShadowHold hold, double clock, string reason) =>
        ShadowGripChanged?.Invoke(new ConstructedShadowGrip(hold.Running.Id, hold.Running.TargetId,
            hold.LeftContact, hold.RightContact, clock, reason));

    private static bool TryReleaseShadowGrip(RunningCast attacker, int node, Actor target, ConstructionGrabRelease mode,
        float strength, CastAuthority caster)
    {
        if (!heldByShadow.TryGetValue(target.id, out ShadowHold grip) || grip.Running.Target != target) return false;
        RunningCast running = grip.Running;
        CastAuthority holder = running.InitialOwner;
        if (!holder.IsLive) return true;
        if (mode == ConstructionGrabRelease.Own)
        {
            if (holder.Id != caster.Id || holder.Actor != caster.Actor) return true;
        }
        else
        {
            float force = (node < 0 ? attacker.Power : attacker.Cast.RemainingForce(node)) * strength;
            float effective = force * holder.Actor.GetExtend().ForceResponseScale(caster.Actor, caster.Damage.AttackerPowerLevel);
            if (effective < running.Cast.Plan.Nodes[running.Cast.Plan.ShadowHoldNode].Correspondence.ShadowHold.Strength) return true;
        }
        ReleaseShadowHold(running, "ReleasedByAbility", LogicTime);
        return true;
    }

    private static Vector3 ShadowOrigin(ShadowHold hold, float rise) =>
        hold.Origin + hold.Forward * (hold.Height * .92f * rise);

    private static ShadowArm ArmAt(ShadowHold hold, float side, float rise, float close)
    {
        Vector3 lateral = Vector3.Cross(Vector3.forward, hold.Forward) * side;
        float height = hold.Height;
        Vector3 shoulder = ShadowOrigin(hold, rise) + Vector3.forward * (height * .63f * rise) + lateral * (height * .17f);
        Vector3 elbow = shoulder + lateral * (height * Mathf.Lerp(.31f, .15f, close)) -
                        hold.Forward * (height * Mathf.Lerp(.11f, .49f, close)) - Vector3.forward * (height * .12f);
        Vector3 reach = hold.Origin + hold.Torso + lateral * (hold.HalfWidth * .48f);
        Vector3 palm = Vector3.Lerp(elbow + lateral * (height * .13f) + Vector3.forward * (height * .09f), reach, close);
        return new ShadowArm(shoulder, elbow, palm, Mathf.Clamp(height * .17f, .1f, .48f));
    }

    private void AppendShadowHolds(double now)
    {
        if (shadowHolds.Count == 0) return;
        FormResource resource = FormResources.Load("yin_transfer_wrap");
        for (int i = shadowHolds.Count - 1; i >= 0; i--)
        {
            ShadowHold hold = shadowHolds[i];
            var plan = hold.Running.Cast.Plan;
            var rule = plan.Nodes[plan.ShadowHoldNode].Correspondence.ShadowHold;
            float leave = hold.ReleasedAt is double released ? (float)(now - released) / .42f : 0f;
            if (leave >= 1f)
            {
                RemoveShadowHold(i);
                continue;
            }
            double age = (hold.ReleasedAt ?? now) - hold.Began;
            float fade = 1f - Mathf.SmoothStep(0f, 1f, leave);
            float rise = Mathf.SmoothStep(0f, 1f, (float)age / rule.RiseSeconds) * fade;
            float close = Mathf.SmoothStep(0f, 1f, ((float)age - rule.RiseSeconds) / rule.CloseSeconds) * fade;
            Vector3 ground = ShadowOrigin(hold, rise);
            ground.z += .065f;
            Matrix4x4 pose = Matrix4x4.TRS(ground, Quaternion.AngleAxis(180f * rise, Vector3.forward),
                new Vector3(1.05f, .95f, .028f + rise * 1.02f));
            pose.m02 += hold.Forward.x * (1f - rise) * .92f;
            pose.m12 += hold.Forward.y * (1f - rise) * .92f;
            var tone = new Color(.21f, .13f, .34f, (.63f + .33f * rise) * fade);
            visuals.Add(new FormVisualInstance(resource, hold.Body, pose, tone, terrain, "yin_shadow"));
            if (rise < .45f) continue;
            hold.ArmMesh.Draw(hold.Arms, ArmAt(hold, -1f, rise, close), ArmAt(hold, 1f, rise, close));
            visuals.Add(new FormVisualInstance(resource, hold.Arms, Matrix4x4.identity,
                new Color(.31f, .22f, .48f, fade), terrain, "yin_shadow"));
        }
    }

    private static void RemoveShadowHold(int index)
    {
        ShadowHold hold = shadowHolds[index];
        Object.Destroy(hold.Body);
        Object.Destroy(hold.Arms);
        shadowHolds.RemoveAt(index);
    }

    private static void ClearShadowHolds()
    {
        while (shadowHolds.Count > 0) RemoveShadowHold(shadowHolds.Count - 1);
    }
}
