using Cultiway.Content.Combat;
using Cultiway.Core.Skills;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>兵器接触约束双方已有动作，真实持有及身体控制仍由原所有者维护。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>先核对实际器物和双方动作，再在已确认的接触阶段抵住，不重复申请目标身体。</summary>
    /// <param name="hit">共同时间确认的器物表面接触。</param>
    /// <param name="rule">本次双方关系。</param>
    private static void ResolveWeaponConnection(in ContactChoice hit, in CompiledBodyRelation rule)
    {
        RunningCast owner = hit.Left.Running;
        Actor target = hit.Actor.Actor;
        RunningCast opponent = null;
        foreach (RunningCast candidate in casts)
            if (candidate.BodyControlled && candidate.SourceId == hit.Actor.Id &&
                candidate.Body.Weapon != null && EquippedWeaponVisualService.IsCurrent(target, candidate.Body.Weapon))
            {
                opponent = candidate;
                break;
            }

        BodySurfaceBinding binding = bodySurfaces[hit.Actor.Id];
        BodyPose? passiveArm = opponent == null ? binding.HeldArm(hit.BodySurface) : null;
        string reason = null;
        if (!owner.BodyControlled || owner.Cast.Finished || target.data?.id != hit.Actor.Id ||
            !target.isAlive() || !target.hasHealth()) reason = "InvalidBody";
        else if (owner.Body.Weapon != null && !EquippedWeaponVisualService.IsCurrent(owner.Source, owner.Body.Weapon))
            reason = "SourceWeaponLost";
        else if (opponent == null && !passiveArm.HasValue) reason = "UnavailableArm";
        else if (opponent?.Connection?.Active == true || opponent?.WeaponConstraint?.Active == true ||
                 owner.WeaponConstraint?.Active == true || passiveWeaponBodies.ContainsKey(hit.Actor.Id)) reason = "Occupied";
        else if (target.GetExtend().ForceResponseScale(owner.Source, owner.InitialOwner.Damage.AttackerPowerLevel) <= 0f)
            reason = "Resistance";
        BodyControlLease lease = reason == null && opponent == null
            ? BodyControlState.TryAcquire(hit.Actor.Id, BodyOccupancy.RightArm, passiveArm.Value.Forward) : null;
        if (reason == null && opponent == null && lease == null) reason = "Occupied";
        if (reason != null)
        {
            BodyConnectionChanged?.Invoke(new ConstructedBodyConnection(owner.Id, owner.SourceId, hit.Actor.Id,
                rule, ConstructedBodyConnectionState.Rejected, hit.Point, hit.Point, LogicTime,
                owner.StartedAt + rule.UntilWorldTime, 0f, reason));
            return;
        }

        BodyPose sourcePose = owner.Cast.BodyPose.Value, targetPose = opponent?.Cast.BodyPose ?? passiveArm.Value;
        owner.Connection = new BodyConnection
        {
            Owner = owner, Relation = rule, Target = target, TargetId = hit.Actor.Id,
            WeaponTarget = opponent, WeaponTargetAge = opponent?.Cast.Age ?? 0f,
            TargetLease = lease, SurfaceBinding = binding,
            PassiveWeapon = opponent != null ? null : new PassiveWeaponArm
            {
                Binding = binding, Original = hit.BodySurface, Initial = targetPose, Pose = targetPose,
                Surface = hit.BodySurface, Height = targetPose.Height, Time = LogicTime,
                Upper = Vector3.Distance(targetPose.Shoulder, targetPose.Elbow),
                Lower = Vector3.Distance(targetPose.Elbow, targetPose.Hand), ContactOffset = hit.Point - targetPose.Hand
            },
            WeaponTargetLocal = Quaternion.Inverse(targetPose.EffectorRotation) * (hit.Point - targetPose.EffectorPoint),
            SourceAsset = owner.Source.asset, TargetAsset = target.asset,
            Point = hit.Point, Local = Quaternion.Inverse(sourcePose.EffectorRotation) * (hit.Point - sourcePose.EffectorPoint),
            SourceShape = hit.Left.To.Shape,
            PoseAge = owner.Cast.Age, Active = true, LastTime = LogicTime,
            Normal = hit.Normal, Radius = hit.Left.To.Radius,
            Until = owner.StartedAt + rule.UntilWorldTime,
            Remaining = Mathf.Max(0f, rule.UntilWorldTime - owner.Cast.Age) * rule.ControlStrength
        };
        // 起点也采用同一真实形状内的数值，不能把首次微小校正当成主动收手。
        owner.Connection.Point = owner.Connection.SourcePoint;
        if (opponent != null) opponent.WeaponConstraint = owner.Connection;
        else passiveWeaponBodies.Add(hit.Actor.Id, owner.Connection);
        owner.Cast.ConfirmContact(-1, 1f, hit.Point, target.id);
        owner.Cast.BodyConstraintChanged();
        opponent?.Cast.BodyConstraintChanged();
        BodyConnectionChanged?.Invoke(owner.Connection.Fact(ConstructedBodyConnectionState.Active, LogicTime));
        // 来招选择格挡后的恢复时，先解除相抵，再让其沿自身真实姿态收回。
        if (opponent?.Cast.RespondToBodyResult(BodyActionResult.Blocked) == true)
            ReleaseBodyConnection(owner, "BodyResponse", fade: false);
    }

    /// <summary>双方时钟和预算照常推进；换器、中断、移动脱离或维持结束只解除本关系。</summary>
    /// <param name="owner">实际抵住的一方。</param>
    /// <param name="time">共同确认的世界时间。</param>
    private static void UpdateWeaponConnection(RunningCast owner, double time)
    {
        BodyConnection connection = owner.Connection;
        RunningCast opponent = connection.WeaponTarget;
        string reason = null;
        if (!owner.BodyControlled || owner.Cast.Finished) reason = "OwnerBodyEnded";
        else if (opponent != null && (!opponent.BodyControlled || opponent.Cast.Finished)) reason = "TargetBodyEnded";
        else if (owner.Source.data?.id != owner.SourceId || !owner.Source.isAlive() ||
                 connection.Target.data?.id != connection.TargetId || !connection.Target.isAlive()) reason = "TargetLost";
        else if (!owner.Body.IsValid(owner.Source) || opponent != null && !opponent.Body.IsValid(connection.Target)) reason = "WeaponChanged";
        else if (connection.PassiveWeapon != null && connection.SurfaceBinding.ChangedReason is string changed) reason = changed;
        else if (connection.PassiveWeapon?.InvalidReason is string invalid) reason = invalid;
        else if (connection.PassiveWeapon != null && connection.Target.under_forces) reason = "ExternalForce";
        else if (owner.Source.asset != connection.SourceAsset || connection.Target.asset != connection.TargetAsset)
            reason = "BodyChanged";
        else if (time >= connection.Until - .0000001d) reason = "PhaseEnded";
        else
        {
            float scale = connection.Target.GetExtend()
                .ForceResponseScale(owner.Source, owner.InitialOwner.Damage.AttackerPowerLevel);
            if (scale <= 0f) reason = "Resistance";
            else connection.Remaining = Mathf.Max(0f,
                connection.Remaining - (float)System.Math.Max(0d, time - connection.LastTime) / scale);
            if (reason == null && connection.Remaining <= .000001f) reason = "Exhausted";
            if (reason == null && connection.Relation.Kind == BodyRelationKind.Deflect &&
                Vector3.Dot(connection.SourcePoint - connection.Point, connection.Normal) < -owner.LastSourceHeight * .00001f)
                reason = "Withdrawn";
            if (reason == null && Vector3.Distance(connection.SourcePoint, connection.TargetPoint) >
                owner.LastSourceHeight * .0001f) reason = connection.Relation.Kind == BodyRelationKind.Deflect ? "OutOfReach" : "Separated";
            if (reason == null && connection.Relation.Kind == BodyRelationKind.Deflect) connection.Point = connection.TargetPoint;
        }

        connection.LastTime = time;
        if (reason != null) ReleaseBodyConnection(owner, reason, fade: false, time: time);
    }
}
