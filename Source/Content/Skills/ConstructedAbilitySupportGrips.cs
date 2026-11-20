using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>构造部位真正抓到或放开人物的事实。</summary>
public readonly struct ConstructedSupportGrip
{
    /// <summary>施放、目标及各自的实际部位。</summary>
    public readonly long CastId, TargetId;
    public readonly int Node, Support;
    /// <summary>世界时点和解除原因；开始时没有解除原因。</summary>
    public readonly double WorldTime;
    public readonly string ReleaseReason;

    /// <summary>仅在确认过程中发布真实结果。</summary>
    internal ConstructedSupportGrip(long castId, long targetId, int node, int support, double time, string reason)
    {
        CastId = castId; TargetId = targetId; Node = node; Support = support;
        WorldTime = time; ReleaseReason = reason;
    }
}

public sealed partial class ConstructedAbilityService
{
    /// <summary>只有真正取得或归还控制时发布。</summary>
    public static event Action<ConstructedSupportGrip> SupportGripChanged;

    /// <summary>一个人物最多被一条支撑抓持；与徒手抓持使用相同的站姿控制。</summary>
    private static readonly Dictionary<long, SupportedGrip> supportGrips = new();

    private sealed class SupportedGrip
    {
        internal RunningCast Cast;
        internal int Node;
        internal Actor Target;
        internal long TargetId;
        internal BodyControlLease Lease;
        internal Vector3 Foot;
        internal float ForceScale;
        internal double LastContact;
    }

    /// <summary>用真实接触取得控制；后续每次物理接触才推挤，并重新确认可到达的脚下。</summary>
    private static void ConfirmSupportGrip(in ContactChoice hit)
    {
        RunningCast running = hit.Left.Running;
        int node = hit.Left.To.Node;
        if (node < 0 || running.Cast.Plan.Nodes[node].SupportGrip is not { } rule ||
            !running.Cast.HasGripSupport(node) || !running.Cast.GripPartActive(node) || !running.InitialOwner.IsLive) return;
        Actor target = hit.Actor.Actor;
        long id = hit.Actor.Id;
        if (target.data?.id != id || !target.isAlive() || !target.hasHealth() || target.isImmovable() ||
            !target.asset.can_be_moved_by_powers || target.isFlying() || target.under_forces || target.position_height > .0002f && !OnSurface(target)) return;
        double clock = simulation.contactTime;
        if (supportGrips.TryGetValue(id, out var grip))
        {
            if (grip.Cast != running || grip.Node != node || grip.Target != target) return;
        }
        else
        {
            if (running.UsedGripBranches?.Contains(node) == true) return;
            float scale = target.GetExtend().ForceResponseScale(running.Source, running.InitialOwner.Damage.AttackerPowerLevel);
            if (scale <= 0f) return;
            BodyControlLease lease = BodyControlState.TryAcquire(id, BodyOccupancy.Stance);
            if (lease == null) return;
            (running.UsedGripBranches ??= new()).Add(node);
            grip = new SupportedGrip { Cast = running, Node = node, Target = target, TargetId = id, Lease = lease,
                Foot = PhysicsFoot(target), LastContact = clock, ForceScale = scale };
            (running.SupportGrips ??= new()).Add(grip);
            supportGrips.Add(id, grip);
            target.cancelAllBeh();
            target.stopMovement();
            NotifyActorChanged(target);
            SupportGripChanged?.Invoke(new ConstructedSupportGrip(running.Id, id, node,
                running.Cast.Plan.Nodes[node].Parent, clock, null));
            if (!lease.IsHeld || running.Cast.Finished) return;
        }

        grip.LastContact = clock;
        Vector3 foot = PhysicsFoot(target);
        Vector2 direction = hit.Normal;
        if (direction.sqrMagnitude < .0001f) direction = (Vector2)(foot - hit.Point);
        Vector3 next = foot + (Vector3)(direction.normalized * rule.Pressure * Mathf.Clamp01(grip.ForceScale));
        if (simulation.CanLandActor(target, foot, ref next))
        {
            PublishBodyPosition(target, next);
            NotifyActorChanged(target);
            foot = next;
        }
        grip.Foot = foot;
    }

    /// <summary>每个已确认时间边界归还失效控制；从无抓持的施放不分配状态。</summary>
    private static void UpdateSupportGrips(RunningCast running, double clock)
    {
        if (running.SupportGrips == null) return;
        for (int i = running.SupportGrips.Count - 1; i >= 0; i--)
        {
            var grip = running.SupportGrips[i];
            Actor target = grip.Target;
            float interval = running.Cast.Plan.ContactPolicies[running.Cast.ContactGroup(grip.Node)].Interval;
            string reason = !running.InitialOwner.IsLive ? "SourceLost" : running.Cast.Finished ? "Finished" :
                !running.Cast.HasGripSupport(grip.Node) ? "SupportLost" : !running.Cast.GripPartActive(grip.Node) ? "Released" :
                target.data?.id != grip.TargetId || !target.isAlive() || !target.hasHealth() ? "TargetLost" :
                !grip.Lease.IsHeld || target.under_forces || target.isFlying() ||
                Vector3.Distance(grip.Foot, PhysicsFoot(target)) > running.Cast.Plan.Nodes[grip.Node].SupportGrip.EscapeDistance ? "Escaped" :
                clock - grip.LastContact > Math.Max(.25d, interval * 1.75d) ? "Separated" : null;
            if (reason != null) ReleaseSupportGrip(grip, reason, clock);
        }
    }

    /// <summary>只归还这一条抓持自己的控制。</summary>
    private static void ReleaseSupportGrip(SupportedGrip grip, string reason, double clock)
    {
        if (!grip.Cast.SupportGrips.Remove(grip)) return;
        supportGrips.Remove(grip.TargetId);
        grip.Lease.Dispose();
        SupportGripChanged?.Invoke(new ConstructedSupportGrip(grip.Cast.Id, grip.TargetId, grip.Node,
            grip.Cast.Cast.Plan.Nodes[grip.Node].Parent, clock, reason));
    }

    /// <summary>换世界、替换及结束不会把旧人物继续留在抓持中。</summary>
    private static void ReleaseSupportGrips(RunningCast running, string reason)
    {
        if (running.SupportGrips == null) return;
        while (running.SupportGrips.Count > 0)
            ReleaseSupportGrip(running.SupportGrips[running.SupportGrips.Count - 1], reason, running.LastWorldTime);
    }

    /// <summary>普通释放或救援词条同样作用于真实的支撑抓持。</summary>
    private static bool TryReleaseSupportGrip(RunningCast attacker, int node, Actor target, ConstructionGrabRelease mode,
        float strength, CastAuthority caster)
    {
        if (!supportGrips.TryGetValue(target.id, out var grip) || grip.Target != target) return false;
        var holder = grip.Cast.InitialOwner;
        if (!holder.IsLive) return true;
        if (mode == ConstructionGrabRelease.Own)
        {
            if (holder.Id != caster.Id || holder.Actor != caster.Actor) return true;
        }
        else
        {
            float force = (node < 0 ? attacker.Power : attacker.Cast.RemainingForce(node)) * strength;
            if (force * holder.Actor.GetExtend().ForceResponseScale(caster.Actor, caster.Damage.AttackerPowerLevel) <
                grip.Cast.Cast.Plan.Nodes[grip.Node].SupportGrip.Strength) return true;
        }
        ReleaseSupportGrip(grip, "ReleasedByAbility", LogicTime);
        return true;
    }
}
