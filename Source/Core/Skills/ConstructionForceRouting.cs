using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际来力可以分配到的出口，不按招式名字决定。</summary>
public enum ConstructionForceDestination
{
    /// <summary>由当前人物承受，但改变方向。</summary>
    Body = 0,

    /// <summary>分给真正接触且位于指定方向的人物。</summary>
    Contacts = 1,

    /// <summary>卸入确实承托身体的地表或模型表面。</summary>
    Support = 2,

    /// <summary>沿本次已经确认的身体接触返回来源，到达后终止。</summary>
    Source = 3
}

/// <summary>一条承力分支；份额以本段输入为准，不复制给每个接触人物。</summary>
public sealed class ConstructionForceRoute
{
    /// <summary>真实出口。</summary>
    public ConstructionForceDestination Destination;

    /// <summary>分配本段来力的比例，各分支合计不超过一。</summary>
    public float Share = .5f;

    /// <summary>相对来力方向的前、侧、上分量；空值保持方向，支撑出口自动朝向实际落脚点。</summary>
    [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
    public float[] Direction;

    /// <summary>接触人物相对分配方向的最大夹角，单位为度。</summary>
    public float Angle = 45f;
}

/// <summary>有限传力规则，同样用于攻击传导和身体承力。</summary>
public sealed class ConstructionForceRouting
{
    /// <summary>实际执行顺序。</summary>
    public ConstructionForceRoute[] Routes;

    /// <summary>最多经过的人物连接数，零只允许当前人物改向或卸入支撑。</summary>
    public int MaximumDepth = 4;

    /// <summary>一次接触链最多包含的人物，含原目标。</summary>
    public int MaximumActors = 16;
}

/// <summary>动作有效期间处理来力的有限能力；额度由整次施放共享。</summary>
public sealed class BodyForceResponse
{
    /// <summary>抗性处理前最多改向或转移的速度变化总量。</summary>
    public float Capacity = 10f;

    /// <summary>明确的来力出口。</summary>
    public ConstructionForceRouting Routing;
}

/// <summary>已复制的单条分支，不再读取编辑数组。</summary>
public readonly struct CompiledForceRoute
{
    /// <summary>实际出口和输入比例。</summary>
    public readonly ConstructionForceDestination Destination;

    /// <summary>各分支统一消除浮点边界误差后的比例。</summary>
    public readonly float Share;

    /// <summary>可选的单位方向。</summary>
    public readonly Vector3? Direction;

    /// <summary>方向筛选的最小点积。</summary>
    public readonly float MinimumDot;

    /// <summary>复制已校验分支。</summary>
    /// <param name="source">资料分支。</param><param name="scale">份额统一比例。</param>
    internal CompiledForceRoute(ConstructionForceRoute source, float scale)
    {
        Destination = source.Destination;
        Share = source.Share * scale;
        Direction = source.Direction == null
            ? null
            : new Vector3(source.Direction[0], source.Direction[1], source.Direction[2]).normalized;
        MinimumDot = Mathf.Cos(source.Angle * Mathf.Deg2Rad);
    }

    /// <summary>在本段来力坐标中改向；竖直来力使用稳定侧轴，不依赖观察方向。</summary>
    /// <param name="incoming">本段非零世界向量。</param>
    public Vector3 WorldDirection(Vector3 incoming)
    {
        Vector3 forward = incoming.normalized;
        if (!Direction.HasValue) return forward;
        Vector3 side = Vector3.Cross(Vector3.forward, forward);
        if (side.sqrMagnitude < .000001f) side = Vector3.right;
        else side.Normalize();
        Vector3 up = Vector3.Cross(forward, side);
        Vector3 local = Direction.Value;
        return forward * local.x + side * local.y + up * local.z;
    }
}

/// <summary>边界只校验一次的有限传力规则。</summary>
public sealed class CompiledForceRouting
{
    /// <summary>原顺序的只读分支。</summary>
    public IReadOnlyList<CompiledForceRoute> Routes { get; }

    /// <summary>本次允许的连接数和人物数。</summary>
    public int MaximumDepth { get; }

    /// <summary>本次接触链人数边界。</summary>
    public int MaximumActors { get; }

    /// <summary>只表示控制及处理规模，不额外生成来力。</summary>
    public double Work => Routes.Count * .02d * MaximumActors;

    /// <summary>从可编辑或恢复资料复制参数。</summary>
    /// <param name="source">完整规则。</param><param name="path">错误位置。</param>
    internal CompiledForceRouting(ConstructionForceRouting source, string path)
    {
        if (source.Routes == null || source.Routes.Length < 1 || source.Routes.Length > 8 ||
            source.MaximumDepth < 0 || source.MaximumDepth > 8 || source.MaximumActors < 1 || source.MaximumActors > 32)
            throw new InvalidDataException(path + "：需要1至8条分支、0至8层连接及1至32个人物");
        double sum = 0;
        foreach (var route in source.Routes)
        {
            if (route == null || !Enum.IsDefined(typeof(ConstructionForceDestination), route.Destination) ||
                !Finite(route.Share) || route.Share <= 0f || route.Share > 1f ||
                !Finite(route.Angle) || route.Angle < 0f || route.Angle > 180f ||
                route.Direction != null && (route.Direction.Length != 3 ||
                                            route.Direction.Any(v => !Finite(v) || Math.Abs(v) > 1000f) ||
                                            route.Direction.Sum(v => (double)v * v) < .000001d ||
                                            route.Destination == ConstructionForceDestination.Support))
                throw new InvalidDataException(path + "：分支需要有效出口、正数份额和0至180度夹角；方向需要三个有限分量且不能为零向量，支撑方向由真实落脚决定");
            sum += route.Share;
        }

        if (sum > 1.000001d) throw new InvalidDataException(path + "：分支份额合计不能超过一");
        Routes = Array.AsReadOnly(source.Routes.Select(r => new CompiledForceRoute(r, (float)(1d / Math.Max(1d, sum))))
            .ToArray());
        MaximumDepth = source.MaximumDepth;
        MaximumActors = source.MaximumActors;
    }

    /// <summary>恢复资料入口共用的有限值判断。</summary><param name="value">外部数值。</param>
    internal static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
}

/// <summary>编译后的身体承力，不持有临时余额。</summary>
public sealed class CompiledBodyForceResponse
{
    /// <summary>整次施放的初始额度。</summary>
    public float Capacity { get; }

    /// <summary>共用的分配规则。</summary>
    public CompiledForceRouting Routing { get; }

    /// <summary>检查并复制正式构造。</summary>
    /// <param name="source">身体资料。</param>
    internal CompiledBodyForceResponse(BodyForceResponse source)
    {
        if (!CompiledForceRouting.Finite(source.Capacity) || source.Capacity <= 0f || source.Capacity > 1000000f ||
            source.Routing == null)
            throw new InvalidDataException("Body/ForceResponse：需要正数且不超过一百万的承力额度及明确出口");
        Capacity = source.Capacity;
        Routing = new CompiledForceRouting(source.Routing, "Body/ForceResponse/Routing");
    }
}