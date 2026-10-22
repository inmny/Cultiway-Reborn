using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>飞行对实际地图零高度平面的响应。</summary>
public enum ConstructionGroundResponse
{
    /// <summary>不限制离地高度，可用于明确的穿地或无地面路径。</summary>
    Ignore,
    /// <summary>实际作用形状着地后停止，保留当前位置及形状。</summary>
    Stop
}

/// <summary>已经检查的完整飞行参数，可由事件替换而不修改共用计划。</summary>
internal sealed class CompiledFlight
{
    /// <summary>启动时明确提供的速度及加速度。</summary>
    internal readonly Vector3 Velocity, Acceleration;
    /// <summary>是否采用地图方向；否则按启动时形体朝向解释。</summary>
    internal readonly bool WorldVelocity, InheritVelocity;
    /// <summary>每秒允许的追踪转角。</summary>
    internal readonly float TurnRate;
    /// <summary>可选共用曲线。</summary>
    internal readonly CompiledConstructionPath Path;
    /// <summary>真实地面响应。</summary>
    internal readonly ConstructionGroundResponse Ground;
    /// <summary>检查并复制完整参数。</summary>
    /// <param name="source">外部操作。</param>
    /// <param name="velocity">已检查速度。</param>
    /// <param name="path">错误位置。</param>
    internal CompiledFlight(ConstructionOperation source, Vector3 velocity, string path)
    {
        Velocity = velocity; WorldVelocity = source.WorldVelocity; InheritVelocity = source.InheritVelocity; TurnRate = source.TurnRate; Ground = source.Ground;
        if (source.Acceleration != null)
        {
            if (source.Acceleration.Length != 3) throw new InvalidDataException(path + "：加速度需要三个坐标");
            Acceleration = new Vector3(source.Acceleration[0], source.Acceleration[1], source.Acceleration[2]);
        }
        if (float.IsNaN(Acceleration.sqrMagnitude) || float.IsInfinity(Acceleration.sqrMagnitude) || Acceleration.magnitude > 10000f || TurnRate > 7200f ||
            !Enum.IsDefined(typeof(ConstructionGroundResponse), Ground)) throw new InvalidDataException(path + "：加速度、转向或地面响应超出预算");
        if (source.Path != null) Path = new CompiledConstructionPath(source.Path, path + "/Path");
    }
}

/// <summary>实际部位上的一次条件操作，不是另一施放或形体。</summary>
internal sealed class CompiledEventOperation
{
    /// <summary>共用计划内的目标部位。</summary>
    internal int Node;
    /// <summary>本次施放的独立操作状态索引。</summary>
    internal int Index;
    /// <summary>实际改变的过程。</summary>
    internal readonly ConstructionOperationKind Kind;
    /// <summary>目标分支最早接受事件的时点。</summary>
    internal readonly float At;
    /// <summary>旋转操作的明确角速度。</summary>
    internal readonly Vector3 Vector;
    /// <summary>明确条件。</summary>
    internal readonly CompiledConstructionCondition When;
    /// <summary>飞行操作的全部替换参数。</summary>
    internal readonly CompiledFlight Flight;
    /// <summary>检查及复制一次操作。</summary>
    /// <param name="source">外部资料。</param>
    /// <param name="vector">已检查的三个坐标。</param>
    /// <param name="lifetime">整次上限。</param>
    /// <param name="path">具体错误位置。</param>
    internal CompiledEventOperation(ConstructionOperation source, Vector3 vector, float lifetime, string path)
    {
        Kind = source.Kind; At = source.At; Vector = vector;
        When = new CompiledConstructionCondition(source.When, lifetime, path + "/When");
        if (Kind is not (ConstructionOperationKind.Fly or ConstructionOperationKind.Spin or ConstructionOperationKind.Detach or ConstructionOperationKind.Stop))
            throw new InvalidDataException(path + "：此操作尚未提供运行中的条件改写，可使用条件部位开始");
        if (Kind == ConstructionOperationKind.Spin && vector.magnitude > 7200f) throw new InvalidDataException(path + "：旋转超过每秒7200度");
        if (Kind == ConstructionOperationKind.Fly) Flight = new CompiledFlight(source, vector, path);
    }
}
