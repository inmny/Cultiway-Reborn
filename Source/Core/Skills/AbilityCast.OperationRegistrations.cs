using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>明确登记操作的编译、纯数值应用和实际完成，不按法术编号判断。</summary>
public sealed partial class AbilityCast
{
    /// <summary>旧专用定时入口保持原实现；所有条件及命名执行共用同一登记表。</summary>
    internal static void RegisterOperationImplementations()
    {
        ConstructionOperations.Register(ConstructionOperationKind.Fly, c => new CompiledFlight(c.Source, c.Vector, c.Path),
            (cast, action, p) => { cast.StartFlight(action.Node, p, cast.Age); return true; },
            (cast, action, p, native) => new ConstructionOperationProgress(
                ReferenceEquals(cast.nodes[action.Node].Flight, native ? cast.Plan.Nodes[action.Node].Flight : p) && cast.nodes[action.Node].MovementStopped,
                !ReferenceEquals(cast.nodes[action.Node].Flight, native ? cast.Plan.Nodes[action.Node].Flight : p)), direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Animate, CompileMotion,
            (cast, action, p) => { cast.StartMotion(action.Node, action, p); return true; },
            (cast, action, p, native) => cast.MotionProgress(action, p, native), direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Grow, c =>
            {
                float scale = c.Source.TargetScale ?? 1f;
                if (c.Source.Seconds <= 0 || c.Source.Seconds > c.Lifetime || float.IsNaN(scale) || float.IsInfinity(scale) || scale < 0 || scale > 64)
                    throw new InvalidDataException(c.Path + "：生长需要正数历时，目标倍率须在0至64内");
                return new CompiledGrowthParameters(scale, c.Source.Seconds);
            }, (cast, action, p) => { cast.StartGrowth(action.Node, action, p); return true; },
            (cast, action, p, native) => cast.GrowthProgress(action, p, native), direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Spin, c =>
            {
                if (c.Vector.magnitude > 7200f) throw new InvalidDataException(c.Path + "：旋转超过每秒7200度");
                return c.Vector;
            }, (cast, action, p) =>
            {
                ref NodeState state = ref cast.nodes[action.Node]; state.SpinOperation = action;
                state.SpinOffset *= Quaternion.Euler(state.Spin * Mathf.Max(0f, cast.Age - state.SpinAt));
                state.Spin = p; state.SpinAt = cast.Age; return true;
            }, (cast, action, p, native) => new ConstructionOperationProgress(
                (native ? cast.nodes[action.Node].SpinOperation == null : ReferenceEquals(cast.nodes[action.Node].SpinOperation, action)) && cast.nodes[action.Node].Spin.sqrMagnitude == 0,
                native ? cast.nodes[action.Node].SpinOperation != null : !ReferenceEquals(cast.nodes[action.Node].SpinOperation, action)), direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.ConvertMatter, c => new CompiledMatterParameters(
                AbilityMatters.Resolve(c.Source.Matter, c.Source.State, c.Path), c.Source.WorldMatter == null ? null : new CompiledWorldMatter(c.Source.WorldMatter, c.Path, c.Order)),
            (cast, action, p) => { cast.ChangeMatter(action.Node, p.Matter); cast.operations[action.Index].WorldRequested = p.World != null; return true; }, immediate: p => p.World == null, direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Gather, c => new CompiledGather(c.Source, c.Vector,
                Quaternion.Euler(c.Node.Rotation[0], c.Node.Rotation[1], c.Node.Rotation[2]), c.Lifetime, c.Path),
            (cast, action, p) => { cast.StartGather(action.Node, p, cast.Age); return true; },
            (cast, action, p, native) => cast.GatherProgress(action, p, native), direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Fracture, c => c.Fracture,
            (cast, action, p) => { cast.RequestFracture(action.Node); return true; },
            (cast, action, p, native) => new ConstructionOperationProgress(cast.fractures[action.Node].Broken), direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Flow, c => new CompiledFlow(c.Source.Flow, c.Path),
            (cast, action, p) => { cast.StartFlow(action.Node); return true; },
            (cast, action, p, native) => new ConstructionOperationProgress(cast.flows[action.Node].Started && (cast.flows[action.Node].Volume <= .000000001 || cast.nodes[action.Node].MovementStopped)), direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Recover, c => new CompiledRecovery(c.Source, c.Lifetime, c.Path),
            (cast, action, p) => { cast.StartRecovery(action.Node, p); cast.nodes[action.Node].RecoveryOperation = action; return true; }, direct: true);
        ConstructionOperations.Register(ConstructionOperationKind.Conduct, CompileConduction,
            (cast, action, p) => { cast.operations[action.Index].Conduction.Start(cast.Age); cast.operations[action.Index].Conduction.Tick(cast.Age, p); return true; }, direct: true);
        ConstructionOperations.Register<object>(ConstructionOperationKind.Detach, c => null,
            (cast, action, p) => { cast.ReleaseNode(action.Node); return true; }, immediate: p => true, direct: true);
        ConstructionOperations.Register<object>(ConstructionOperationKind.Stop, c => null,
            (cast, action, p) => { cast.StopNode(action.Node, 1f); return true; }, immediate: p => true, direct: true);
        ConstructionOperations.Register<object>(ConstructionOperationKind.Leave, c => null,
            (cast, action, p) => { cast.StartLeave(action.Node); cast.nodes[action.Node].LeaveOperation = action; return true; }, direct: true);
        RegisterDeflection();
    }

