using System;
using System.IO;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>另一只手的共享腕点，两个呈现各自保留自己的肩肘。</summary>
internal readonly struct BodySupportingGrip
{
    /// <summary>两套臂链共用的腕点。</summary>
    internal readonly Vector3 Hand;

    /// <summary>抓住杆身时的腕部朝向。</summary>
    internal readonly Quaternion Rotation;

    /// <summary>原版与三维各自计算的实际臂链。</summary>
    internal readonly BodyArmPose Arm;
    internal readonly BodyArmPose? Adapted;

    /// <summary>保存同一腕点和相应骨架，不再由绘制端补算。</summary>
    /// <param name="hand">真实腕点。</param><param name="rotation">真实手部朝向。</param>
    /// <param name="arm">原版臂链。</param><param name="adapted">三维臂链。</param>
    internal BodySupportingGrip(Vector3 hand, Quaternion rotation, BodyArmPose arm, BodyArmPose? adapted)
    {
        Hand = hand;
        Rotation = rotation;
        Arm = arm;
        Adapted = adapted;
    }
}

public sealed partial class CompiledBodyAction
{
    /// <summary>同一次动作确有第二个实际杆握点。</summary>
    internal bool UsesSupportingGrip => Construction.SupportGrip != null;

    /// <summary>左手腕点相对右手的位移，在器物未转动的单位体高坐标中。</summary>
    private readonly Vector3 supportWristLocal;

    /// <summary>左手从杆的另一面托住，手内挂点继续贴合杆轴。</summary>
    private static readonly Quaternion supportRoll = Quaternion.AngleAxis(180f, Vector3.forward);

    /// <summary>先检查托手的杆点及两手间距，再换算两处手内位置之间的腕点偏移。</summary>
    /// <param name="resource">真实兵器。</param><param name="hand">共用手形。</param>
    /// <param name="primary">主手握点。</param><param name="slide">若有滑握时的主手另一端。</param>
    /// <param name="weaponLocal">主手握持变换。</param><param name="handLocal">手形相对腕点的变换。</param>
    private Vector3 CompileSupportingGrip(FormResource resource, FormResource hand, FormGrip primary, string slide,
        Matrix4x4 weaponLocal, Matrix4x4 handLocal)
    {
        const string path = "Body/Construction/Relations/Grip";
        var support = ConstructionReferences.Resolve(path, () => resource.Grip(Construction.SupportGrip),
            "Grip", resource.Description.Id + " / " + Construction.SupportGrip);
        RequireShaft(resource, primary, support, path);
        int anchor = Array.FindIndex(hand.Anchors, binding => binding.Id == Construction.SupportHandGrip);
        if (anchor < 0) throw new InvalidDataException(path + "：左手缺少实际手内握点");
        Vector3 at = support.Transform.GetColumn(3), start = primary.Transform.GetColumn(3);
        Vector3 closest = start;
        if (slide != null)
        {
            var along = (Vector3)resource.Grip(slide).Transform.GetColumn(3) - start;
            closest += along * Mathf.Clamp01(Vector3.Dot(at - start, along) / along.sqrMagnitude);
        }
        if (Vector3.Distance(closest, at) * WeaponSize < .08f)
            throw new InvalidDataException(path + "：两手的实际握持不能在同一处交错");
        Vector3 socket = handLocal.MultiplyPoint3x4(hand.Anchors[anchor].Transform.GetColumn(3));
        return weaponLocal.MultiplyPoint3x4(at) - supportRoll * socket;
    }

    /// <summary>将左右两手的真实臂长归入同一根杆的主腕点，再一次求共同可达位置。</summary>
    /// <param name="frame">同轮实际身体比例。</param><param name="desired">主手请求腕点。</param>
    /// <param name="shoulder">主手原版肩点。</param><param name="adaptedRight">主手三维肩点。</param>
    /// <param name="left">左手原版肩点。</param><param name="adaptedLeft">左手三维肩点。</param>
    /// <param name="gripDelta">两手共用枪杆决定的腕点差。</param><param name="pole">对称时的弯曲方向。</param>
    /// <param name="wrist">两手都能到达时，求出的主腕点。</param>
    private static bool ProjectGrip(in BodyFrame frame, Vector3 desired, Vector3 shoulder, Vector3 adaptedRight,
        Vector3 left, Vector3 adaptedLeft, Vector3 gripDelta, Vector3 pole, out Vector3 wrist)
    {
        var projector = new BodyReachProjector();
        float height = frame.Height;
        float upper = frame.UpperLength / height, lower = frame.LowerLength / height;
        projector.AddArm(Vector3.zero, upper, lower, BodyJointSolver.Extension);
        projector.AddArm((left - shoulder - gripDelta) / height, UpperArm, LowerArm, BodyJointSolver.Extension);
        if (frame.Fit != null)
            projector.AddArm((adaptedRight - shoulder) / height, frame.Fit.Upper, frame.Fit.Lower, .96f);
        if (frame.SupportFit != null)
            projector.AddArm((adaptedLeft - shoulder - gripDelta) / height, frame.SupportFit.Upper, frame.SupportFit.Lower, .96f);
        bool available = projector.Project((desired - shoulder) / height, pole, null, 0f, out Vector3 point);
        wrist = shoulder + point * height;
        return available;
    }

