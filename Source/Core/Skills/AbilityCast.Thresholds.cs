using System;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>按真实过程越过尺寸边界，所有判断仍使用同一数值状态。</summary>
public sealed partial class AbilityCast
{
    /// <summary>对相邻阈值二分定位，避免每步遍历全部订阅者。</summary>
    /// <param name="values">已经排序的不同阈值。</param>
    /// <param name="value">当前实际尺寸。</param>
    /// <param name="up">是否正在长大。</param>
    private static int NextSizeIndex(float[] values, float value, bool up)
    {
        int index = Array.BinarySearch(values, value);
        return up ? index >= 0 ? index + 1 : ~index : index >= 0 ? index - 1 : ~index - 1;
    }

    /// <summary>将平滑生长进度反解为真正达到目标尺寸的局部时点。</summary>
    /// <param name="node">正在成形的来源。</param>
    /// <param name="maximum">其他共同边界允许的步长。</param>
    private float SizeThresholdStep(int node, float maximum)
    {
        NodeState state = nodes[node]; CompiledForm form = Plan.Nodes[node]; float[] values = form.SizeThresholds;
        if (values.Length == 0 || state.GrowthStopped || state.GrowthChanged && (state.ShapeHeld ||
            state.HoldUsed < form.Hold.Seconds && state.ShapeAge >= form.Hold.At)) return maximum;
        float from = state.GrowthChanged ? state.GrowthFrom : 0f, target = state.GrowthChanged ? state.GrowthTarget : 1f;
        if (target == state.GrowthValue || target == from) return maximum;
        bool up = target > state.GrowthValue; int index = NextSizeIndex(values, state.GrowthValue, up);
        if (index < 0 || index >= values.Length || up && values[index] > target || !up && values[index] < target) return maximum;
        float ratio = Mathf.Clamp01((values[index] - from) / (target - from));
        float phase = .5f - Mathf.Sin(Mathf.Asin(1f - 2f * ratio) / 3f);
        float at = state.GrowthChanged ? Age + (state.GrowthSeconds * phase - state.GrowthAge) :
            state.StartedAt + Mathf.Lerp(form.Growth.x, form.Growth.y, phase);
        return at > Age && at < Age + maximum ? at - Age : maximum;
    }

    /// <summary>消除反解及采样在同一边界的微小舍入差，使模型和事件确实达到同一值。</summary>
    /// <param name="node">实际主体。</param>
    /// <param name="before">本步之前的真实倍率。</param>
    /// <param name="value">当前过程算出的倍率。</param>
    private float SnapSizeThreshold(int node, float before, float value)
    {
        float[] values = Plan.Nodes[node].SizeThresholds;
        if (values.Length == 0 || before == value) return value;
        int index = NextSizeIndex(values, before, value > before);
        return index >= 0 && index < values.Length && Mathf.Abs(value - values[index]) <= .000001f * Mathf.Max(1f, Mathf.Abs(values[index])) ? values[index] : value;
    }

    /// <summary>姿态及实际形状完成后才通知成长，不把出生时的初始尺寸当作过程。</summary>
    /// <param name="node">已经采样到共同时间的来源。</param>
    private void SignalSizeChange(int node)
    {
        NodeState state = nodes[node]; float[] values = Plan.Nodes[node].SizeThresholds;
        if (values.Length == 0 || state.PreviousGrowth == state.GrowthValue) return;
        bool up = state.GrowthValue > state.PreviousGrowth; int index = NextSizeIndex(values, state.PreviousGrowth, up);
        if (index < 0 || index >= values.Length || up && state.GrowthValue < values[index] || !up && state.GrowthValue > values[index]) return;
        SignalEvent(node, up ? ConstructionChangeTrigger.SizeAbove : ConstructionChangeTrigger.SizeBelow,
            state.Transform.GetColumn(3), state.PreviousGrowth, state.GrowthValue);
    }
}
