using System;
using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>转折的不可变姿态参数，不保存原始描述。</summary>
internal readonly struct CompiledDeflection
{
    /// <summary>实际转角。</summary>
    internal readonly Quaternion Rotation;
    /// <summary>是否按地图坐标解释；否则以执行当时主体朝向为准。</summary>
    internal readonly bool World;
    /// <summary>创建时固定数值，不固定执行时的朝向。</summary>
    /// <param name="rotation">已检查转角。</param><param name="world">地图方向。</param>
    internal CompiledDeflection(Quaternion rotation, bool world) { Rotation = rotation; World = world; }
}

/// <summary>不重新施法、不重置曲线时钟的实际运动转折。</summary>
public sealed partial class AbilityCast
{
    /// <summary>一个新增操作只登记自己的参数、数值执行和完整目标要求。</summary>
    private static void RegisterDeflection() => ConstructionOperations.Register(ConstructionOperationKind.Deflect,
        c =>
        {
            if (c.Vector.sqrMagnitude == 0 || c.Vector.magnitude > 7200f) throw new InvalidDataException(c.Path + "：转折需要非零且不超过7200度的明确转角");
            return new CompiledDeflection(Quaternion.Euler(c.Vector), c.Source.WorldVelocity);
        }, (cast, action, p) => cast.Deflect(action.Node, p), immediate: p => true,
        validate: (node, p, path) =>
        {
            if (node.FlowSettings != null) throw new InvalidDataException(path + "：转折作用于完整自由主体；流散水格须由其实际流动规则改变动量");
        }, direct: false);

    /// <summary>先转实际运动和朝向，再围绕当前位置转曲线参照，最后作废旧接触预测。</summary>
    /// <param name="node">当前真实部位。</param><param name="p">已编译转角。</param>
    private bool Deflect(int node, CompiledDeflection p)
    {
        ref NodeState state = ref nodes[node];
        if (!state.Free || state.MovementStopped) return false;
        Quaternion rotation = p.World ? p.Rotation : state.Rotation * p.Rotation * Quaternion.Inverse(state.Rotation);
        Vector3 previous = state.Velocity;
        state.Velocity = rotation * state.Velocity; state.Acceleration = rotation * state.Acceleration;
        state.Rotation = rotation * state.Rotation;
        if (state.Flying && state.Flight.Path != null)
        {
            state.PathVelocity = rotation * state.PathVelocity;
            state.PathX = rotation * state.PathX; state.PathY = rotation * state.PathY; state.PathZ = rotation * state.PathZ;
            state.Flight.Path.Sample(Mathf.Max(0f, Age - state.FlightStartedAt), out Vector3 offset, out _);
            state.PathCentre = state.Position - PathVector(in state, offset);
        }
        Matrix4x4 turn = Matrix4x4.TRS(state.Position, rotation, Vector3.one) * Matrix4x4.Translate(-state.Position);
        state.Transform = turn * state.Transform;
        for (int child = node + 1; child < nodes.Length; child++)
            if (IsAttachedBelow(child, node))
            { nodes[child].Transform = turn * nodes[child].Transform; nodes[child].AttachedVelocity = rotation * nodes[child].AttachedVelocity; nodes[child].Revision++; }
        AdjustFragmentVelocity(node, previous); state.Revision++;
        return true;
    }
}
