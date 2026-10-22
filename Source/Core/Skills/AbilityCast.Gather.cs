using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>保留部件身份的布局聚合；作用形状与显示读取同一个局部姿态。</summary>
public sealed partial class AbilityCast
{
    /// <summary>只在计划实际含聚合时分配，普通施放不保存额外布局。</summary>
    private readonly GatherState[] gathers, savedGathers;

    /// <summary>从当时布局开始汇入，已经解除的连接不会暗中恢复。</summary>
    /// <param name="node">仍连接父体的真实部件。</param>
    /// <param name="gather">完整目标布局。</param>
    /// <param name="at">已经确认的开始时点。</param>
    private void StartGather(int node, CompiledGather gather, float at)
    {
        if (nodes[node].Free) return;
        ref GatherState state = ref gathers[node];
        state.From = state.Offset; state.FromRotation = state.Rotation;
        state.Process = gather; state.StartedAt = at;
        state.AngularRate = 1.5f * Quaternion.Angle(state.FromRotation, gather.Rotation) / gather.Seconds;
    }

    /// <summary>先开始初始过程，再以共同世界时间采样位置和朝向，保持完整尺寸及独立旋转。</summary>
    /// <param name="node">实际部件。</param>
    /// <param name="age">该分支的世界年龄。</param>
    private void SampleGather(int node, float age)
    {
        ref GatherState state = ref gathers[node]; CompiledForm form = Plan.Nodes[node];
        if (nodes[node].Free) return;
        if (!state.InitialStarted && form.Gather != null && age >= form.Gather.At)
        {
            state.InitialStarted = true;
            StartGather(node, form.Gather, nodes[node].StartedAt + form.Gather.At);
        }
        if (state.Process == null) return;
        float phase = Mathf.Clamp01((Age - state.StartedAt) / state.Process.Seconds);
        float t = Mathf.SmoothStep(0f, 1f, phase);
        state.Offset = Vector3.Lerp(state.From, state.Process.Target, t);
        state.Rotation = Quaternion.Slerp(state.FromRotation, state.Process.Rotation, t);
    }

    /// <summary>聚合只保存局部布局和时点，父体运动由共同连接继续提供。</summary>
    private struct GatherState
    {
        /// <summary>初始操作是否已经执行；条件操作不会把它重新排队。</summary>
        internal bool InitialStarted;
        /// <summary>本次正在采用的完整目标。</summary>
        internal CompiledGather Process;
        /// <summary>真实开始时点及保守转动速度。</summary>
        internal float StartedAt, AngularRate;
        /// <summary>当前及开始时的局部位置。</summary>
        internal Vector3 Offset, From;
        /// <summary>当前及开始时的局部朝向。</summary>
        internal Quaternion Rotation, FromRotation;
    }
}
