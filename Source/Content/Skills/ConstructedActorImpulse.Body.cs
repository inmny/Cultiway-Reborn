using System;
using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>离手身体沿用原受力所有权，姿态与环境接触交给共同步骤。</summary>
internal static partial class ConstructedActorImpulse
{
    /// <summary>一次真实离手；持有原表面和现有控制，不持有来源施放。</summary>
    internal sealed class BodyFlight
    {
        /// <summary>同一原版受力记录。</summary>
        internal readonly Motion Motion;
        /// <summary>原施放及来源身份。</summary>
        internal readonly long CastId, SourceId;
        /// <summary>离手前已经取得的同一控制。</summary>
        internal readonly BodyControlLease Lease;
        /// <summary>实际资源、原姿态与真实几何。</summary>
        internal readonly BodySurfaceBinding Binding;
        /// <summary>抓住时的完整表面。</summary>
        internal readonly BodySurfacePose Basis;
        /// <summary>真实可见的冻结几何。</summary>
        internal readonly BodySurfaceMesh Geometry;
        /// <summary>同一中心及角速度的重力轨迹。</summary>
        internal readonly BodyRigidFlight Flight;
        /// <summary>实际抓持时冻结的撞击归属与质量。</summary>
        internal readonly BodyImpactSource ImpactSource;
        /// <summary>已经确认的完整姿态。</summary>
        internal BodySurfacePose Surface;
        /// <summary>真实触地后继续使用原身体及原控制。</summary>
        internal BodyGroundRecovery Ground;
        /// <summary>真实接手时采集的肢链，后续起身不重新读取自然动画。</summary>
        internal readonly BodyRecoveryRig Rig;

        /// <summary>接收已确认的运动与原控制；不重新抢占身体。</summary>
        /// <param name="motion">原版受力记录。</param><param name="cast">原施放。</param><param name="source">原来源。</param>
        /// <param name="lease">原控制。</param><param name="binding">同一资源。</param><param name="basis">原表面。</param>
        /// <param name="geometry">同一几何。</param><param name="flight">离手轨迹。</param><param name="impact">冻结撞击来源。</param><param name="recovery">已经采集的恢复肢链。</param>
        internal BodyFlight(Motion motion, long cast, long source, BodyControlLease lease, BodySurfaceBinding binding,
            BodySurfacePose basis, BodySurfaceMesh geometry, BodyRigidFlight flight, BodyImpactSource impact, BodyRecoveryRig recovery = null)
        {
            Motion = motion; CastId = cast; SourceId = source; Lease = lease; Binding = binding;
            Basis = basis; Geometry = geometry; Flight = flight;
            ImpactSource = impact;
            Rig = recovery ?? ConstructedAbilityService.CaptureRecoveryRig(binding, basis);
            Binding.IsPinned = Binding.PinsWholeBody = true;
            Surface = basis.Transformed(flight.Sample(0d));
        }

        /// <summary>根点和表面来自同一时点，不按原版直立身体重新取样。</summary>
        /// <param name="time">共同时间。</param>
        internal BodySurfacePose At(double time) => Ground != null
            ? Ground.At(time, currentTerrain ?? Motion.Terrain)
            : Basis.Transformed(Flight.Sample((Motion.SlideUntil > 0d ? Math.Min(time, Motion.SlideUntil) : time) - Motion.StartedAt));

        /// <summary>根点真实速度包含绕中心转动的份额。</summary>
        /// <param name="time">共同时间。</param>
        internal Vector3 RootVelocity(double time)
        {
            if (Ground != null) return Ground.Velocity + Vector3.Cross(Ground.Angular, Surface.Foot - Surface.RigidTransform.Point);
            double age = time - Motion.StartedAt;
            return Flight.Arc.Speed(age) + Vector3.Cross(Flight.AngularVelocity,
                At(time).Foot - Flight.Arc.Position(age));
        }
    }

