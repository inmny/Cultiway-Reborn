using System;
using System.IO;
using UnityEngine;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>飞行对共同实际地表的响应。</summary>
public enum ConstructionGroundResponse
{
    /// <summary>不限制离地高度，可用于明确的穿地或无地面路径。</summary>
    Ignore = 0,

    /// <summary>实际作用形状着地后停止，保留当前位置及形状。</summary>
    Stop = 1,

    /// <summary>由真实地表持续承托，沿水平运动继续推进。</summary>
    Follow = 2,

    /// <summary>实际形状着地后结束本部位，最后一段仍参与真实接触。</summary>
    End = 3
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
    /// <summary>明确目标及实际到达规则，空值保留原行为。</summary>
    internal readonly CompiledFlightGoal Goal;
    /// <summary>可选的同主体间隔转移。</summary>
    internal readonly CompiledFlightTransfer Transfer;
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
        if (source.Goal != null) Goal = new CompiledFlightGoal(source.Goal, path + "/Goal");
        if (source.Transfer != null)
        {
            Transfer = new CompiledFlightTransfer(source.Transfer, path + "/Transfer");
            if (Path != null || Acceleration != Vector3.zero)
                throw new InvalidDataException(path + "：间隔转移不同时采用连续位置曲线或加速度，可使用方向曲线和调速");
        }
        if (Goal?.DirectArrival == true && Goal.Arrival == ConstructionFlightArrival.Continue &&
            (Path != null || Acceleration != Vector3.zero))
            throw new InvalidDataException(path + "：直达后继续采用中心飞行，不同时叠加位置曲线或加速度");
        if (Goal?.Approach == true && Acceleration != Vector3.zero)
            throw new InvalidDataException(path + "：限速接近通过速度调整改变快慢，不与向量加速度混用");
    }
}

/// <summary>实际部位上的一次条件操作，不是另一施放或形体。</summary>
internal sealed class CompiledEventOperation
{
    /// <summary>原部位可选的稳定输出名称。</summary>
    internal readonly string Id;
    /// <summary>实际部位及独占状态索引。</summary>
    internal int Node, Index;
    /// <summary>实际输出步骤，负一表示没有命名跟踪。</summary>
    internal int Step = -1;
    /// <summary>已经核对的执行种类。</summary>
    internal readonly ConstructionOperationKind Kind;
    /// <summary>本部位开始接受操作的时点。</summary>
    internal readonly float At;
    /// <summary>已经解析的真实事件绑定。</summary>
    internal readonly CompiledConstructionCondition When;
    /// <summary>只保存本种操作使用的参数，不为所有种类预留空字段。</summary>
    private readonly object parameters;
    /// <summary>已登记的同一版本实现，运行不查全局表。</summary>
    internal readonly ConstructionOperationImplementation Implementation;
    /// <summary>实际资料位置，只在完整展开后核对目标使用。</summary>
    internal readonly string Path;
    /// <summary>实际应用是否同时完成。</summary>
    internal readonly bool Immediate;
    /// <summary>只供本类连续表在创建时读取。</summary>
    internal object Parameters => parameters;
    /// <summary>飞行参数。</summary>
    internal CompiledFlight Flight => parameters as CompiledFlight;
    /// <summary>聚合参数。</summary>
    internal CompiledGather Gather => parameters as CompiledGather;
    /// <summary>实际分件参数。</summary>
    internal CompiledFracture Fracture => parameters as CompiledFracture;
    /// <summary>流散参数。</summary>
    internal CompiledFlow Flow => parameters as CompiledFlow;
    /// <summary>收束参数。</summary>
    internal CompiledRecovery Recovery => parameters as CompiledRecovery;
    /// <summary>导电参数。</summary>
    internal CompiledConduction Conduction => parameters as CompiledConduction;
    /// <summary>沿地传播的实际参数。</summary>
    internal CompiledPropagation Propagation => parameters as CompiledPropagation;
    /// <summary>有限射线的实际制作控制。</summary>
    internal CompiledRay Ray => parameters as CompiledRay;
    /// <summary>同一份动作参数。</summary>
    internal CompiledMotionParameters MotionData => parameters as CompiledMotionParameters;
    /// <summary>同一份物态及世界选择。</summary>
    internal CompiledMatterParameters MatterData => parameters as CompiledMatterParameters;
    /// <summary>动作资源。</summary>
    internal FormMotion Motion => MotionData?.Motion;
    /// <summary>动作速度和过渡。</summary>
    internal float MotionSpeed => MotionData?.Speed ?? 0f;
    /// <summary>实际过渡时长。</summary>
    internal float BlendSeconds => MotionData?.Blend ?? 0f;
    /// <summary>动作自己的维持。</summary>
    internal CompiledConstructionHold Hold => MotionData?.Hold ?? default;
    /// <summary>动作标记阶段。</summary>
    internal float ActiveFrom => MotionData?.From ?? 0f;
    /// <summary>动作结束标记。</summary>
    internal float ActiveUntil => MotionData?.Until ?? 0f;
    /// <summary>生长目标。</summary>
    internal float TargetScale => parameters is CompiledGrowthParameters growth ? growth.Scale : 0f;
    /// <summary>生长历时。</summary>
    internal float Seconds => parameters is CompiledGrowthParameters growth ? growth.Seconds : 0f;
    /// <summary>已解析的输出材料。</summary>
    internal AbilityMatter Matter => MatterData?.Matter;
    /// <summary>已解析的世界选择。</summary>
    internal CompiledWorldMatter WorldMatter => MatterData?.World ?? (parameters as CompiledErosion)?.World;
    /// <summary>只有旋转、飞行或聚合需要三维向量。</summary>
    internal Vector3 Vector => parameters is Vector3 spin ? spin : Flight?.Velocity ?? Gather?.Target ?? Vector3.zero;

    /// <summary>仅在编译边界创建本种操作的参数和真实条件。</summary>
    /// <param name="source">可编辑输入。</param><param name="vector">已检查的向量。</param><param name="lifetime">整次截止。</param>
    /// <param name="path">错误位置。</param><param name="resource">实际资源。</param><param name="node">原部位。</param>
    /// <param name="fracture">实际分件。</param><param name="bound">输入是否已绑定。</param><param name="order">原资料顺序。</param>
    internal CompiledEventOperation(ConstructionOperation source, Vector3 vector, float lifetime, string path, FormResource resource, ConstructionNode node, CompiledFracture fracture, bool bound = false, int order = -1)
    {
        Kind = source.Kind; Id = source.Id; At = source.At; Path = path;
        Implementation = ConstructionOperations.Implementation(Kind);
        When = source.When == null && (bound || !Implementation.Direct) ? null : new CompiledConstructionCondition(source.When, lifetime, path + "/When");
        parameters = Implementation.Compile(new ConstructionOperationCompileContext(source, vector, lifetime, path, resource, node, fracture, order));
        Immediate = Implementation.Immediate(parameters);
    }
}