    /// <summary>从同一杆点解出原版及三维左臂，保持前一帧肘弯方向。</summary>
    /// <param name="frame">本轮实际体型。</param><param name="shoulder">原版肩点。</param>
    /// <param name="adaptedShoulder">三维肩点。</param><param name="wrist">同一腕点。</param>
    /// <param name="rotation">主手朝向。</param><param name="pole">左臂自然弯向。</param>
    /// <param name="changedBasis">本步身体方向变化。</param><param name="previous">此前托握姿态。</param>
    private static BodySupportingGrip SolveSupportingGrip(in BodyFrame frame, Vector3 shoulder,
        Vector3 adaptedShoulder, Vector3 wrist, Quaternion rotation, Vector3 pole, Quaternion changedBasis,
        BodySupportingGrip? previous)
    {
        float upper = UpperArm * frame.Height, lower = LowerArm * frame.Height;
        BodyArmPose arm = SolveArm(shoulder, wrist, pole, frame.Forward, changedBasis, upper, lower, frame.Height,
            previous?.Hand, previous?.Arm);
        BodyArmPose? adapted = frame.SupportFit == null ? null : FitArm(frame.SupportFit,
            adaptedShoulder, wrist, pole, frame.Forward, changedBasis, frame.Height,
            previous?.Hand, previous?.Adapted);
        return new BodySupportingGrip(wrist, rotation * supportRoll, arm, adapted);
    }

    /// <summary>用三维实际臂长解出安装在指定腕点的当前肩肘。</summary>
    /// <param name="fit">真实三维手臂。</param><param name="shoulder">当前肩点。</param><param name="wrist">共同腕点。</param>
    /// <param name="pole">原始弯向。</param><param name="forward">本身朝向。</param><param name="changedBasis">本步方向变化。</param>
    /// <param name="height">实际体高。</param><param name="previousWrist">前次腕点。</param><param name="previousArm">前次臂链。</param>
    private static BodyArmPose FitArm(BodyFit fit, Vector3 shoulder, Vector3 wrist, Vector3 pole, Vector3 forward,
        Quaternion changedBasis, float height, Vector3? previousWrist, BodyArmPose? previousArm) =>
        SolveArm(shoulder, wrist, pole, forward, changedBasis, fit.Upper * height, fit.Lower * height,
            fit.HeightScale * height, previousWrist, previousArm);

    /// <summary>腕点已通过共同可达检查，沿真实两段臂长求连续肘弯。</summary>
    /// <param name="shoulder">当前肩点。</param><param name="wrist">共同腕点。</param><param name="pole">原始弯向。</param>
    /// <param name="forward">本身朝向。</param><param name="changedBasis">本步方向变化。</param>
    /// <param name="upper">上臂长。</param><param name="lower">前臂长。</param><param name="height">该呈现的体高。</param>
    /// <param name="previousWrist">前次腕点。</param><param name="previousArm">前次臂链。</param>
    private static BodyArmPose SolveArm(Vector3 shoulder, Vector3 wrist, Vector3 pole, Vector3 forward,
        Quaternion changedBasis, float upper, float lower, float height, Vector3? previousWrist, BodyArmPose? previousArm)
    {
        Vector3 prior = forward;
        Vector3? bend = null;
        if (previousArm.HasValue)
        {
            BodyArmPose last = previousArm.Value;
            Vector3 old = previousWrist.Value - last.Shoulder;
            prior = changedBasis * (old / old.magnitude);
            bend = changedBasis * last.Bend;
        }
        BodyJointSolver.Solve(shoulder, wrist, upper, lower, pole, prior, bend, 0f, -1f, -1f,
            out Vector3 elbow, out _, out Vector3 actualBend);
        return new BodyArmPose(shoulder, elbow, actualBend, upper, lower, height);
    }
}
