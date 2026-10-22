using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际材料的有限燃烧，数值推进不访问人物或创建特效。</summary>
public sealed partial class AbilityCast
{
    /// <summary>读取本部位的真实热量与累计损耗。</summary>
    /// <param name="node">实际展开部位。</param>
    public AbilityBurning Burning(int node) => nodes[node].Burning;

    /// <summary>本次预测起点已有的可传热量，不提前使用本步尚未烧出的热量。</summary>
    /// <param name="node">真实热源。</param>
    internal double StartHeat(int node)
    {
        NodeState state = savedNodes[node];
        return state.Matter.Id == "fire" && state.Matter.State == "energy"
            ? state.Force
            : state.Burning.Transferable(state.Volume);
    }

    /// <summary>读取跟随实际主体的热接触位置，结束结果也保留最后主体变换。</summary>
    /// <param name="node">真实部位。</param>
    internal Vector3 BurningPoint(int node)
    {
        var state = nodes[node];
        return state.Transform.MultiplyPoint3x4(new Vector3(state.Burning.X, state.Burning.Y, state.Burning.Z));
    }

    /// <summary>确认接触时只补充尚缺的点火量。</summary>
    /// <param name="node">真实木体。</param><param name="available">来源可支付的热量。</param>
    /// <param name="source">实际加热来源。</param>
    /// <param name="point">实际世界接触点。</param>
    internal double HeatWood(int node, double available, long source, Vector3 point)
    {
        ref NodeState state = ref nodes[node];
        if (!state.Exists || !AbilityBurning.CanBurn(state.Matter) || state.Volume <= 0) return 0;
        double amount = Math.Min(available, state.Burning.Needed(state.Volume));
        if (amount <= 0) return 0;
        InterruptThermalRecovery(node);
        state.Burning.AddHeat(state.Volume, amount, source);
        state.Revision++;
        Vector3 local = state.Transform.inverse.MultiplyPoint3x4(point);
        state.Burning.X = local.x;
        state.Burning.Y = local.y;
        state.Burning.Z = local.z;
        return amount;
    }

    /// <summary>水冷却或传播扣除已经传出的热量，不能消费原攻击力代替燃烧热量。</summary>
    /// <param name="node">真实热主体。</param><param name="amount">实际扣除的热量。</param>
    internal void CoolWood(int node, double amount)
    {
        nodes[node].Burning.Cool(amount);
        nodes[node].Revision++;
    }

    /// <summary>按实际存续部分烧损；烧尽支撑时先释放完整子部位，再结束源体。</summary>
    /// <param name="seconds">本次数值世界步长。</param>
    private bool AdvanceBurning(float seconds)
    {
        bool changed = false;
        for (int node = 0; node < nodes.Length; node++)
        {
            ref NodeState state = ref nodes[node];
            if (!state.Exists || state.Leaving || !state.Burning.Lit) continue;
            if (!AbilityBurning.CanBurn(state.Matter))
            {
                state.Burning.Extinguish();
                continue;
            }

            double before = state.Volume;
            double removed = state.Burning.Advance(Mathf.Min(seconds, Mathf.Max(0, Age - state.StartedAt)), before);
            if (removed <= 0) continue;
            changed = true;
            float retained = (float)Math.Max(0, 1 - removed / before);
            state.Volume = before - removed;
            if (flows?[node]?.Started == true)
            {
                flows[node].RetainMaterial(retained);
                state.Volume = flows[node].Volume;
            }
            else
            {
                state.MaterialRetained *= retained;
                state.Transform *= Matrix4x4.Scale(Vector3.one * Mathf.Pow(retained, 1f / 3f));
            }

            state.Resource *= retained;
            float force = state.Force;
            state.Force *= retained;
            state.Revision++;
            if (force > state.Force && state.InitialForce > 0)
                SignalEvent(node, ConstructionChangeTrigger.ForceBelow, state.Position, force / state.InitialForce,
                    state.Force / state.InitialForce);
            if (state.Volume > Math.Max(1e-10, state.Burning.Fuel * 1e-6)) continue;
            for (int child = node + 1; child < nodes.Length; child++)
                if (nodes[child].Exists && !nodes[child].Free && Plan.Nodes[child].Parent == node)
                    ReleaseNode(child);
            state.Volume = state.Resource = 0;
            state.Force = 0;
            state.Burning.Extinguish();
            EndNode(node);
        }

        if (changed) FlushBirths(inputs.Target);
        return changed;
    }

    /// <summary>真实热接触中断未完成收束，已完成回能不重算。</summary>
    /// <param name="node">仍有实际材料的部位。</param>
    private void InterruptThermalRecovery(int node)
    {
        ref NodeState state = ref nodes[node];
        if (!state.Recovering) return;
        state.Recovering = false;
        state.RecoveryInterrupted = true;
        state.Scale = state.Transform.lossyScale / Mathf.Max(.00001f, Mathf.Pow(state.MaterialRetained, 1f / 3f));
        state.GrowthValue = state.PreviousGrowth = 1f;
        if (flows?[node]?.Started == true) state.MovementStopped = false;
    }
}