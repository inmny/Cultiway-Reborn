using UnityEngine;

namespace Cultiway.Core.Skills;

/// <summary>实际结果改变本身体的恢复过程，独立主体仍用整次施放时钟。</summary>
public sealed partial class AbilityCast
{
    /// <summary>当前结果过程及预测前的纯数值副本。</summary>
    private BodyResponseState? bodyResponse, savedBodyResponse;

    /// <summary>实际空中运动占用的世界时间；随预测一起保存。</summary>
    private float bodyResponseDelay, savedBodyResponseDelay;

    /// <summary>供主线程运动在确认结果时接收同一已解析恢复状态。</summary>
    internal BodyResponseState? BodyResponse => bodyResponse;

    /// <summary>落地等待只延长身体控制，不延长独立主体。</summary>
    private float CastUntil => Mathf.Max(Plan.Lifetime, bodyInterrupted ? 0f : BodyUntil);

    /// <summary>主线程已经计算的实际落地等待，不读取游戏对象。</summary>
    /// <param name="delay">本次结果后累计空中世界秒数。</param>
    internal void SetBodyResponseDelay(float delay) => bodyResponseDelay = delay;

    /// <summary>身体已经进入哪种实际结果，空值表示仍沿原动作。</summary>
    public BodyActionResult? BodyResult => bodyResponse?.Result;

    /// <summary>实际收势期间保留显示与控制，但不能再次攻击。</summary>
    internal bool BodyContactEnabled => Plan.Body != null && !bodyInterrupted && !bodyResponse.HasValue &&
        exhaustedBodyStage != bodyStage && Plan.Body.IsActiveAt(Age);

    /// <summary>结果恢复与普通恢复各自真实的结束时点。</summary>
    internal float BodyUntil => bodyResponse.HasValue ? bodyResponse.Value.Until + bodyResponseDelay : Plan.Body?.Duration ?? 0f;

    /// <summary>先选择实际结果和连续收势，再停止依赖身体的攻击，最后记录新的恢复阶段。</summary>
    /// <param name="result">来源结算已经确认的结果。</param>
    public bool RespondToBodyResult(BodyActionResult result)
    {
        bool interrupted = result == BodyActionResult.Interrupted;
        if (!ControlsBody || !interrupted && (bodyResponse.HasValue ||
            result != BodyActionResult.SupportLost && Age > Plan.Body.ActiveUntil)) return false;
        if (interrupted && BodyResult == result) return true;
        if (!Plan.Body.TryResponse(result, out CompiledBodyResponse rule))
        {
            if (!interrupted) return false;
            rule = Plan.Body.InterruptionResponse;
        }
        CompiledBodyAction.Key key = Plan.Body.ResponseOrigin(Age - bodyResponseDelay, BodyPose.Value.ActionAge, bodyResponse);
        bodyResponse = new BodyResponseState(in rule, Age, BodyPose.Value.ActionAge, Plan.Body.RecoveryEnd, in key);
        bodyResponseDelay = 0f;
        BodyContactRevision++;
        if (interrupted)
        {
            CancelAwaiters(-1);
            if (nodes.Length > 0 && (Plan.Binding?.Anchor is AbilityAnchor.Hand or AbilityAnchor.Effector) && !nodes[0].Free)
                EndNode(0, true);
            FlushBirths(inputs.Target);
        }
        if (interrupted && (bodyActionStages & (1 << (int)AbilityActionEventKind.Recovering)) != 0)
            RecordActionEvent(-1, AbilityActionEventKind.Recovering, null, BodyPose.Value.Hand);
        else RecordActionStage(-1, AbilityActionEventKind.Recovering);
        return true;
    }
}
