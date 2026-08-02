using Friflo.Engine.ECS;
using Cultiway.Core.SkillLibV3.Components;

namespace Cultiway.Core.SkillLibV3;

/// <summary>施法序列扣除资源的时机。</summary>
public enum SkillCastPaymentTiming
{
    /// <summary>序列启动前一次性支付全部步骤，保持现有法术的默认语义。</summary>
    Upfront,

    /// <summary>每个步骤实际发射前单独支付；跳过、延迟或取消的步骤不产生消耗。</summary>
    PerEmission,
}

/// <summary>动态步骤钩子对当前步骤作出的决定。</summary>
public enum SkillCastStepDecisionKind
{
    /// <summary>使用返回的步骤继续发射。</summary>
    Emit,

    /// <summary>永久跳过当前步骤，不支付资源。</summary>
    Skip,

    /// <summary>暂时保留当前步骤，等待后续 Tick 再次判断。</summary>
    Defer,

    /// <summary>取消当前步骤及全部剩余步骤。</summary>
    Cancel,
}

/// <summary>施法序列停止的原因。</summary>
public enum SkillCastSequenceEndReason
{
    /// <summary>全部计划步骤均已发射或跳过。</summary>
    Completed,

    /// <summary>施法者或技能容器在执行期间失效。</summary>
    Invalidated,

    /// <summary>动态步骤钩子主动终止了序列。</summary>
    Cancelled,

    /// <summary>逐发支付时已经无法支付下一步。</summary>
    InsufficientResource,
}

/// <summary>传给序列开始钩子的只读上下文。</summary>
public readonly struct SkillCastSequenceStartContext
{
    public readonly ActorExtend Caster;
    public readonly Entity SkillContainer;
    public readonly SkillCastPlan Plan;
    public readonly float Strength;
    public readonly float PowerLevel;
    public readonly SkillCastFundingSource FundingSource;
    public readonly SkillCastRuntimeData RuntimeData;

    public SkillCastSequenceStartContext(
        ActorExtend caster,
        Entity skillContainer,
        SkillCastPlan plan,
        float strength,
        float powerLevel,
        SkillCastFundingSource fundingSource,
        SkillCastRuntimeData runtimeData)
    {
        Caster = caster;
        SkillContainer = skillContainer;
        Plan = plan;
        Strength = strength;
        PowerLevel = powerLevel;
        FundingSource = fundingSource;
        RuntimeData = runtimeData;
    }
}

/// <summary>传给逐步骤钩子的只读上下文。</summary>
public readonly struct SkillCastSequenceStepContext
{
    public readonly ActorExtend Caster;
    public readonly Entity SkillContainer;
    public readonly int StepIndex;
    public readonly int EmittedCount;
    public readonly float Elapsed;
    public readonly SkillCastRuntimeData RuntimeData;

    public SkillCastSequenceStepContext(
        ActorExtend caster,
        Entity skillContainer,
        int stepIndex,
        int emittedCount,
        float elapsed,
        SkillCastRuntimeData runtimeData)
    {
        Caster = caster;
        SkillContainer = skillContainer;
        StepIndex = stepIndex;
        EmittedCount = emittedCount;
        Elapsed = elapsed;
        RuntimeData = runtimeData;
    }
}

/// <summary>动态步骤钩子返回的不可变步骤决定。</summary>
public readonly struct SkillCastStepDecision
{
    public readonly SkillCastStepDecisionKind Kind;
    public readonly SkillCastStep Step;

    private SkillCastStepDecision(SkillCastStepDecisionKind kind, SkillCastStep step)
    {
        Kind = kind;
        Step = step;
    }

    /// <summary>发射指定步骤；可借此在支付前替换目标或落点。</summary>
    public static SkillCastStepDecision Emit(SkillCastStep step) =>
        new(SkillCastStepDecisionKind.Emit, step);

    /// <summary>永久跳过当前步骤。</summary>
    public static SkillCastStepDecision Skip() =>
        new(SkillCastStepDecisionKind.Skip, default);

    /// <summary>把当前步骤延迟到后续 Tick。</summary>
    public static SkillCastStepDecision Defer() =>
        new(SkillCastStepDecisionKind.Defer, default);

    /// <summary>取消整个序列。</summary>
    public static SkillCastStepDecision Cancel() =>
        new(SkillCastStepDecisionKind.Cancel, default);
}

/// <summary>传给序列结束钩子的执行结果。</summary>
public readonly struct SkillCastSequenceResult
{
    public readonly ActorExtend Caster;
    public readonly Entity SkillContainer;
    public readonly int EmittedCount;
    public readonly int ProcessedStepCount;
    public readonly SkillCastSequenceEndReason Reason;
    public readonly SkillCastRuntimeData RuntimeData;

    public SkillCastSequenceResult(
        ActorExtend caster,
        Entity skillContainer,
        int emittedCount,
        int processedStepCount,
        SkillCastSequenceEndReason reason,
        SkillCastRuntimeData runtimeData)
    {
        Caster = caster;
        SkillContainer = skillContainer;
        EmittedCount = emittedCount;
        ProcessedStepCount = processedStepCount;
        Reason = reason;
        RuntimeData = runtimeData;
    }
}

/// <summary>
/// 为持续施法提供开始校验、逐步骤重定向和结束通知。
/// <see cref="PrepareStep"/> 在 ECS 查询期间调用，不得直接执行 ECS 结构变更；其余回调在查询外调用。
/// </summary>
public interface ISkillCastSequenceHooks
{
    /// <summary>在支付和创建序列前完成最后一次主线程校验。</summary>
    bool CanStart(in SkillCastSequenceStartContext context);

    /// <summary>序列实体创建完成后建立内容侧运行状态。</summary>
    void OnStarted(in SkillCastSequenceStartContext context);

    /// <summary>在当前步骤支付资源前校验并按实时战况解析目标。</summary>
    SkillCastStepDecision PrepareStep(
        in SkillCastSequenceStepContext context,
        in SkillCastStep scheduledStep);

    /// <summary>序列停止且最后一批实体已生成后清理内容侧运行状态。</summary>
    void OnEnded(in SkillCastSequenceResult result);
}

/// <summary>施法序列的可选执行策略；未提供时完全保留原有行为。</summary>
public sealed class SkillCastSequenceOptions
{
    /// <summary>资源支付时机。</summary>
    public SkillCastPaymentTiming PaymentTiming { get; set; } = SkillCastPaymentTiming.Upfront;

    /// <summary>可选的动态生命周期钩子。</summary>
    public ISkillCastSequenceHooks Hooks { get; set; }

    /// <summary>单个序列每个 Tick 最多生成的实体数量。</summary>
    public int MaxEmitPerTick { get; set; } = 8;
}
