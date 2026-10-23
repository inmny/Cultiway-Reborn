using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>路径坐标的实际参照，不随镜头改变。</summary>
public enum ConstructionPathSpace
{
    /// <summary>相对开始飞行时的局部三个方向，数值为地图格。</summary>
    Local = 0,

    /// <summary>横坐标沿开始位置至当时目标的位置，零至一表示全程；其余为横向及离地偏移。</summary>
    TargetLine = 1
}

/// <summary>分段三次曲线；它提供中心运动之外的真实偏移，不绘制独立轨迹线。</summary>
public sealed class ConstructionPath
{
    /// <summary>路径使用的实际坐标参照。</summary>
    public ConstructionPathSpace Space;
    /// <summary>有序关键点，位置与两侧控制柄均使用相同坐标参照。</summary>
    public ConstructionPathKey[] Keys;
    /// <summary>完整走完后是否循环；循环路径必须闭合。</summary>
    public bool Loop;
    /// <summary>形体朝向实际速度时采用的局部前向；空值表示保留作者旋转。</summary>
    public float[] Forward = { 0f, 0f, 1f };
    /// <summary>朝向实际运动的每秒最大转角，避免尖折处突然跳转。</summary>
    public float TurnRate = 720f;
}

/// <summary>路径的一个实际时点；柄的位置相对本点，不是另一个世界位置。</summary>
public sealed class ConstructionPathKey
{
    /// <summary>相对开始飞行的秒数；首点必须为零。</summary>
    public float At;
    /// <summary>此时的真实偏移。</summary>
    public float[] Point = new float[3];
    /// <summary>进入本点的局部控制柄。</summary>
    public float[] In = new float[3];
    /// <summary>离开本点的局部控制柄。</summary>
    public float[] Out = new float[3];
}

/// <summary>完成检查与多项式预计算的不可变路径，工作线程只读取数值。</summary>
internal sealed class CompiledConstructionPath
{
    /// <summary>目标相对坐标的明确最大跨度，覆盖当前地图与世界级构造。</summary>
    internal const float MaximumTargetSpan = 10000f;
    /// <summary>有限曲段及其起止时点。</summary>
    private readonly Segment[] segments;
    /// <summary>实际路径参照。</summary>
    internal readonly ConstructionPathSpace Space;
    /// <summary>完整曲线的秒数。</summary>
    internal readonly float Duration;
    /// <summary>是否闭合循环。</summary>
    internal readonly bool Loop;
    /// <summary>局部前向，零表示不改变作者朝向。</summary>
    internal readonly Vector3 Forward;
    /// <summary>朝向实际运动的转动上限。</summary>
    internal readonly float TurnRate;
    /// <summary>各轴二阶导数绝对值的共同上界，用于限制真实曲线的取样误差。</summary>
    internal readonly Vector3 AccelerationBound;
    /// <summary>曲段数，用于批量任务估量。</summary>
    internal int Count => segments.Length;

