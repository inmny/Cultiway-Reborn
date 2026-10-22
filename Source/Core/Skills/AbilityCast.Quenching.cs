using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>确认水火接触后扣除材料；预测阶段不发布蒸散。</summary>
public sealed partial class AbilityCast
{
    /// <summary>按实际接触格或完整生成水体扣除有限水量。</summary>
    /// <param name="node">真正接触的部位。</param><param name="shape">实际形状或流散格编号。</param>
    /// <param name="maximum">火力允许的最大蒸散量。</param><param name="point">共同确认的接触位置。</param>
    internal double EvaporateWater(int node, int shape, double maximum, Vector3 point)
    {
        ref NodeState state = ref nodes[node];
        if (!state.Exists || state.Matter.Id != "water" || state.Matter.State != "liquid") return 0;
        double before = state.Volume;
        if (before <= 0) return 0;
        if (state.Recovering && maximum > 0)
        {
            // 真实火作用打断尚未完成的收束；已经完成的回能不重算，剩余水也不再按旧基数补满。
            state.Recovering = false;
            state.RecoveryInterrupted = true;
            state.Scale = state.Transform.lossyScale / Mathf.Max(.00001f, Mathf.Pow(state.MaterialRetained, 1f / 3f));
            state.GrowthValue = state.PreviousGrowth = 1f;
            if (flows?[node]?.Started == true) state.MovementStopped = false;
        }

        var flow = flows?[node];
        double removed;
        if (flow?.Started == true)
        {
            int cell = shape - Plan.Nodes[node].Resource.Contacts.Length;
            removed = flow.Evaporate(cell, maximum);
            state.Volume = flow.Volume;
        }
        else
        {
            removed = Math.Min(before, maximum);
            state.Volume -= removed;
            float retained = (float)Math.Max(0, 1 - removed / before);
            state.MaterialRetained *= retained;
            state.Transform *= Matrix4x4.Scale(Vector3.one * Mathf.Pow(retained, 1f / 3f));
        }

        if (removed <= 0) return 0;
        float ratio = (float)Math.Max(0, 1 - removed / before);
        state.Resource *= ratio;
        // 水的攻击力随实际材料损失；不再按力量损失重复扣一次支付。
        float force = state.Force;
        state.Force *= ratio;
        state.Revision++;
        if (force > state.Force && state.InitialForce > 0)
            SignalEvent(node, ConstructionChangeTrigger.ForceBelow, point, force / state.InitialForce,
                state.Force / state.InitialForce);
        if (state.Volume <= before * .0000001)
        {
            state.Volume = 0;
            state.Resource = 0;
            EndNode(node);
        }

        FlushBirths(inputs.Target);
        return removed;
    }
}