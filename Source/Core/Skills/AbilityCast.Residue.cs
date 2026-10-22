using System;
using System.Collections.Generic;
using Cultiway.Core.Visuals.Forms;
using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>真实生成材料的世界转交请求，预测只冻结状态，主线程接受后才移走材料。</summary>
public sealed partial class AbilityCast
{
    /// <summary>开始留置即停止攻击和运动，等待事实维护者接受。</summary>
    /// <param name="node">真实生成部位。</param>
    private void StartLeave(int node)
    {
        ref NodeState state = ref nodes[node];
        if (state.Leaving || state.Recovering || !state.Exists) return;
        ReleaseNode(node);
        for (int child = node + 1; child < nodes.Length; child++)
            if (IsAttachedBelow(child, node))
                ReleaseNode(child);
        state.Leaving = true;
        state.Force = 0;
        state.Revision++;
        state.Flying = false;
        state.MotionStopped = state.GrowthStopped = state.MovementStopped = true;
        state.Velocity = state.Acceleration = state.Spin = Vector3.zero;
    }

    /// <summary>读取已确认时点的转交请求，取消或清世界后不产生旧事实。</summary>
    /// <param name="node">展开部位。</param>
    internal bool HasLeave(int node) => !cancelled && nodes[node].Exists && nodes[node].Leaving;

    /// <summary>世界材料选择只接受一次，预测回退会恢复领取标记。</summary>
    /// <param name="node">正在收束的部位。</param>
    internal bool HasWorldRecovery(int node) => !cancelled && nodes[node].Recovering &&
                                                nodes[node].Recovery.WorldMaterials && !nodes[node].WorldRecoveryTaken;

    /// <summary>实际开始的收束参数，条件和定时共用。</summary>
    /// <param name="node">正在收束的部位。</param>
    internal CompiledRecovery RecoveryFor(int node) => nodes[node].Recovery;

    /// <summary>主线程已接受选择请求，不在以后每帧重选同一份材料。</summary>
    /// <param name="node">实际部位。</param>
    internal void TakeWorldRecovery(int node) => nodes[node].WorldRecoveryTaken = true;

    /// <summary>正式确认后建立独立材料；不把骨架和水深工作数组交给两个所有者同时修改。</summary>
    /// <param name="node">正在交出的部位。</param>
    internal AbilityMaterial CaptureMaterial(int node)
    {
        ref NodeState state = ref nodes[node];
        CompiledForm form = Plan.Nodes[node];
        AbilityFlow flow = null;
        if (flows?[node]?.Started == true)
        {
            flow = new AbilityFlow(flows[node].Settings);
            flows[node].CopyTo(flow);
        }

        return new AbilityMaterial(form.Resource, state.Matter, state.Transform,
            poses[node] == null ? null : (Matrix4x4[])poses[node].Skin.Clone(),
            poses[node]?.Bounds,
            flow == null
                ? AbilityMaterial.BuildShapes(form.Resource, poses[node], state.Transform)
                : Array.Empty<AbilityCapsule>(),
            form.Surface, form.Dye, state.Volume, state.Resource, flow, state.Burning);
    }

    /// <summary>另一施放按同一实际形状选择材料，零攻击力量不等于没有物理范围。</summary>
    /// <param name="node">选择部位。</param><param name="output">实际空间缓冲。</param>
    internal void MaterialSelection(int node, List<AbilityCapsule> output)
    {
        if (cancelled || !nodes[node].Exists) return;
        if (flows?[node]?.Started == true) AppendFlowContacts(node, output, false);
        else
            output.AddRange(AbilityMaterial.BuildShapes(Plan.Nodes[node].Resource, poses[node], nodes[node].Transform));
    }

    /// <summary>世界维护者已经接受，源部位清零并按正常结束规则处理后段。</summary>
    /// <param name="node">实际被接受的部位。</param>
    internal void CompleteLeave(int node)
    {
        ref NodeState state = ref nodes[node];
        state.Leaving = false;
        state.Volume = 0;
        state.Resource = 0;
        EndNode(node);
        FlushBirths(inputs.Target);
    }
}