using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际侵蚀使用普通操作、共同时间及已有份额扣除，不另建施放。</summary>
public sealed partial class AbilityCast
{
    /// <summary>读取实际材料阶段和已经退出的份额。</summary>
    /// <param name="node">真实生成部位。</param>
    public AbilityErosion Erosion(int node) => nodes[node].Weathering;

    /// <summary>从当前材料开始一段过程，后续替换保留已经发生的事实。</summary>
    /// <param name="action">本次操作身份。</param><param name="settings">入口核对的参数。</param>
    private bool StartErosion(CompiledEventOperation action, CompiledErosion settings)
    {
        ref NodeState state = ref nodes[action.Node];
        if (settings.World != null)
        {
            operations[action.Index].WorldRequested = true;
            return true;
        }
        if (!state.Exists || state.Leaving || state.Recovering || state.Volume <= 0 || state.Matter.ErosionFactor <= 0 ||
            !CanUseWorldMaterial(action.Node, 0, state.Volume * settings.Fraction * state.Matter.ErosionFactor)) return false;
        state.ErosionOperation = action;
        state.Erosion = settings;
        state.ErosionAt = Age;
        state.ErosionProgress = 0;
        return true;
    }

    /// <summary>中断不抹除已发生损耗，已经完成的事实保持可读。</summary>
    /// <param name="node">失去继续条件的部位。</param>
    private void InterruptErosion(int node)
    {
        if (nodes[node].ErosionProgress < 1) nodes[node].Erosion = null;
    }

    /// <summary>完成和替换分别沿共同步骤记录，不将时间到达当成世界回执。</summary>
    /// <param name="action">被查询的实际过程。</param>
    private ConstructionOperationProgress ErosionProgress(CompiledEventOperation action)
    {
        NodeState state = nodes[action.Node];
        bool same = ReferenceEquals(state.ErosionOperation, action);
        bool completed = same && state.ErosionProgress >= 1;
        return new ConstructionOperationProgress(completed, !same || !completed && state.Erosion == null);
    }

    /// <summary>先结算本段时间的旧材料，再由共同采样执行到点的替换与结束。</summary>
    private bool AdvanceErosion()
    {
        bool changed = false;
        for (int node = 0; node < nodes.Length; node++)
        {
            ref NodeState state = ref nodes[node];
            if (state.Erosion == null || state.ErosionProgress >= 1 || !state.Exists) continue;
            float progress = Mathf.Clamp01((Age - state.ErosionAt) / state.Erosion.Seconds);
            double loss = state.Erosion.Fraction * (double)state.Matter.ErosionFactor;
            double retained = CompiledErosion.Retained(loss, state.ErosionProgress, progress);
            double removed = state.Volume * Math.Max(0, 1 - retained);
            // 很小的一步累计到足以改变实际形体时再扣除，不把舍入掉的变化记为已发生。
            if (!CanUseWorldMaterial(node, 0, removed))
            {
                if (progress >= 1) InterruptErosion(node);
                continue;
            }
            state.ErosionProgress = progress;
            double before = state.Volume;
            double actual = UseWorldMaterial(node, 0, removed, state.Position, out double resource);
            state.Weathering.Add(before, actual, resource);
            changed |= actual > 0;
        }
        return changed;
    }
}
