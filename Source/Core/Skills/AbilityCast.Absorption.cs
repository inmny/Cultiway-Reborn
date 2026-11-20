using UnityEngine;

namespace Cultiway.Core.Skills;

public sealed partial class AbilityCast
{
    /// <summary>把已从来势扣除的力量存入唯一出口。首次接触固定位置和等待时间，后续吸收只增加实际力量。</summary>
    /// <param name="source">确实消耗容量的屏障。</param>
    /// <param name="point">接触结算确认的裂口位置。</param>
    /// <param name="amount">确实被吸收的力量。</param>
    private void RetainAbsorbedForce(int source, Vector3 point, float amount)
    {
        int index = Plan.Nodes[source].AbsorbedForceOutlet;
        ref NodeState outlet = ref nodes[index];
        if (amount <= 0f || !outlet.Exists || outlet.Started || Age >= outlet.BirthAt) return;
        NodeState shell = nodes[source];
        outlet.Force = outlet.InitialForce = Mathf.Min(shell.InitialForce, outlet.Force + amount);
        if (!float.IsPositiveInfinity(outlet.BirthAt)) return;

        CompiledConstructionActivation gate = Plan.Nodes[index].Activation;
        Vector3 forward = point - (Vector3)shell.Transform.GetColumn(3);
        forward.z = 0f;
        if (forward.sqrMagnitude < .000001f) forward = new Vector3(inputs.Forward.x, inputs.Forward.y, 0f);
        if (forward.sqrMagnitude < .000001f) forward = Vector3.right;
        outlet.BirthAt = Age + gate.Delay;
        outlet.Free = outlet.EventPlaced = true;
        outlet.EventPosition = point;
        outlet.EventRotation = Quaternion.LookRotation(forward, Vector3.forward);
        outlet.Authority = shell.Authority;
        QueueBirth(index);
    }
}
