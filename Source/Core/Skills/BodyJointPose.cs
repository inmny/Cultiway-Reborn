using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实兽足按自己原骨段取得的接地姿态。</summary>
internal readonly struct BodyJointSupport
{
    /// <summary>原骨段及其已有子部件。</summary>
    internal readonly string[] Parts;
    /// <summary>同轮真实关节变化。</summary>
    internal readonly BodySurfaceTransform Motion;
    /// <summary>保存真实骨段的接地变换。</summary>
    /// <param name="parts">实际骨段。</param><param name="motion">实际变化。</param>
    internal BodyJointSupport(string[] parts, BodySurfaceTransform motion) { Parts = parts; Motion = motion; }
}

/// <summary>已采集的原骨段及其真实作用面；取样只处理数字。</summary>
public sealed class BodyJointFrame
{
    /// <summary>同一原身体、原关节和实际足部作用区域。</summary>
    internal readonly BodySurfacePose Body, Contact;
    /// <summary>实际关节、根点及承重点。</summary>
    internal readonly Vector3 Pivot, Center;
    /// <summary>身体根运动和实际关节的局部活动轴。</summary>
    internal readonly BodySurfaceTransform Root;
    /// <summary>真实局部活动坐标系。</summary>
    internal readonly Quaternion Axes;
    /// <summary>实际所选原骨段及其原有子部件。</summary>
    internal readonly string[] Parts;
    /// <summary>同一骨段的真实网格，地面限制不改用人物胶囊。</summary>
    internal readonly BodySurfaceMesh Limb;
    /// <summary>共同地表快照及当前承托结论。</summary>
    internal readonly AbilityHeightField Terrain;
    /// <summary>当前真实支撑或已受理的腾空过程允许使用本姿态。</summary>
    internal readonly bool Supported;
    /// <summary>真实承托未成立的具体原因，便于定位不可用的身体请求。</summary>
    internal readonly string Rejection;

    /// <summary>本身体各足的真实贴地变化。</summary>
    internal readonly BodyJointSupport[] Supports;
    /// <summary>发力足起势时已经采用的真实贴地转角。</summary>
    internal readonly BodySurfaceTransform Reference;

    /// <summary>同一实际身体的自然姿态及原关节交接规则。</summary>
    internal readonly BodySurfacePose Natural;
    /// <summary>原父子连接，收势只处理数字。</summary>
    internal readonly BodyJointSkeleton Skeleton;

    /// <summary>保存同轮冻结的实际身体与承托；不持有游戏对象。</summary>
    /// <param name="body">原身体。</param><param name="contact">真实作用区域。</param>
    /// <param name="limb">实际骨段网格。</param><param name="parts">真实骨段选择。</param>
    /// <param name="pivot">原关节。</param><param name="axes">原活动坐标系。</param>
    /// <param name="center">原承重点。</param><param name="root">共同根运动。</param>
    /// <param name="terrain">共同地表。</param><param name="supported">实际承托结果。</param>
    /// <param name="supports">各足自己的接地变化。</param><param name="reference">发力足的真实起势转角。</param>
    /// <param name="natural">同一实际身体的自然姿态。</param><param name="skeleton">实际关节交接规则。</param>
    /// <param name="rejection">实际承托失败原因。</param>
    internal BodyJointFrame(BodySurfacePose body, BodySurfacePose contact, BodySurfaceMesh limb, string[] parts,
        Vector3 pivot, Quaternion axes, Vector3 center, BodySurfaceTransform root, AbilityHeightField terrain, bool supported,
        BodyJointSupport[] supports, BodySurfaceTransform reference, BodySurfacePose natural, BodyJointSkeleton skeleton, string rejection = null)
    { Body = body; Contact = contact; Limb = limb; Parts = parts; Pivot = pivot; Axes = axes; Center = center; Root = root; Terrain = terrain; Supported = supported; Supports = supports; Reference = reference; Natural = natural; Skeleton = skeleton; Rejection = rejection; }
}

/// <summary>原骨段围绕真实关节的同轮姿态，完整身体与作用面共用它。</summary>
public sealed class BodyJointPose
{
    /// <summary>真实关节和承重点。</summary>
    public readonly Vector3 Pivot, Center, Point;
    /// <summary>同轮实际关节方向。</summary>
    public readonly Quaternion Rotation;
    /// <summary>实际身体、真实作用面和当前承托。</summary>
    internal readonly BodySurfacePose Surface, Contact;
    /// <summary>实际支撑或已经受理的腾空过程允许本姿态时才作用。</summary>
    internal readonly bool Supported;

    /// <summary>本动作已有收势阶段接回自然姿态的进度。</summary>
    public readonly float NaturalWeight;
    /// <summary>同一采集轮次的自然目标。</summary>
    internal readonly BodySurfacePose Natural;

    /// <summary>先放置原身体，再由地面限制真实骨段转角，最后共同更新身体与作用面。</summary>
    /// <param name="frame">已经冻结的实际身体。</param><param name="angles">同一动作曲线的三个局部转角。</param>
    /// <param name="naturalWeight">已有收势阶段的交接进度。</param>
    internal BodyJointPose(BodyJointFrame frame, Vector3 angles, float naturalWeight)
    {
        Matrix4x4 root = frame.Root.Matrix;
        Pivot = root.MultiplyPoint3x4(frame.Pivot); Center = root.MultiplyPoint3x4(frame.Center);
        Quaternion initial = frame.Reference.Rotation * frame.Root.Rotation;
        Quaternion axes = initial * frame.Axes;
        Quaternion delta = axes * Quaternion.Euler(angles) * Quaternion.Inverse(axes);
        var before = new BodySurfaceTransform(frame.Pivot, Pivot, initial);
        var desired = new BodySurfaceTransform(frame.Pivot, Pivot, delta * initial);
        if (angles.sqrMagnitude > 0f && BodySurfaceSweep.Find(frame.Limb, before, desired, frame.Terrain, Array.Empty<BodySurfaceObstacle>(), out var hit))
            delta = Quaternion.Slerp(Quaternion.identity, delta, hit.Fraction);
        Rotation = delta * axes; Supported = frame.Supported;
        var motion = new BodySurfaceTransform(Pivot, Pivot, delta);
        BodySurfacePose body = frame.Body.Transformed(frame.Root);
        foreach (BodyJointSupport support in frame.Supports) body = body.TransformParts(support.Parts, support.Motion);
        NaturalWeight = Mathf.Clamp01(naturalWeight); Natural = frame.Natural;
        BodySurfacePose controlled = body.TransformParts(frame.Parts, motion);
        Surface = frame.Skeleton.Blend(controlled, Natural, NaturalWeight);
        int matrix = frame.Skeleton.MatrixFor(frame.Parts[0]);
        Matrix4x4 handoff = Surface.Matrices[matrix] * controlled.Matrices[matrix].inverse;
        Pivot = handoff.MultiplyPoint3x4(Pivot);
        Rotation = handoff.rotation * Rotation;
        Center = frame.Skeleton.Center(Surface);
        Contact = new BodySurfacePose(frame.Contact.Parts, Surface.Matrices, Surface.Foot, Surface.Height, Surface.Heading);
        // 抓点坐标系沿原足面固定位置转动，不能随旋转后的包围盒中心漂移。
        Point = (Surface.Matrices[matrix] * frame.Body.Matrices[matrix].inverse).MultiplyPoint3x4(frame.Contact.Bounds().center);
    }
}
