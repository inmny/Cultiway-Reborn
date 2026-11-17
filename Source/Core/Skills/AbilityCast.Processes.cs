using UnityEngine;
using Cultiway.Core.Visuals.Forms;

namespace Cultiway.Core.Skills;

/// <summary>同一主体的成形与动作进度，形状、挂点和显示共用。</summary>
public sealed partial class AbilityCast
{
    /// <summary>仅为可切换动作的主体准备真实过渡起姿及其预测副本。</summary>
    private readonly FormPose[] transitionPoses, savedTransitionPoses;

    /// <summary>从事件发生时的实际尺寸开始，不回到原始生长起点。</summary>
    /// <param name="node">已存在部位。</param>
    /// <param name="operation">已经检查的目标及历时。</param>
    /// <param name="parameters">紧凑生长表中的实际参数。</param>
    private void StartGrowth(int node, CompiledEventOperation operation, CompiledGrowthParameters parameters)
    {
        ref NodeState state = ref nodes[node];
        state.GrowthOperation = operation;
        state.GrowthChanged = true;
        state.GrowthStopped = false;
        state.GrowthFrom = state.GrowthValue;
        state.GrowthTarget = parameters.Scale;
        state.GrowthSeconds = parameters.Seconds;
        state.GrowthAge = 0f;
    }

    /// <summary>记录当前完整局部姿态，由同一骨架连续过渡到新动作。</summary>
    /// <param name="node">实际形体。</param>
    /// <param name="operation">当前动作及过渡参数。</param>
    /// <param name="parameters">当前动作共享的已编译参数。</param>
    private void StartMotion(int node, CompiledEventOperation operation, CompiledMotionParameters parameters)
    {
        ref NodeState state = ref nodes[node];
        if (state.ActionStages != 0) RecordActionStage(node, AbilityActionEventKind.Interrupted);
        state.ActionInstance++;
        state.ActionStages = 0;
        state.NextActionMarker = 0;
        poses[node].CopyTo(transitionPoses[node]);
        state.Motion = operation;
        state.MotionData = parameters;
        state.MotionStopped = false;
        state.MotionAge = 0f;
        state.BlendAge = 0f;
        state.MotionHoldUsed = 0f;
        state.BlendAngularRate = 0f;
        for (int control = 0; control < operation.Motion.Rotations[0].Length; control++)
        {
            float angle = 0f;
            foreach (Quaternion[] key in operation.Motion.Rotations)
                angle = Mathf.Max(angle, Quaternion.Angle(transitionPoses[node].Rotations[control], key[control]));
            state.BlendAngularRate += angle * 1.5f / operation.BlendSeconds;
        }

        state.PreviousMotionAge = 0f;
        state.PreviousBlendAge = 0f;
        SampleMotion(node, 0f, 0f);
    }

    /// <summary>更新已确认的尺寸过程，停止及材料定型时保留真实尺寸。</summary>
    /// <param name="node">实际主体。</param>
    /// <param name="age">部位自己的世界时间。</param>
    /// <param name="delta">经过材料及维持处理的内部时间增量。</param>
    private float AdvanceGrowth(int node, float age, float delta)
    {
        ref NodeState state = ref nodes[node];
        CompiledForm form = Plan.Nodes[node];
        state.PreviousGrowth = state.GrowthValue;
        if (!state.GrowthStopped)
        {
            if (state.GrowthChanged)
            {
                state.GrowthAge += delta;
                state.GrowthValue = Mathf.Lerp(state.GrowthFrom, state.GrowthTarget,
                    Mathf.SmoothStep(0f, 1f, state.GrowthAge / state.GrowthSeconds));
            }
            else
                state.GrowthValue = form.Growth.x < 0
                    ? 1f
                    : Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(form.Growth.x, form.Growth.y, age));
        }

        state.GrowthValue = SnapSizeThreshold(node, state.PreviousGrowth, state.GrowthValue);
        return state.GrowthValue;
    }

    /// <summary>采样当前动作及过渡，所有局部旋转先混合再按层级建立矩阵。</summary>
    /// <param name="node">实际主体。</param>
    /// <param name="motionAge">当前动作内部秒数。</param>
    /// <param name="blendAge">实际已经过渡的秒数。</param>
    private void SampleMotion(int node, float motionAge, float blendAge)
    {
        NodeState state = nodes[node];
        CompiledForm form = Plan.Nodes[node];
        if (state.Motion == null) form.Resource.Rig.Sample(motionAge < 0 ? null : form.Motion, motionAge, poses[node]);
        else
            form.Resource.Rig.Sample(state.MotionData.Motion, motionAge, poses[node], transitionPoses[node],
                Mathf.SmoothStep(0f, 1f, blendAge / state.MotionData.Blend));
        if (state.MotionData?.PreserveBuild == true)
            poses[node].BuildProgress = transitionPoses[node].BuildProgress;
        if (state.Motion == null && motionAge < 0f && form.Motion?.FormsOverTime == true)
            poses[node].BuildProgress = form.Motion.Builds[0];
    }

    /// <summary>读取当前动作对应的阶段窗口，数值窗口仍使用部位世界时间。</summary>
    /// <param name="node">实际部位。</param>
    /// <param name="includeEnd">是否保留刚到结束边界的最后一次扫掠。</param>
    /// <param name="afterBoundary">阶段通知在窗口末端转入收势，保留碰撞自身的边界规则。</param>
    private bool ProcessIsActive(int node, bool includeEnd, bool afterBoundary = false)
    {
        CompiledForm form = Plan.Nodes[node];
        NodeState state = nodes[node];
        if (form.BodyWindow && (!BodyContactEnabled || afterBoundary && Age >= Plan.Body.Stages[bodyStage].Until))
            return false;
        float age = NodeAge(node);
        if ((form.ShapeActiveFrom || form.ShapeActiveUntil) && form.Motion == null && state.Motion == null)
            return false;
        float from = form.Active.x, until = form.Active.y;
        float fromAge = form.ShapeActiveFrom ? state.ShapeAge : age,
            untilAge = form.ShapeActiveUntil ? state.ShapeAge : age;
        float previous = state.PreviousShapeAge;
        if (state.Motion == null && form.Motion != null)
        {
            if (form.ShapeActiveFrom) fromAge = form.MotionAt + state.MotionAge / form.MotionSpeed;
            if (form.ShapeActiveUntil)
            {
                untilAge = form.MotionAt + state.MotionAge / form.MotionSpeed;
                previous = form.MotionAt + state.PreviousMotionAge / form.MotionSpeed;
            }
        }

        if (state.Motion != null)
        {
            if (form.ShapeActiveFrom)
            {
                from = state.MotionData.From;
                fromAge = state.MotionAge;
            }

            if (form.ShapeActiveUntil)
            {
                until = state.MotionData.Until;
                untilAge = state.MotionAge;
                previous = state.PreviousMotionAge;
            }
        }

        bool ended = form.ShapeActiveUntil
            ? untilAge >= until && !(includeEnd && previous < until && untilAge == until)
            : afterBoundary
                ? untilAge >= until
                : untilAge > until;
        return fromAge >= from && !ended;
    }
}
