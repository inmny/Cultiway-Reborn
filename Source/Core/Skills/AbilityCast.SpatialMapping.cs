using System.IO;
using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>生成物和生灵采用同一入口大小约定。</summary>
    /// <param name="context">命名步骤的构造资料。</param>
    private static float CompileMappingRadius(ConstructionOperationCompileContext context)
    {
        float radius = context.Source.MappingRadius ?? 1f;
        if (!(radius > 0f && radius <= 32f)) throw new InvalidDataException(context.Path + "：入口半径须大于0且不超过32");
        return radius;
    }

    /// <summary>按两端当前姿态保留局部位置和方向；门的显示大小不参与换算。</summary>
    /// <param name="step">已经绑定双门的步骤。</param><param name="rotation">入口到出口的朝向差。</param>
    private Matrix4x4 MappingFrame(CompiledConstructionStep step, out Quaternion rotation)
    {
        Matrix4x4 entrance = nodes[step.MappingEntrance].Transform, exit = nodes[step.MappingExit].Transform;
        rotation = exit.rotation * Quaternion.Inverse(entrance.rotation);
        return Matrix4x4.TRS((Vector3)exit.GetColumn(3) - rotation * (Vector3)entrance.GetColumn(3), rotation, Vector3.one);
    }

    /// <summary>在真实接触回调中计算候选落点，数值计算本身不移动人物。</summary>
    /// <param name="index">生灵转移步骤。</param><param name="node">实际接触部位。</param>
    /// <param name="foot">原人物脚下。</param><param name="mapped">按出口方向换算的候选落点。</param>
    internal bool TryMapActor(int index, int node, Vector3 foot, out Vector3 mapped)
    {
        var step = Plan.Steps[index];
        mapped = default;
        if (cancelled || step.Node != node || stepStates[index].State != ConstructionStepState.Waiting ||
            NodeAge(node) < step.Action.At || !StepInputsReady(index)) return false;
        Vector3 entrance = nodes[node].Transform.GetColumn(3);
        float radius = (float)step.Action.Parameters;
        if ((foot - entrance).sqrMagnitude > radius * radius) return false;
        mapped = MappingFrame(step, out _).MultiplyPoint3x4(foot);
        return true;
    }

    /// <summary>只有原人物已到达出口才发布完成，拒绝时中断依赖完成的步骤。</summary>
    /// <param name="index">已经尝试的转移步骤。</param><param name="receipt">实际到达；失败为空。</param>
    internal void FinishActorTransfer(int index, ConstructionWorldReceipt receipt)
    {
        if (cancelled || stepStates[index].State != ConstructionStepState.Waiting) return;
        if (receipt == null)
        {
            stepStates[index].State = ConstructionStepState.Interrupted;
            UpdateSteps();
        }
        else
        {
            StepApplied(Plan.Steps[index].Action);
            ConfirmStepWorld(index, receipt);
        }
    }

    /// <summary>通过两端的当前姿态转移同一个形体；门的尺寸不缩放通过的物体。</summary>
    /// <param name="action">已绑定入口、出口和主体的命名步骤。</param>
    /// <param name="radius">实际入口半径。</param>
    private bool MapSpace(CompiledEventOperation action, float radius)
    {
        var step = Plan.Steps[action.Step];
        ref NodeState subject = ref nodes[step.Node];
        ref NodeState entrance = ref nodes[step.MappingEntrance];
        Vector3 from = entrance.Transform.GetColumn(3);
        Vector3 pivot = subject.Free ? subject.Position : (Vector3)subject.Transform.GetColumn(3);
        if (!subject.Visible || (pivot - from).sqrMagnitude > radius * radius) return false;

        // 动态曲面和正在聚合的部位另有位置控制，不能只移动它们的显示矩阵。
        for (int i = step.Node; i < nodes.Length; i++)
            if ((i == step.Node || IsAttachedBelow(i, step.Node)) &&
                (flows?[i]?.Started == true || nodes[i].Ray != null || gathers?[i].Process != null || propagations?[i] != null)) return false;

        Matrix4x4 map = MappingFrame(step, out Quaternion rotation);

        // 先记下整支的旧位置，再统一搬移。连续穿过多个门时保留这一步最早的离开点。
        for (int i = nodes.Length - 1; i >= step.Node; i--)
        {
            ref NodeState part = ref nodes[i];
            if (i != step.Node && (!part.Started || !IsAttachedBelow(i, step.Node))) continue;
            part.TransferDeparture = TryTransferDeparture(i, out var previous) ? previous * part.Transform : part.Transform;
            if (i == step.Node) ReleaseNode(i);
            part.TransferRevision++;
            part.Transform = map * part.Transform;
            part.AttachedVelocity = rotation * part.AttachedVelocity;
            part.Revision++;
        }
        subject.Position = map.MultiplyPoint3x4(pivot);
        subject.Rotation = rotation * subject.Rotation;
        subject.Velocity = rotation * subject.Velocity;
        subject.Acceleration = rotation * subject.Acceleration;
        subject.FlightDirection = rotation * subject.FlightDirection;
        if (subject.Flying) subject.FlightOrigin = map.MultiplyPoint3x4(subject.FlightOrigin);
        if (subject.Flight?.Goal?.Anchor == ConstructionFlightAnchor.Direction)
            subject.FlightTarget = rotation * subject.FlightTarget;
        if (subject.Flight?.Path != null)
        {
            subject.PathCentre = map.MultiplyPoint3x4(subject.PathCentre);
            subject.PathVelocity = rotation * subject.PathVelocity;
            subject.PathX = rotation * subject.PathX;
            subject.PathY = rotation * subject.PathY;
            subject.PathZ = rotation * subject.PathZ;
        }
        return true;
    }
}
