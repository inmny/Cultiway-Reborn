using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Visuals.Forms;

/// <summary>局部 XZ 平面上的有厚度扇形。方向从正 Z 朝正 X 量角。</summary>
public sealed class FormContactSector
{
    public float Radius, Angle, Direction, HalfThickness;
}

/// <summary>局部 XZ 平面上的开放路径。Starts 可指定各段开始作用的成形进度。</summary>
public sealed class FormContactPath
{
    public float[][] Points;
    public float HalfWidth, HalfThickness;
    public float[] Starts;
}

/// <summary>制作坐标 XZ 的逆时针凸轮廓，沿 Y 两侧保留同一厚度。</summary>
public sealed class FormContactFootprint
{
    /// <summary>每项为相对于作用面中心的 X、Z。</summary>
    public float[][] Points;
    /// <summary>实际局部半厚度。</summary>
    public float HalfThickness;
}

/// <summary>加载形体时拆出有限的凸片；各片的身份不会随成形进度变化。</summary>
internal static class FormContactRegions
{
    /// <summary>单段圆弧最多五度。向内收缩为半径的 1 - cos(2.5°)，小于千分之一。</summary>
    internal const int ArcStepDegrees = 5;

    internal static void Add(List<FormContactBinding> contacts, FormContactDescription source, FormRig rig, string control)
    {
        var sector = source.Sector;
        var path = source.Path;
        var footprint = source.Footprint;
        if (sector == null && path == null && footprint == null)
        {
            contacts.Add(new FormContactBinding(source, rig, control));
            return;
        }

        if (sector != null && path != null || footprint != null && (sector != null || path != null) || source.HalfExtents != null || source.B != null || source.Radius != 0f ||
            source.BControl != null || source.BInfluences != null || source.BuildRange != null || source.BuildAlong)
            throw Invalid(source, "区域不能同时声明其他作用形状或成形区间");
        if (sector != null) AddSector(contacts, source, rig, control, sector);
        else if (path != null) AddPath(contacts, source, rig, control, path);
        else contacts.Add(new FormContactBinding(source, rig, control, ConvexOutline(source, footprint), footprint.HalfThickness));
    }

    /// <summary>只在加载时核对凸边；顺序、重边或交叉错误不能产生比模型更大的接触面。</summary>
    /// <param name="source">资源作用定义。</param><param name="footprint">制作导出的轮廓。</param>
    private static Vector2[] ConvexOutline(FormContactDescription source, FormContactFootprint footprint)
    {
        if (footprint.Points?.Length is not (>= 3 and <= 12) || !Positive(footprint.HalfThickness))
            throw Invalid(source, "实体轮廓须有3至12个点，并指定正半厚");
        int count = footprint.Points.Length;
        var points = new Vector2[count];
        for (int i = 0; i < count; i++)
        {
            float[] point = footprint.Points[i];
            if (point?.Length != 2 || !FormRig.Finite(point[0]) || !FormRig.Finite(point[1]))
                throw Invalid(source, "每个轮廓点需要有限的 X、Z 坐标");
            points[i] = new Vector2(point[0], point[1]);
        }
        for (int i = 0; i < count; i++)
        {
            Vector2 edge = points[(i + 1) % count] - points[i];
            for (int j = 2; j < count; j++)
            {
                Vector2 other = points[(i + j) % count] - points[i];
                if (edge.x * other.y - edge.y * other.x <= 0f)
                    throw Invalid(source, "实体轮廓须按逆时针顺序形成凸形，无重叠或平直折点");
            }
        }
        return points;
    }

    private static void AddSector(List<FormContactBinding> contacts, FormContactDescription source, FormRig rig, string control, FormContactSector sector)
    {
        if (!Positive(sector.Radius) || !Positive(sector.HalfThickness) || !Positive(sector.Angle) || sector.Angle > 360f ||
            !FormRig.Finite(sector.Direction)) throw Invalid(source, "扇形需要正半径、正半厚和 0 到 360 度之间的开角");

        int count = Mathf.CeilToInt(sector.Angle / 90f);
        float span = sector.Angle / count;
        float first = 90f - sector.Direction % 360f - sector.Angle * .5f;
        int edges = Mathf.CeilToInt(span / ArcStepDegrees);
        for (int part = 0; part < count; part++)
        {
            var outline = new Vector2[edges + 2];
            for (int edge = 0; edge <= edges; edge++)
            {
                float angle = (first + span * (part + (float)edge / edges)) * Mathf.Deg2Rad;
                outline[edge + 1] = sector.Radius * new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
            contacts.Add(new FormContactBinding(source, rig, control, outline, sector.HalfThickness));
        }
    }

    private static void AddPath(List<FormContactBinding> contacts, FormContactDescription source, FormRig rig, string control, FormContactPath path)
    {
        if (path.Points == null || path.Points.Length < 2 || path.Points.Length > 65 || !Positive(path.HalfWidth) ||
            !Positive(path.HalfThickness) || path.Starts != null && path.Starts.Length != path.Points.Length - 1)
            throw Invalid(source, "路径需要 2 到 65 个点、正半宽和正半厚；有成形进度时每段一个");

        var points = new Vector2[path.Points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            var point = path.Points[i];
            if (point?.Length != 2 || !FormRig.Finite(point[0]) || !FormRig.Finite(point[1]))
                throw Invalid(source, "路径点需要有限的 X、Z 坐标");
            points[i] = new Vector2(point[0], point[1]);
        }

        float previousStart = 0f;
        Vector2 previousSide = default;
        for (int i = 0; i + 1 < points.Length; i++)
        {
            Vector2 a = points[i], b = points[i + 1], direction = b - a;
            float length = direction.magnitude, start = path.Starts?[i] ?? 0f;
            if (!Positive(length) || !FormRig.Finite(start) || start < previousStart || start > 1f)
                throw Invalid(source, "路径不能有零长段，成形进度应从 0 到 1 依次递增或保持");
            Vector2 side = new Vector2(-direction.y, direction.x) * (path.HalfWidth / length);
            contacts.Add(new FormContactBinding(source, rig, control, new[] { a - side, b - side, b + side, a + side },
                path.HalfThickness, start));
            float turn = i == 0 ? 0f : previousSide.x * side.y - previousSide.y * side.x;
            // 只接外角，并跟随后一段开始。弯内跨过两段的大片空间保持为空。
            if (turn != 0f)
            {
                Vector2[] join = turn > 0f ? new[] { a, a - previousSide, a - side } : new[] { a, a + side, a + previousSide };
                contacts.Add(new FormContactBinding(source, rig, control, join, path.HalfThickness, start));
            }
            previousStart = start;
            previousSide = side;
        }
    }

    private static bool Positive(float value) => FormRig.Finite(value) && value > .000001f;
    private static InvalidDataException Invalid(FormContactDescription source, string message) => new(source.Id + "：" + message);
}
