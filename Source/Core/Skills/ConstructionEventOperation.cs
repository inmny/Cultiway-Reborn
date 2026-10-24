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
    Follow = 2
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
    /// <summary>原部位可选输出名称，编译时用于接通既有条件操作。</summary>
    internal readonly string Id;
    /// <summary>共用计划内的目标部位。</summary>
    internal int Node;
    /// <summary>本次施放的独立操作状态索引。</summary>
    internal int Index;
    /// <summary>对应命名步骤，负一表示原有真实条件操作。</summary>
    internal int Step = -1;
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
    /// <summary>条件尺寸变化的目标及历时。</summary>
    internal readonly float TargetScale, Seconds;
    /// <summary>已经解析的完整动作及速度。</summary>
    internal readonly FormMotion Motion;
    /// <summary>动作速度及过渡秒数。</summary>
    internal readonly float MotionSpeed, BlendSeconds;
    /// <summary>相对本次实际动作开始的维持，不暂停飞行、生长或其他独立过程。</summary>
    internal readonly CompiledConstructionHold Hold;
    /// <summary>已经检查的目标布局及聚合历时。</summary>
    internal readonly CompiledGather Gather;
    /// <summary>与原主体共用的真实分件定义。</summary>
    internal readonly CompiledFracture Fracture;
    /// <summary>由同一真实事件启动的流散。</summary>
    internal readonly CompiledFlow Flow;
    /// <summary>同一真实事件可开始收束，预算仍来自实际支付。</summary>
    internal readonly CompiledRecovery Recovery;

    /// <summary>同一真实事件触发的有界导电。</summary>
    internal readonly CompiledConduction Conduction;

    /// <summary>可选的真实范围世界材料转换。</summary>
    internal readonly CompiledWorldMatter WorldMatter;

    /// <summary>当前动作的明确阶段标记。</summary>
    internal readonly float ActiveFrom, ActiveUntil;
    /// <summary>已经解析的材料变化目标。</summary>
    internal readonly AbilityMatter Matter;
    /// <summary>检查及复制一次操作。</summary>
    /// <param name="source">外部资料。</param>
    /// <param name="vector">已检查的三个坐标。</param>
    /// <param name="lifetime">整次上限。</param>
    /// <param name="path">具体错误位置。</param>
    /// <param name="resource">已有资源及制作动作。</param>
    /// <param name="node">实际部位的阶段标记。</param>
    /// <param name="fracture">已经检查的真实分件，其他操作不使用。</param>
    /// <param name="bound">由已解析输入控制开始，允许没有另一份When条件。</param>
    /// <param name="order">命名操作的明确原资料序号，负一沿用原部位顺序。</param>
    internal CompiledEventOperation(ConstructionOperation source, Vector3 vector, float lifetime, string path, FormResource resource, ConstructionNode node, CompiledFracture fracture, bool bound = false, int order = -1)
    {
        Kind = source.Kind;
        Id = source.Id;
        At = source.At;
        Vector = vector;
        Fracture = source.Kind == ConstructionOperationKind.Fracture ? fracture : null;
        if (Kind == ConstructionOperationKind.Conduct)
        {
            Conduction = new CompiledConduction(source.Conduction, path, order < 0 ? Array.IndexOf(node.Operations, source) : order);
            if (Conduction.Pulses > 1 && At + (source.When?.Delay ?? 0) + Conduction.Span >= (node.DisappearAt ?? lifetime))
                throw new InvalidDataException(path + "：导电整轮须能在部位结束前启动并完成");
        }

        if (Kind == ConstructionOperationKind.Recover) Recovery = new CompiledRecovery(source, lifetime, path);
        if (Kind == ConstructionOperationKind.Flow) Flow = new CompiledFlow(source.Flow, path);
        When = source.When == null && bound ? null : new CompiledConstructionCondition(source.When, lifetime, path + "/When");
        if (Kind == ConstructionOperationKind.Spin && vector.magnitude > 7200f) throw new InvalidDataException(path + "：旋转超过每秒7200度");
        if (Kind == ConstructionOperationKind.Gather) Gather = new CompiledGather(source, vector, Quaternion.Euler(node.Rotation[0], node.Rotation[1], node.Rotation[2]), lifetime, path);
        if (Kind == ConstructionOperationKind.Fly) Flight = new CompiledFlight(source, vector, path);
        if (Kind == ConstructionOperationKind.Grow)
        {
            TargetScale = source.TargetScale ?? 1f; Seconds = source.Seconds;
            if (Seconds <= 0 || Seconds > lifetime || float.IsNaN(TargetScale) || float.IsInfinity(TargetScale) || TargetScale < 0 || TargetScale > 64)
                throw new InvalidDataException(path + "：生长需要正数历时，目标倍率须在0至64内");
        }
        if (Kind == ConstructionOperationKind.Animate)
        {
            if (string.IsNullOrWhiteSpace(source.Clip) || source.Seconds < 0) throw new InvalidDataException(path + "：动作名称或历时无效");
            Motion = ConstructionReferences.Resolve(path + "/Clip", () => resource.Rig.Motion(source.Clip)); MotionSpeed = source.Seconds == 0 ? 1f : Motion.Duration / source.Seconds;
            BlendSeconds = source.BlendSeconds ?? .15f;
            Hold = new CompiledConstructionHold(source.Hold, lifetime, path + "/Hold");
            if (source.Hold != null && Hold.At > Motion.Duration / MotionSpeed)
                throw new InvalidDataException(path + "/Hold：维持时点超过本次动作长度");
            if (float.IsNaN(MotionSpeed) || float.IsInfinity(MotionSpeed) || Motion.MaximumAngularRate * MotionSpeed > 7200f ||
                float.IsNaN(BlendSeconds) || float.IsInfinity(BlendSeconds) || BlendSeconds <= 0 || BlendSeconds > lifetime)
                throw new InvalidDataException(path + "：动作速度或姿态过渡历时无效，过渡须为正数");
            ActiveFrom = string.IsNullOrEmpty(node.ActiveFromMarker) ? -1f : ConstructionReferences.Resolve(path + "/ActiveFromMarker", () => Motion.MarkerTime(node.ActiveFromMarker));
            ActiveUntil = string.IsNullOrEmpty(node.ActiveUntilMarker)
                ? -1f
                : ConstructionReferences.Resolve(path + "/ActiveUntilMarker", () => Motion.MarkerTime(node.ActiveUntilMarker));
            if (ActiveFrom >= 0 && ActiveUntil >= 0 && ActiveFrom > ActiveUntil)
                throw new InvalidDataException(path + "：当前动作阶段顺序相反");
        }

        if (Kind == ConstructionOperationKind.ConvertMatter)
        {
            Matter = AbilityMatters.Resolve(source.Matter, source.State, path);
            if (source.WorldMatter != null)
                WorldMatter = new CompiledWorldMatter(source.WorldMatter, path, order < 0 ? Array.IndexOf(node.Operations, source) : order);
        }
    }
}