    /// <summary>实际离手直接交接原凭据和确认速度，原控制中间没有空档。</summary>
    /// <param name="actor">真实目标。</param><param name="cast">原施放。</param><param name="source">原来源。</param>
    /// <param name="lease">原凭据。</param><param name="binding">同一资源。</param><param name="basis">原表面。</param>
    /// <param name="geometry">冻结几何。</param><param name="pose">离手时确认姿态。</param>
    /// <param name="velocity">中心线速度。</param><param name="angular">世界角速度。</param>
    /// <param name="time">真实离手时间。</param><param name="terrain">共同地表。</param><param name="impact">冻结撞击来源。</param>
    internal static void BeginBody(Actor actor, long cast, long source, BodyControlLease lease, BodySurfaceBinding binding,
        BodySurfacePose basis, BodySurfaceMesh geometry, BodySurfaceTransform pose, Vector3 velocity, Vector3 angular,
        double time, AbilityHeightField terrain, BodyImpactSource impact)
    {
        float massGravity = Mathf.Min(actor.stats["mass"] * SimGlobals.m.gravity, 20f);
        var flight = new BodyRigidFlight(geometry.Center, pose, velocity, angular, .3f * massGravity * massGravity);
        var motion = new Motion { Actor = new WeakReference<Actor>(actor), ActorId = actor.id, Terrain = terrain,
            StartedAt = time, SampledAt = time, SpeedScale = 1f, MassGravity = massGravity, Arc = flight.Arc };
        motion.Body = new BodyFlight(motion, cast, source, lease, binding, basis, geometry, flight, impact);
        actor.stopMovement();
        actor.under_forces = true;
        actor.velocity_speed = 1f;
        motions[actor.id] = motion;
        CommitBody(motion.Body, time, terrain);
        ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(motion.Body,
            ConstructedBodyFlightState.Released, time, null, null, 0));
    }

    /// <summary>共同步骤只枚举仍由本次实际受力持有的身体。</summary>
    internal static IEnumerable<BodyFlight> Bodies
    {
        get
        {
            foreach (Motion motion in motions.Values)
                if (motion.Body != null) yield return motion.Body;
        }
    }

    /// <summary>在实际人物索引中直接取得离手身体。</summary>
    /// <param name="id">人物身份。</param><param name="body">实际运动。</param>
    internal static bool TryBody(long id, out BodyFlight body)
    {
        body = motions.TryGetValue(id, out Motion motion) ? motion.Body : null;
        return body != null;
    }

    /// <summary>每轮采集前核对实际控制；外部接管不能被旧飞行覆盖。</summary>
    internal static void ValidateBodies()
    {
        foreach (BodyFlight body in Bodies)
        {
            Motion motion = body.Motion;
            bool alive = motion.Actor.TryGetTarget(out Actor actor) && actor.isAlive();
            string reason = !alive ? "TargetLost" : !body.Lease.IsHeld || !Matches(actor, motion) ? "ExternalControl" :
                body.Binding.ChangedReason ?? (actor.isImmovable() ? "Immovable" : null);
            if (reason == null) continue;
            bool owns = alive && body.Lease.IsHeld && Matches(actor, motion);
            motions.TryRemove(motion.ActorId, out _);
            body.Lease.Dispose();
            if (owns) actor.stopForce();
            ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(body,
                ConstructedBodyFlightState.Cancelled, motion.SampledAt, reason, null, 0));
        }
    }

    /// <summary>原版受力回调先发现外部接管时，也只归还原凭据并报告一次结束。</summary>
    /// <param name="motion">已从当前受力记录移除的旧运动。</param><param name="reason">实际原因。</param>
    private static void CancelBodyOwnership(Motion motion, string reason)
    {
        BodyFlight body = motion.Body;
        if (body == null) return;
        body.Lease.Dispose();
        ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(body,
            ConstructedBodyFlightState.Cancelled, motion.SampledAt, reason, null, 0));
    }

    /// <summary>共同时间确认后才写原版位置、速度及完整表面。</summary>
    /// <param name="body">实际身体。</param><param name="time">确认时间。</param><param name="terrain">共同地表。</param>
    internal static void CommitBody(BodyFlight body, double time, AbilityHeightField terrain)
    {
        Motion motion = body.Motion;
        motion.Actor.TryGetTarget(out Actor actor);
        bool rising = body.Ground?.Rising == true;
        body.Surface = body.Ground != null ? body.Ground.Commit(time, terrain) : body.At(time);
        bool slideEnded = body.Ground == null && motion.SlideUntil > 0d && time >= motion.SlideUntil;
        if (slideEnded)
        {
            var hit = new BodySurfaceHit(0f, new Vector3(body.Surface.Foot.x, body.Surface.Foot.y,
                BodyLocomotion.Floor(terrain, body.Surface.Foot, body.Surface.Foot.z + .0001f, body.Basis.Height * .16f)), Vector3.forward, "SlideEnded");
            body.Ground = new BodyGroundRecovery(body, in hit, time, stopped: true);
            motion.SlideUntil = 0d;
        }
        Vector3 root = body.Surface.Foot, speed = body.RootVelocity(time);
        actor.current_position = root;
        actor.setCurrentTile(Toolbox.getTileAt(root.x, root.y));
        actor.position_height = root.z - Ground(terrain, root);
        actor.velocity = new Vector3(speed.x, speed.y, motion.MassGravity > 0f ? speed.z / motion.MassGravity : 0f);
        actor.dirty_current_tile = true;
        actor.checkIsInLiquid();
        actor.updatePos();
        motion.SampledAt = time; motion.Terrain = terrain;
        motion.ExpectedPosition = actor.current_position; motion.ExpectedHeight = actor.position_height;
        motion.ExpectedVelocity = actor.velocity;
        if (slideEnded) ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(body,
            ConstructedBodyFlightState.Rolling, time, "SlideEnded", body.Surface.Foot, 0));
        if (body.ImpactSource.Generation != ConstructedAbilityService.BodyWorldGeneration) return;
        if (body.Ground != null)
        {
            if (!rising && body.Ground.Rising) ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(body,
                ConstructedBodyFlightState.Recovering, time, "GroundRecovery", null, 0));
            if (body.Ground.Finished) ReturnGroundBody(body, actor, time, terrain);
        }
    }

    /// <summary>只有同一恢复真正站稳后才归还；回调中的新运动不会被旧结束覆盖。</summary>
    /// <param name="body">同一身体。</param><param name="actor">仍有效的原人物。</param>
    /// <param name="time">确认时间。</param><param name="terrain">当前地表。</param>
    private static void ReturnGroundBody(BodyFlight body, Actor actor, double time, AbilityHeightField terrain)
    {
        Motion motion = body.Motion;
        if (!motions.TryGetValue(motion.ActorId, out Motion current) || !ReferenceEquals(current, motion) ||
            !Matches(actor, motion) || !body.Lease.IsHeld) return;
        motions.TryRemove(motion.ActorId, out _);
        body.Lease.Dispose();
        float height = Mathf.Max(0f, body.Surface.Foot.z - Ground(terrain, body.Surface.Foot));
        actor.stopForce(); actor.position_height = height;
        actor.target_angle.z = 0f; actor.updateRotation();
        Vector3 forward = new(Mathf.Cos(body.Surface.Heading), Mathf.Sin(body.Surface.Heading), 0f);
        actor.lookTowardsPosition(actor.current_position + (Vector2)forward);
        if (height > .0001f) RestoreSurface(actor, body.Surface.Foot.z, terrain);
        ConstructedAbilityService.BodyFlightReturned(actor, time);
        BodyActionBinding.FacingReleased?.Invoke(actor, forward);
        if (body.ImpactSource.Generation != ConstructedAbilityService.BodyWorldGeneration) return;
        ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(body,
            ConstructedBodyFlightState.Returned, time, "Recovered", null, 0));
    }

    /// <summary>先消费当前运动，再结算真实撞伤；伤害回调的新运动不会被旧归还覆盖。</summary>
    /// <param name="body">已经回到接触时点的身体。</param><param name="hit">实际环境。</param><param name="time">共同时间。</param>
    internal static void ContactBody(BodyFlight body, BodySurfaceHit hit, double time)
    {
        if (body.Ground != null) return;
        Motion motion = body.Motion;
        if (!motions.TryGetValue(motion.ActorId, out Motion current) || !ReferenceEquals(current, motion)) return;
        if (!motion.Actor.TryGetTarget(out Actor actor) || !Matches(actor, motion))
        {
            motions.TryRemove(motion.ActorId, out _);
            CancelBodyOwnership(motion, "ExternalControl");
            return;
        }
        var contact = new ConstructedBodyFlight(body, ConstructedBodyFlightState.Contact, time,
            hit.Reason, hit.Point, hit.Owner);
        // 在标准受击回调之前消费旧运动，重复候选和回调重入均不能重复结算。
        motions.TryRemove(motion.ActorId, out _);
        actor.stopForce();
        Vector2 position = actor.current_position;
        float height = actor.position_height;
        double age = time - motion.StartedAt;
        if (!ConstructedAbilityService.ResolveBodyImpact(body.ImpactSource, hit, body.Flight.Arc.Position(age),
                body.Flight.Arc.Speed(age), body.Flight.AngularVelocity, time)) { body.Lease.Dispose(); return; }
        if (actor.data?.id == motion.ActorId && actor.isAlive() && !actor.under_forces && body.Lease.IsHeld &&
            !motions.ContainsKey(motion.ActorId) && actor.current_position == position && actor.position_height == height &&
            body.Binding.ChangedReason == null && hit.Normal.z > .5f)
        {
            body.Ground = new BodyGroundRecovery(body, in hit, time);
            actor.under_forces = true; actor.velocity_speed = 1f;
            motions[motion.ActorId] = motion;
            CommitBody(body, time, motion.Terrain);
            ConstructedAbilityService.ReportBodyFlight(contact);
            if (!body.Lease.IsHeld || body.ImpactSource.Generation != ConstructedAbilityService.BodyWorldGeneration ||
                !motions.TryGetValue(motion.ActorId, out Motion owned) || !ReferenceEquals(owned, motion)) return;
            ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(body,
                ConstructedBodyFlightState.Rolling, time, "GroundContact", hit.Point, hit.Owner));
            return;
        }
        body.Lease.Dispose();
        if (actor.data?.id == motion.ActorId && actor.isAlive() && !actor.under_forces &&
            !motions.ContainsKey(motion.ActorId) && actor.current_position == position && actor.position_height == height)
        {
            actor.position_height = Mathf.Max(0f, actor.position_height);
            if (actor.position_height > 0f) BeginDirected(actor, Vector3.zero, 0f, motion.Terrain);
            ConstructedAbilityService.BodyFlightReturned(actor, time);
        }
        ConstructedAbilityService.ReportBodyFlight(contact);
        if (body.ImpactSource.Generation != ConstructedAbilityService.BodyWorldGeneration) return;
        ConstructedAbilityService.ReportBodyFlight(new ConstructedBodyFlight(body,
            ConstructedBodyFlightState.Returned, time, hit.Reason, hit.Point, hit.Owner));
    }
}
