using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>通过两端的当前姿态转移同一个形体；门的尺寸不缩放通过的物体。</summary>
    /// <param name="action">已绑定入口、出口和主体的命名步骤。</param>
    /// <param name="radius">实际入口半径。</param>
    private bool MapSpace(CompiledEventOperation action, float radius)
    {
        var step = Plan.Steps[action.Step];
        ref NodeState subject = ref nodes[step.Node];
        ref NodeState entrance = ref nodes[step.MappingEntrance];
        ref NodeState exit = ref nodes[step.MappingExit];
        Vector3 from = entrance.Transform.GetColumn(3), to = exit.Transform.GetColumn(3);
        Vector3 pivot = subject.Free ? subject.Position : (Vector3)subject.Transform.GetColumn(3);
        if (!subject.Visible || (pivot - from).sqrMagnitude > radius * radius) return false;

        // 动态曲面和正在聚合的部位另有位置控制，不能只移动它们的显示矩阵。
        for (int i = step.Node; i < nodes.Length; i++)
            if ((i == step.Node || IsAttachedBelow(i, step.Node)) &&
                (flows?[i]?.Started == true || nodes[i].Ray != null || gathers?[i].Process != null || propagations?[i] != null)) return false;

        Quaternion rotation = exit.Transform.rotation * Quaternion.Inverse(entrance.Transform.rotation);
        Matrix4x4 map = Matrix4x4.TRS(to - rotation * from, rotation, Vector3.one);

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
