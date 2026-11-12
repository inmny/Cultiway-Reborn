using System.Collections.Generic;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

/// <summary>普通持物只接管真实握持臂，不创建目标施法或整体搬动目标。</summary>
public sealed partial class ConstructedAbilityService
{
    /// <summary>只在真实取得右臂后登记，表面查询可直接找到这次联系。</summary>
    private static readonly Dictionary<long, BodyConnection> passiveWeaponBodies = new();

    /// <summary>接触时的原物品、真实臂长，以及确认和预测各自的纯数值姿态。</summary>
    private sealed class PassiveWeaponArm
    {
        /// <summary>原持有人对应的实际身体及物品采集入口。</summary>
        internal BodySurfaceBinding Binding;

        /// <summary>接触时原物品的真实布局及形状，不能换成另一件临时模型。</summary>
        internal BodySurfacePose Original;

        /// <summary>接触时原腕点；物品始终继承该握点发生的实际位移。</summary>
        internal BodyPose Initial;

        /// <summary>已经确认及尚未提交的真实臂链。</summary>
        internal BodyPose Pose, PredictedPose;

        /// <summary>已经确认及尚未提交的完整表面。</summary>
        internal BodySurfacePose Surface, PredictedSurface;

        /// <summary>真实表面接触点相对原腕点的世界偏移，普通持物只平移不凭空转手。</summary>
        internal Vector3 ContactOffset;

        /// <summary>原实际两段骨长及建立时的体高。</summary>
        internal float Upper, Lower, Height;

        /// <summary>确认和预测的共同时间，回退不会改写确认态。</summary>
        internal double Time, PredictedTime;

        /// <summary>采集入口已经不能提供原臂链时解除，不以其他身体代替。</summary>
        internal string InvalidReason;

        /// <summary>连续查询读取同一预测步，绘制只取得已经确认的表面。</summary>
        /// <param name="time">需要的共同时间。</param>
        internal BodySurfacePose At(double time) => PredictedSurface != null && PredictedTime > Time && time > Time
            ? BodySurfacePose.Lerp(Surface, PredictedSurface, Mathf.Clamp01((float)((time - Time) / (PredictedTime - Time))))
            : Surface;

        /// <summary>手臂由真实接触点约束，肩继续跟随人物原身体，超出臂长也不会拖回根点。</summary>
        /// <param name="point">本步请求的真实接触点。</param><param name="time">共同末端。</param>
        internal void Predict(Vector3 point, double time)
        {
            BodySurfacePose basis = Binding.At(time);
            BodyPose? captured = Binding.HeldArm(basis);
            if (!captured.HasValue) { InvalidReason = "UnavailableArm"; return; }
            BodyPose natural = captured.Value;
            Vector3 previous = Pose.Hand - natural.Shoulder;
            BodyJointSolver.Solve(natural.Shoulder, point - ContactOffset, Upper, Lower, Pose.Bend,
                previous.normalized, Pose.Bend, previous.magnitude, -1f, -1f,
                out Vector3 elbow, out Vector3 hand, out Vector3 bend, extension: 1f);
            PredictedPose = new BodyPose(basis.Foot, natural.Shoulder, elbow, hand, Initial.Rotation, Height,
                Vector3.zero, 0f, natural.Forward, (float)(time - Time), bend);
            BodySurfacePose arm = Binding.ExternalArm(basis, natural, PredictedPose);
            PredictedSurface = Binding.ExternalHeld(arm, Original, hand - Initial.Hand);
            PredictedTime = time;
        }

        /// <summary>共同步确认后才发布姿态，预测不占用或释放身体。</summary>
        /// <param name="time">本次确认时点。</param>
        internal void Commit(double time)
        {
            if (PredictedSurface == null || PredictedTime != time) return;
            Pose = PredictedPose;
            Surface = PredictedSurface;
            Time = time;
        }
    }

    /// <summary>来源数值推进完成后，普通持物跟随同一时点的真实接触点。</summary>
    /// <param name="time">共同预测末端。</param>
    private static void PredictPassiveWeapons(double time)
    {
        foreach (var pair in passiveWeaponBodies)
        {
            BodyConnection connection = pair.Value;
            Vector3 point = connection.Relation.Kind == BodyRelationKind.Deflect &&
                            Vector3.Dot(connection.SourcePoint - connection.Point, connection.Normal) >= 0f
                ? connection.SourcePoint : connection.Point;
            connection.PassiveWeapon.Predict(point, time);
        }
    }

    /// <summary>预测完成后统一确认，目标没有额外施法或独立动作时钟。</summary>
    /// <param name="time">共同确认时点。</param>
    private static void CommitPassiveWeapons(double time)
    {
        foreach (var pair in passiveWeaponBodies) pair.Value.PassiveWeapon.Commit(time);
    }
}
