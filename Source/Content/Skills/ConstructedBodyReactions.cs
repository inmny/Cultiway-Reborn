using System;
using Cultiway.Content.Combat;
using Cultiway.Core;
using Cultiway.Core.EventSystem.Events;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>正式击退从原身体接收姿态和原控制，外部直接改动仍由原归还规则处理。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>只在主线程接手时读取当前正式姿态；原版像素臂和模型臂使用各自同一轮关节。</summary>
    /// <param name="binding">实际身体。</param><param name="basis">已确认表面。</param>
    internal static BodyRecoveryRig CaptureRecoveryRig(BodySurfaceBinding binding, BodySurfacePose basis)
    {
        BodyPose? stance = null, right = null, left = null;
        foreach (RunningCast running in casts)
        {
            if (running.SourceId != binding.Id || !running.BodyControlled) continue;
            if (running.Cast.Plan.Body.ControlsStance) stance = running.Cast.BodyPose;
            if (!running.Cast.Plan.Body.Part.IsArm) continue;
            if (running.Cast.Plan.Body.Part.Left) left = running.Cast.BodyPose;
            else right = running.Cast.BodyPose;
        }
        return binding.RecoveryRig(basis, stance, right, left);
    }

    /// <summary>只在实际受力已经受理后调用，失败保留原有中断和普通运动。</summary>
    /// <param name="actor">实际受击人物。</param><param name="motion">已经按抗性确定的新运动。</param>
    /// <param name="prior">仍在飞行或恢复的原身体，其他情况为空。</param>
    internal static ConstructedActorImpulse.BodyFlight TakeBodyForCombat(Actor actor,
        ConstructedActorImpulse.Motion motion, ConstructedActorImpulse.BodyFlight prior)
    {
        BodySurfaceBinding binding;
        BodySurfacePose basis;
        BodySurfaceMesh geometry;
        BodyControlLease lease;
        BodySurfaceTransform pose;
        Vector3 angular;
        long cast;
        BodyRecoveryRig recovery;
        if (prior != null)
        {
            if (!prior.Lease.IsHeld || prior.Binding.ChangedReason != null ||
                Vector2.Distance(prior.Surface.Foot, actor.current_position) > .0001f) return null;
            binding = prior.Binding; basis = prior.Basis; geometry = prior.Geometry; lease = prior.Lease;
            recovery = prior.Rig;
            if (prior.Ground != null && prior.Rig != null)
            {
                basis = prior.Surface; geometry = new BodySurfaceMesh(basis);
                pose = new BodySurfaceTransform(geometry.Center, geometry.Center, Quaternion.identity);
                recovery = prior.Ground.Rebase(basis);
            }
            else pose = prior.Surface.RigidTransform;
            angular = prior.Ground?.Angular ?? prior.Flight.AngularVelocity;
            cast = prior.CastId;
            Vector3 shift = motion.Arc.Origin - prior.Surface.Foot;
            pose = new BodySurfaceTransform(pose.Pivot, pose.Point + shift, pose.Rotation);
        }
        else
        {
            RunningCast source = null;
            foreach (RunningCast running in casts)
                if (running.SourceId == actor.id && running.BodyControlled && running.Cast.ControlsBody &&
                    running.Cast.Plan.Body.ControlsStance && running.BodyLease.IsHeld && running.Body.IsCurrent(actor) &&
                    Vector2.Distance(running.Cast.BodyPose.Value.Foot, actor.current_position) < .0001f)
                { source = running; break; }
            if (source == null || !PrepareBodySurface(actor)) return null;
            binding = bodySurfaces[actor.id];
            binding.Capture(source.Cast.BodyPose.Value.Foot, source.LastSourceHeight, LogicTime);
            basis = BodySurfaceAt(actor.id, LogicTime);
            if (basis == null) return null;
            // 新受力保留真实当前位置；原动作只贡献同一时点的身体姿态。
            Vector3 shift = motion.Arc.Origin - basis.Foot;
            basis = basis.Transformed(new BodySurfaceTransform(basis.Foot, basis.Foot + shift, Quaternion.identity));
            geometry = new BodySurfaceMesh(basis);
            lease = source.BodyLease; cast = source.Id;
            pose = new BodySurfaceTransform(geometry.Center, geometry.Center, Quaternion.identity);
            angular = Vector3.zero;
            recovery = CaptureRecoveryRig(binding, basis);
            source.BodyControlled = false; source.BodyLease = null;
            source.Cast.InterruptBody();
            InterruptBodyConnections(actor);
        }
        // 恢复自身不追加碰撞伤害，原攻击的伤害和传力仍在正式入口各结算一次。
        GetHitEvent damage = CombatDamageEffects.PrepareDamage(actor, 0f, ElementComposition.Static.empty);
        var impact = new BodyImpactSource(cast, actor.id, actor.id, worldGeneration, damage, actor.stats["mass"], 0f);
        Vector3 centerVelocity = motion.Arc.Velocity - Vector3.Cross(angular,
            pose.Matrix.MultiplyPoint3x4(basis.Foot) - pose.Matrix.MultiplyPoint3x4(geometry.Center));
        var flight = new BodyRigidFlight(geometry.Center, pose, centerVelocity, angular, motion.Arc.Gravity);
        return new ConstructedActorImpulse.BodyFlight(motion, cast, actor.id, lease, binding, basis, geometry, flight, impact, recovery);
    }
}
