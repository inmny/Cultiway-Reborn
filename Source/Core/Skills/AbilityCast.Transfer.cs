using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>间隔转移只改变同一主体的实际位置，空隙不参与扫掠。</summary>
public sealed partial class AbilityCast
{
    /// <summary>在下一转移时点拆步，保持大步推进与逐帧推进相同。</summary>
    /// <param name="state">本次独占飞行状态。</param><param name="maximum">其他边界允许的步长。</param>
    private float TransferStep(in NodeState state, float maximum)
    {
        if (!state.Flying || state.MovementStopped || state.Flight.Transfer == null) return maximum;
        float marker = state.FlightStartedAt + (state.TransferCount + 1) * state.Flight.Transfer.Interval;
        return Mathf.Min(maximum, Mathf.Max(Mathf.Max(.000001f, Age * .0000001f), marker - Age));
    }

    /// <summary>间隔内停留，到时只在目标方向取得一个真实落点。</summary>
    /// <param name="node">实际部位。</param><param name="state">独占状态。</param><param name="target">真实目标。</param><param name="aim">普通方向及曲线偏转后的追逐点。</param><param name="growth">实际尺寸倍率。</param>
    private bool MoveTransfer(int node, ref NodeState state, Vector3 target, Vector3 aim, float growth)
    {
        CompiledFlightTransfer transfer = state.Flight?.Transfer;
        if (!state.Flying || transfer == null) return false;
        state.Velocity = Vector3.zero;
        float marker = state.FlightStartedAt + (state.TransferCount + 1) * transfer.Interval;
        if (Age + Mathf.Max(.0000001f, Age * .0000001f) < marker) return true;
        state.TransferDeparture = Matrix4x4.TRS(state.Position, state.Rotation, state.Scale * Mathf.Max(.00001f, growth));
        if (Plan.Nodes[node].FragmentSource >= 0) state.TransferDeparture *= Matrix4x4.Translate(-state.Pivot);
        Vector3 before = state.Position;
        Vector3 desired = state.Flight.Goal == null ? state.FlightDirection : aim - before;
        Vector3 direction = desired.sqrMagnitude > 1e-8f ? desired.normalized : state.FlightDirection;
        if (state.Flight.TurnRate > 0f)
            direction = Vector3.RotateTowards(state.FlightDirection, direction, state.Flight.TurnRate * Mathf.Deg2Rad * transfer.Interval, 0f).normalized;
        float distance = Mathf.Max(transfer.Distance, state.ApproachSpeed * transfer.Interval);
        Vector3 actualGoal = FlightAim(in state, target);
        bool final = state.Flight.Goal != null && state.Flight.Goal.Anchor != ConstructionFlightAnchor.Direction &&
            state.Flight.Goal.Arrival != ConstructionFlightArrival.None && Vector3.Distance(before, actualGoal) <= distance + transfer.ArrivalSlack;
        Vector3 side = new(-direction.y, direction.x, 0f);
        if (side.sqrMagnitude > 1e-8f) side.Normalize();
        state.Position = final ? actualGoal : before + direction * distance + side * transfer.Offset(state.TransferCount);
        Vector3 delta = state.Position - before;
        if (delta.sqrMagnitude > 1e-8f)
        {
            state.Rotation = Quaternion.FromToRotation(state.FlightDirection, delta) * state.Rotation;
            state.FlightDirection = delta.normalized;
        }
        state.TransferCount++; state.TransferRevision++;
        AdvanceSteering(ref state, delta / transfer.Interval, transfer.Interval);
        return true;
    }

    /// <summary>找出本步空间转移前的共同根变换，附着部件同样不会在空隙扫伤。</summary>
    /// <param name="node">生成部位；身体不使用此运动。</param><param name="transform">从本步末态映回离开前姿态。</param>
    internal bool TryTransferDeparture(int node, out Matrix4x4 transform)
    {
        transform = Matrix4x4.identity;
        while (node >= 0)
        {
            if (nodes[node].TransferRevision != savedNodes[node].TransferRevision)
            {
                transform = nodes[node].TransferDeparture * nodes[node].Transform.inverse;
                return true;
            }
            if (nodes[node].Free) return false;
            node = Plan.Nodes[node].Parent;
        }
        return false;
    }
}
