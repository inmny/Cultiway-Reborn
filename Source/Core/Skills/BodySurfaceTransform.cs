using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>围绕同一真实抓点移动和转动，不把矩阵逐项插值造成身体缩扁。</summary>
internal readonly struct BodySurfaceTransform
{
    /// <summary>建立时的真实抓点和当前抓点。</summary>
    internal readonly Vector3 Pivot, Point;

    /// <summary>相对建立姿态的实际转角。</summary>
    internal readonly Quaternion Rotation;

    /// <summary>从原世界姿态到当前世界姿态的刚性变换。</summary>
    internal Matrix4x4 Matrix => Matrix4x4.TRS(Point, Rotation, Vector3.one) * Matrix4x4.Translate(-Pivot);

    /// <summary>保存共同抓点和旋转。</summary>
    /// <param name="pivot">建立时抓点。</param><param name="point">当前抓点。</param><param name="rotation">相对转角。</param>
    internal BodySurfaceTransform(Vector3 pivot, Vector3 point, Quaternion rotation)
    {
        Pivot = pivot;
        Point = point;
        Rotation = rotation;
    }

    /// <summary>抓点沿同一小段移动，身体保持原大小并绕抓点连续转动。</summary>
    /// <param name="from">已确认前态。</param><param name="to">本段后态。</param><param name="at">共同进度。</param>
    internal static BodySurfaceTransform Lerp(in BodySurfaceTransform from, in BodySurfaceTransform to, float at) =>
        new(from.Pivot, Vector3.Lerp(from.Point, to.Point, at), Quaternion.Slerp(from.Rotation, to.Rotation, at));
}