    /// <summary>在资料边界一次核对并复制全部必要数值。</summary>
    /// <param name="source">可编辑的原始路径。</param>
    /// <param name="path">错误对应的构造位置。</param>
    internal CompiledConstructionPath(ConstructionPath source, string path)
    {
        if (!Enum.IsDefined(typeof(ConstructionPathSpace), source.Space) || source.Keys == null || source.Keys.Length < 2 || source.Keys.Length > 65 ||
            !Finite(source.TurnRate) || source.TurnRate < 0f || source.TurnRate > 7200f)
            throw new InvalidDataException(path + "：路径需要2至65个有序关键点及有效参照和转向预算");
        Space = source.Space; Loop = source.Loop; TurnRate = source.TurnRate;
        Forward = source.Forward == null ? Vector3.zero : Vector(source.Forward, path + "/Forward");
        if (source.Forward != null && (Forward.sqrMagnitude < .000001f || !Finite(Forward.sqrMagnitude) || TurnRate == 0f))
            throw new InvalidDataException(path + "：朝向路径需要非零前向和明确的转动速度");
        Forward = Forward.normalized;
        var points = new Vector3[source.Keys.Length];
        var incoming = new Vector3[points.Length]; var outgoing = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            ConstructionPathKey key = source.Keys[i];
            if (key == null || !Finite(key.At) || key.At < 0f || key.At > 3600f ||
                i == 0 && key.At != 0f || i > 0 && key.At - source.Keys[i - 1].At < .001f)
                throw new InvalidDataException(path + "/Keys[" + i + "]：时点须从零递增，相邻至少0.001秒");
            points[i] = Vector(key.Point, path); incoming[i] = Vector(key.In, path); outgoing[i] = Vector(key.Out, path);
        }
        if (points[0] != Vector3.zero || Loop && points[points.Length - 1] != points[0])
            throw new InvalidDataException(path + "：路径必须从零偏移连续开始，循环路径还必须闭合");
        Duration = source.Keys[points.Length - 1].At;
        segments = new Segment[points.Length - 1];
        Vector3 bound = Vector3.zero;
        for (int i = 0; i < segments.Length; i++)
        {
            segments[i] = new Segment(source.Keys[i].At, source.Keys[i + 1].At,
                points[i], points[i] + outgoing[i], points[i + 1] + incoming[i + 1], points[i + 1]);
            bound = Vector3.Max(bound, segments[i].AccelerationBound);
        }
        float maximumAcceleration = bound.x * (Space == ConstructionPathSpace.TargetLine ? MaximumTargetSpan : 1f) + bound.y + bound.z;
        if (!Finite(maximumAcceleration) || maximumAcceleration > 80000f)
            throw new InvalidDataException(path + "：路径曲率超出每秒两千次取样预算，请延长历时或减小弯曲");
        AccelerationBound = bound;
    }

    /// <summary>采样同一曲线的偏移与解析速度；非循环末端保留偏移，中心线运动继续。</summary>
    /// <param name="age">已经飞行的秒数。</param>
    /// <param name="position">局部真实偏移。</param>
    /// <param name="velocity">局部偏移每秒变化。</param>
    internal void Sample(float age, out Vector3 position, out Vector3 velocity)
    {
        if (!Loop && age >= Duration)
        { segments[segments.Length - 1].Sample(Duration, out position, out _); velocity = Vector3.zero; return; }
        float time = Loop ? Mathf.Repeat(age, Duration) : Mathf.Max(0f, age);
        if (Loop && time >= Duration) time = 0f;
        segments[Find(time)].Sample(time, out position, out velocity);
    }

    /// <summary>下一段拐点或循环边界之前允许推进的秒数。</summary>
    /// <param name="age">当前施放秒数。</param>
    /// <param name="startedAt">开始飞行的施放时点。</param>
    internal float UntilBoundary(float age, float startedAt)
    {
        double local = Math.Max(0d, (double)age - startedAt);
        if (!Loop && local >= Duration) return float.PositiveInfinity;
        double cycle = Loop ? Math.Floor(local / Duration) : 0d;
        int first = Find((float)(local - cycle * Duration));
        for (int i = first; i < segments.Length; i++)
        {
            float boundary = (float)(startedAt + cycle * Duration + segments[i].Until);
            if (boundary > age) return boundary - age;
        }
        return Loop ? (float)(startedAt + (cycle + 1d) * Duration + segments[0].Until) - age : float.PositiveInfinity;
    }

    /// <summary>按已排序的曲段寻找当前段，不访问原始资料。</summary>
    /// <param name="time">已经映射到本周期的时间。</param>
    private int Find(float time)
    {
        int lo = 0, hi = segments.Length - 1;
        while (lo < hi) { int mid = (lo + hi) / 2; if (time >= segments[mid].Until) lo = mid + 1; else hi = mid; }
        return lo;
    }

    /// <summary>资料边界读取三个有限数值。</summary>
    /// <param name="value">原始坐标。</param>
    /// <param name="path">错误路径。</param>
    private static Vector3 Vector(float[] value, string path)
    {
        if (value?.Length != 3 || !Finite(value[0]) || !Finite(value[1]) || !Finite(value[2]))
            throw new InvalidDataException(path + "：路径坐标需要三个有限数值");
        if (Mathf.Max(Mathf.Abs(value[0]), Mathf.Abs(value[1]), Mathf.Abs(value[2])) > 10000f)
            throw new InvalidDataException(path + "：路径坐标及控制柄超过一万格的制作预算");
        return new Vector3(value[0], value[1], value[2]);
    }

    /// <summary>检查外部浮点数。</summary>
    /// <param name="value">待检查的数值。</param>
    private static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);

    /// <summary>预先换算为三次多项式的有限曲段。</summary>
    private readonly struct Segment
    {
        /// <summary>实际秒数起止。</summary>
        internal readonly float At, Until;
        /// <summary>三次、二次、一次及常数系数。</summary>
        private readonly Vector3 a, b, c, d;
        /// <summary>秒数到单位进度的倍率。</summary>
        private readonly float speed;
        /// <summary>该曲段的各轴加速度绝对上界。</summary>
        internal readonly Vector3 AccelerationBound;

        /// <summary>把四个实际控制点预编译。</summary>
        /// <param name="at">开始秒数。</param>
        /// <param name="until">结束秒数。</param>
        /// <param name="p0">起点。</param>
        /// <param name="p1">离开起点的控制点。</param>
        /// <param name="p2">进入终点的控制点。</param>
        /// <param name="p3">终点。</param>
        internal Segment(float at, float until, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            At = at; Until = until; speed = 1f / (until - at);
            a = -p0 + 3f * p1 - 3f * p2 + p3; b = 3f * p0 - 6f * p1 + 3f * p2; c = 3f * (p1 - p0); d = p0;
            Vector3 first = 2f * b * speed * speed, last = (6f * a + 2f * b) * speed * speed;
            AccelerationBound = new Vector3(Mathf.Max(Mathf.Abs(first.x), Mathf.Abs(last.x)),
                Mathf.Max(Mathf.Abs(first.y), Mathf.Abs(last.y)), Mathf.Max(Mathf.Abs(first.z), Mathf.Abs(last.z)));
        }

        /// <summary>位置与解析导数共用相同实际进度。</summary>
        /// <param name="time">当前秒数。</param>
        /// <param name="position">偏移。</param>
        /// <param name="velocity">每秒变化。</param>
        internal void Sample(float time, out Vector3 position, out Vector3 velocity)
        {
            float t = Mathf.Clamp01((time - At) * speed);
            position = ((a * t + b) * t + c) * t + d;
            velocity = ((3f * a * t + 2f * b) * t + c) * speed;
        }
    }
}