    /// <summary>在创建边界解析制作动作、真实阶段及自身维持。</summary>
    /// <param name="c">当前操作的完整输入。</param>
    private static CompiledMotionParameters CompileMotion(ConstructionOperationCompileContext c)
    {
        var source = c.Source;
        if (string.IsNullOrWhiteSpace(source.Clip) || source.Seconds < 0) throw new InvalidDataException(c.Path + "：动作名称或历时无效");
        var motion = ConstructionReferences.Resolve(c.Path + "/Clip", () => c.Resource.Rig.Motion(source.Clip));
        float speed = source.Seconds == 0 ? 1f : motion.Duration / source.Seconds, blend = source.BlendSeconds ?? .15f;
        var hold = new CompiledConstructionHold(source.Hold, c.Lifetime, c.Path + "/Hold");
        if (source.Hold != null && source.At + (source.When?.Delay ?? 0f) + hold.At + hold.Seconds > (c.Node.DisappearAt ?? c.Lifetime) + .000001f)
            throw new InvalidDataException(c.Path + "/Hold：最早启动后的维持已超过实际存在截止");
        if (source.Hold != null && hold.At > motion.Duration / speed) throw new InvalidDataException(c.Path + "/Hold：维持时点超过本次动作长度");
        if (float.IsNaN(speed) || float.IsInfinity(speed) || motion.MaximumAngularRate * speed > 7200f || float.IsNaN(blend) || float.IsInfinity(blend) || blend <= 0 || blend > c.Lifetime)
            throw new InvalidDataException(c.Path + "：动作速度或姿态过渡历时无效，过渡须为正数");
        float from = string.IsNullOrEmpty(c.Node.ActiveFromMarker) ? -1f : ConstructionReferences.Resolve(c.Path + "/ActiveFromMarker", () => motion.MarkerTime(c.Node.ActiveFromMarker));
        float until = string.IsNullOrEmpty(c.Node.ActiveUntilMarker) ? -1f : ConstructionReferences.Resolve(c.Path + "/ActiveUntilMarker", () => motion.MarkerTime(c.Node.ActiveUntilMarker));
        if (from >= 0 && until >= 0 && from > until) throw new InvalidDataException(c.Path + "：当前动作阶段顺序相反");
        return new CompiledMotionParameters(motion, speed, blend, hold, from, until);
    }

    /// <summary>在创建边界确定实际波次，保留原声明次序。</summary>
    /// <param name="c">当前实际输入。</param>
    private static CompiledConduction CompileConduction(ConstructionOperationCompileContext c)
    {
        var result = new CompiledConduction(c.Source.Conduction, c.Path, c.Order);
        if (result.Pulses > 1 && c.Source.At + (c.Source.When?.Delay ?? 0) + result.Span >= (c.Node.DisappearAt ?? c.Lifetime))
            throw new InvalidDataException(c.Path + "：导电整轮须能在部位结束前启动并完成");
        return result;
    }

    /// <summary>原定时与替换后的生长分别读取真实进度。</summary>
    /// <param name="action">真实执行身份。</param><param name="p">本次参数。</param><param name="native">原定时过程。</param>
    private ConstructionOperationProgress GrowthProgress(CompiledEventOperation action, CompiledGrowthParameters p, bool native)
    {
        NodeState state = nodes[action.Node];
        return native ? new ConstructionOperationProgress(!state.GrowthChanged && NodeAge(action.Node) >= Plan.Nodes[action.Node].Growth.y && state.GrowthValue >= .99999f, state.GrowthChanged || state.GrowthStopped)
            : new ConstructionOperationProgress(ReferenceEquals(state.GrowthOperation, action) && state.GrowthAge >= p.Seconds, !ReferenceEquals(state.GrowthOperation, action) || state.GrowthStopped);
    }

    /// <summary>维持实际用完后才发布动作完成，循环动作没有自然完成。</summary>
    /// <param name="action">真实执行身份。</param><param name="p">实际动作。</param><param name="native">原定时过程。</param>
    private ConstructionOperationProgress MotionProgress(CompiledEventOperation action, CompiledMotionParameters p, bool native)
    {
        NodeState state = nodes[action.Node];
        return native ? new ConstructionOperationProgress(state.Motion == null && !p.Motion.Loop && state.MotionAge >= p.Motion.Duration && state.HoldUsed >= Plan.Nodes[action.Node].Hold.Seconds, state.Motion != null || state.MotionStopped)
            : new ConstructionOperationProgress(ReferenceEquals(state.Motion, action) && !p.Motion.Loop && state.MotionAge >= p.Motion.Duration && state.MotionHoldUsed >= p.Hold.Seconds, !ReferenceEquals(state.Motion, action) || state.MotionStopped);
    }

    /// <summary>只有实际连接与对应聚合仍成立才正常完成。</summary>
    /// <param name="action">真实执行身份。</param><param name="p">实际聚合。</param><param name="native">原定时过程。</param>
    private ConstructionOperationProgress GatherProgress(CompiledEventOperation action, CompiledGather p, bool native)
    {
        var process = native ? Plan.Nodes[action.Node].Gather : p;
        return new ConstructionOperationProgress(ReferenceEquals(gathers[action.Node].Process, process) && !nodes[action.Node].Free && Age >= gathers[action.Node].StartedAt + process.Seconds,
            nodes[action.Node].Free || !ReferenceEquals(gathers[action.Node].Process, process));
    }
}
