using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>随完成姿态发布的局部表面变化；不产生独立形体、接触或伤害。</summary>
public readonly struct FormSurfaceFeedback
{
    /// <summary>同一地图空间中的实际表面位置。</summary>
    public readonly Vector3 Point;

    /// <summary>只影响附近表面的世界半径。</summary>
    public readonly float Radius;

    /// <summary>已由共同世界时间确定的零至一强度。</summary>
    public readonly float Strength;

    /// <summary>记录已确认的局部表面资料，绘制不自行计时。</summary>
    /// <param name="point">当前表面位置。</param><param name="radius">世界半径。</param>
    /// <param name="strength">当前强度。</param>
    public FormSurfaceFeedback(Vector3 point, float radius, float strength)
    {
        Point = point; Radius = radius; Strength = strength;
    }

    /// <summary>两端只加与实际几何相同的显示偏移。</summary>
    /// <param name="shift">本视图的身体及地面偏移。</param>
    internal Vector4 Region(Vector3 shift) => new(Point.x + shift.x, Point.y + shift.y, Point.z + shift.z, Radius);
}
