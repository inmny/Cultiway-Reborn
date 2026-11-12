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
    internal bool BodyContactEnabled => !bodyInterrupted && !bodyResponse.HasValue;

    /// <summary>结果恢复与普通恢复各自真实的结束时点。</summary>
    internal float BodyUntil => bodyResponse.HasValue ? bodyResponse.Value.Until + bodyResponseDelay : Plan.Body?.Duration ?? 0f;

    /// <summary>只接受一次真实攻击结果，保留当刻姿态并立即作废旧攻击候选。</summary>
    /// <param name="result">来源结算已经确认的结果。</param>
    public bool RespondToBodyResult(BodyActionResult result)
    {
        if (!ControlsBody || bodyResponse.HasValue || Age > Plan.Body.ActiveUntil ||
            !Plan.Body.TryResponse(result, out CompiledBodyResponse rule)) return false;
        bodyResponse = new BodyResponseState(in rule, Age, BodyPose.Value.ActionAge, Plan.Body.RecoveryEnd);
        BodyContactRevision++;
        RecordActionStage(-1, AbilityActionEventKind.Recovering);
        return true;
    }
}
