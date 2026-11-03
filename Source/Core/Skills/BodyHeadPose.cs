using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际头部的关节及作用包络，绑定后只保留数值。</summary>
public sealed class BodyHeadFit
{
    /// <summary>头部关节与接触轴，按共同体高归一化。</summary>
    public readonly Vector3 Pivot, A, B;

    /// <summary>实际头部作用半径。</summary>
    public readonly float Radius;

    /// <summary>原版图片的头形位于世界横向与高度平面，三维头形采用身体坐标。</summary>
    public readonly bool ImagePlane;

    /// <summary>原模型对头部部件额外采用的比例，不重复乘身体高度。</summary>
    public readonly float ShapeScale;

    /// <summary>共同参考头部，仅用于未绑定人物的数值取样。</summary>
    internal static readonly BodyHeadFit Reference = new(new Vector3(0, 0, .7f), new Vector3(0, 0, .8f),
        new Vector3(0, 0, .9f), .12f);

    /// <summary>保留真实关节和几何包络。</summary>
    /// <param name="pivot">头部关节。</param><param name="a">接触轴起点。</param>
    /// <param name="b">接触轴末点。</param><param name="radius">接触半径。</param>
    /// <param name="imagePlane">是否来自原版图片平面。</param>
    /// <param name="shapeScale">头部部件的原有比例。</param>
    public BodyHeadFit(Vector3 pivot, Vector3 a, Vector3 b, float radius, bool imagePlane = false,
        float shapeScale = 1f)
    {
        Pivot = pivot;
        A = a;
        B = b;
        Radius = radius;
        ImagePlane = imagePlane;
        ShapeScale = shapeScale;
    }

    /// <summary>在绑定边界换算个体比例，不在动作取样时分配。</summary>
    /// <param name="scale">实际模型与共同体高之比。</param>
    public BodyHeadFit Scaled(float scale) =>
        new(Pivot * scale, A * scale, B * scale, Radius * scale, ImagePlane, ShapeScale);

    /// <summary>保留头关节，只改变原模型已有的头形比例。</summary>
    /// <param name="scale">原部件的实际比例。</param>
    public BodyHeadFit Sized(float scale) => new(Pivot, Pivot + (A - Pivot) * scale, Pivot + (B - Pivot) * scale,
        Radius * scale, ImagePlane, ShapeScale * scale);
}

/// <summary>同轮头部姿态，接触、原版图片和三维头骨共同读取。</summary>
public readonly struct BodyHeadPose
{
    /// <summary>真实头关节与作用轴。</summary>
    public readonly Vector3 Pivot, A, B;

    /// <summary>真实头部半径。</summary>
    public readonly float Radius;

    /// <summary>相对已转体身体的共同旋转，以及用于附着形体的方向。</summary>
    public readonly Quaternion Delta, Rotation;

    /// <summary>原版头图的参考中心与关节，保留图片平面坐标。</summary>
    public readonly Vector3 NativeCenter, NativePivot;

    /// <summary>原版头部图片匹配共同头部尺寸的比例。</summary>
    public readonly float NativeScale;

    /// <summary>保留原头部部件的比例。</summary>
    public readonly float ShapeScale;

    /// <summary>绕真实头关节转动，关节与躯干一起移动，不另起显示时钟。</summary>
    /// <param name="fit">实际头部几何。</param><param name="native">原版实际头图。</param>
    /// <param name="frame">共同身体输入。</param><param name="torso">已经约束的躯干世界位移。</param>
    /// <param name="turn">身体转体。</param><param name="angles">围绕前、侧及竖直轴的相对转角。</param>
    internal BodyHeadPose(BodyHeadFit fit, BodyHeadFit native, in BodyFrame frame, Vector3 torso, float turn,
        Vector3 angles)
    {
        Vector3 forward = frame.Forward.normalized, side = Vector3.Cross(forward, Vector3.forward);

        Vector3 Map(Vector3 value) => fit.ImagePlane
            ? new Vector3(value.x, 0, value.z)
            : forward * value.x + side * value.y + Vector3.forward * value.z;

        Quaternion turned = Quaternion.AngleAxis(turn, Vector3.forward);
        Quaternion nod = Quaternion.AngleAxis(angles.z, Vector3.forward) * Quaternion.AngleAxis(angles.y, side) *
                         Quaternion.AngleAxis(angles.x, forward);
        Pivot = frame.Foot + torso + turned * Map(fit.Pivot) * frame.Height;
        A = Pivot + turned * nod * Map(fit.A - fit.Pivot) * frame.Height;
        B = Pivot + turned * nod * Map(fit.B - fit.Pivot) * frame.Height;
        Radius = fit.Radius * frame.Height;
        Delta = turned * nod * Quaternion.Inverse(turned);
        Rotation = turned * nod * Quaternion.LookRotation(forward, Vector3.forward);
        NativeCenter = (native.A + native.B) * .5f;
        NativePivot = native.Pivot;
        NativeScale = fit.Radius / native.Radius;
        ShapeScale = fit.ShapeScale;
    }
}