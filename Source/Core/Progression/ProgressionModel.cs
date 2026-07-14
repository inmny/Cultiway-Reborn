using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.Progression;

/// <summary>
///     进阶改变的层级。小境界不改变修炼体系的主等级，大境界会切换主等级。
/// </summary>
public enum ProgressionKind
{
    /// <summary>同一主等级内的进展，例如筑基五气或金丹淬炼；提交后不改写 CurrLevel。</summary>
    Minor,

    /// <summary>跨越主等级的进展，例如练气进入筑基；提交后把 CurrLevel 写为 TargetLevel。</summary>
    Major
}

/// <summary>
///     进阶入口。不同入口共用结构变换，但是否检查条件、扣除消耗和发放奖励不同。
/// </summary>
public enum ProgressionMode
{
    /// <summary>角色按正常玩法尝试进阶；检查全部关卡，并执行尝试消耗、成功消耗和对应结算。</summary>
    Natural,

    /// <summary>管理或奖励入口；跳过自然关卡和消耗，但执行必要结构变换、奖励及提交事件。</summary>
    Grant,

    /// <summary>状态校准入口；允许升降或修复境界，只执行结构变换与幂等修复，不重放奖励和提交事件。</summary>
    Synchronize,

    /// <summary>角色间传承入口；复制体系组件及专属附加状态，再按目标境界执行幂等修复。</summary>
    Transfer
}

/// <summary>
///     前置阶段的当前状态。
/// </summary>
public enum ProgressionGateState
{
    /// <summary>本关卡已经满足，可以继续检查下一关卡或进入突破结算。</summary>
    Satisfied,

    /// <summary>可恢复的即时条件尚未满足，例如资源未满；当前不应启动长期工作。</summary>
    NotReady,

    /// <summary>长期阶段尚未启动；执行入口可以调用 IProgressionStage.Start 创建外部工作。</summary>
    NeedsStart,

    /// <summary>长期阶段已经启动但尚未完成；等待外部任务或世界状态继续推进。</summary>
    InProgress,

    /// <summary>存在硬性阻断，例如缺少不可替代组件或目标非法；核心不会尝试自动解除。</summary>
    Blocked
}

/// <summary>
///     一次突破判定的结果。判定本身只产生数据，实际修改由过渡定义中的效果列表完成。
/// </summary>
public enum ProgressionResolutionState
{
    /// <summary>本次判定成功，继续执行成功消耗、结构变换和奖励。</summary>
    Success,

    /// <summary>本次判定失败，不发生进阶；自然模式下执行 FailureEffects。</summary>
    Failure,

    /// <summary>本次判定合法但没有可提交的进展；自然模式下执行 NoProgressEffects。</summary>
    NoProgress
}

/// <summary>
///     对外可观察的进阶结果。
/// </summary>
public enum ProgressionResultCode
{
    /// <summary>角色未拥有体系、当前境界没有候选过渡，或指定类型的过渡不存在。</summary>
    NotAvailable,

    /// <summary>候选过渡存在，但接近度或可恢复的即时条件尚未满足。</summary>
    NotReady,

    /// <summary>请求因硬性条件、非法目标或不完整的进阶定义而被拒绝。</summary>
    Blocked,

    /// <summary>准备阶段原本未启动，本次调用已经请求启动该阶段。</summary>
    PreparationStarted,

    /// <summary>准备阶段已经在外部系统中运行，本次没有进行突破结算。</summary>
    PreparationInProgress,

    /// <summary>挑战阶段原本未启动，本次调用已经请求启动该阶段。</summary>
    ChallengeStarted,

    /// <summary>挑战阶段已经在外部系统中运行，本次没有进行突破结算。</summary>
    ChallengeInProgress,

    /// <summary>突破结算明确失败，可能已经执行失败效果，但没有提交进阶。</summary>
    Failed,

    /// <summary>调用成功完成判定，但当前没有可提交进展，例如小境界已达到上限。</summary>
    NoProgress,

    /// <summary>小境界进展已经提交，CurrLevel 保持不变。</summary>
    MinorAdvanced,

    /// <summary>大境界进展已经提交，CurrLevel 已切换到目标等级。</summary>
    MajorAdvanced,

    /// <summary>角色已被校准到目标境界并执行该境界的幂等结构修复。</summary>
    Synchronized,

    /// <summary>来源角色的体系组件和附加状态已经复制到目标角色并完成修复。</summary>
    Transferred
}

/// <summary>
///     纯查询使用的关卡结果。WorkOrderId 供扩展模块把准备过程映射到自己的任务系统。
/// </summary>
public readonly struct ProgressionGateResult
{
    /// <summary>创建一项关卡检查结果。</summary>
    /// <param name="state">关卡当前状态。</param>
    /// <param name="reason">可选的稳定原因键，供日志、UI 或调用方判断。</param>
    /// <param name="workOrderId">可选的外部工作标识，用于追踪准备或挑战流程。</param>
    public ProgressionGateResult(ProgressionGateState state, string reason = null, string workOrderId = null)
    {
        State = state;
        Reason = reason;
        WorkOrderId = workOrderId;
    }

    /// <summary>关卡当前所处的状态。</summary>
    public ProgressionGateState State { get; }

    /// <summary>状态原因的稳定键；没有补充原因时为 null。</summary>
    public string Reason { get; }

    /// <summary>准备或挑战阶段对应的外部工作标识；不涉及外部工作时为 null。</summary>
    public string WorkOrderId { get; }

    /// <summary>创建一个已满足的关卡结果。</summary>
    public static ProgressionGateResult Satisfied => new(ProgressionGateState.Satisfied);

    /// <summary>创建一个即时条件尚未满足的关卡结果。</summary>
    public static ProgressionGateResult NotReady(string reason = null) =>
        new(ProgressionGateState.NotReady, reason);

    /// <summary>创建一个需要启动外部工作的关卡结果。</summary>
    public static ProgressionGateResult NeedsStart(string reason = null, string workOrderId = null) =>
        new(ProgressionGateState.NeedsStart, reason, workOrderId);

    /// <summary>创建一个外部工作仍在执行的关卡结果。</summary>
    public static ProgressionGateResult InProgress(string reason = null, string workOrderId = null) =>
        new(ProgressionGateState.InProgress, reason, workOrderId);

    /// <summary>创建一个被硬性条件阻断的关卡结果。</summary>
    public static ProgressionGateResult Blocked(string reason = null) =>
        new(ProgressionGateState.Blocked, reason);
}

/// <summary>
///     准备阶段和挑战阶段收到的稳定上下文，不暴露具体修炼组件类型。
/// </summary>
public sealed class ProgressionStageContext
{
    internal ProgressionStageContext(ActorExtend actor, BaseCultisysAsset cultisys, string transitionId,
                                      ProgressionKind kind, ProgressionMode mode, int fromLevel, int targetLevel)
    {
        Actor = actor;
        Cultisys = cultisys;
        TransitionId = transitionId;
        Kind = kind;
        Mode = mode;
        FromLevel = fromLevel;
        TargetLevel = targetLevel;
    }

    /// <summary>正在尝试进阶的角色扩展对象。</summary>
    public ActorExtend Actor { get; }

    /// <summary>拥有当前进阶定义的修炼体系资产。</summary>
    public BaseCultisysAsset Cultisys { get; }

    /// <summary>当前过渡的稳定唯一标识。</summary>
    public string TransitionId { get; }

    /// <summary>当前过渡属于小境界还是大境界。</summary>
    public ProgressionKind Kind { get; }

    /// <summary>触发本阶段检查的进阶入口。</summary>
    public ProgressionMode Mode { get; }

    /// <summary>开始本次流程时角色的主等级。</summary>
    public int FromLevel { get; }

    /// <summary>过渡声明的目标主等级；小境界通常与 FromLevel 相同。</summary>
    public int TargetLevel { get; }
}

/// <summary>
///     可选的异步进阶阶段。Evaluate 必须是无副作用查询；只有返回 NeedsStart 时核心才调用 Start。
///     扩展模块自行持有过程状态，并可通过 WorkOrderId 接入自己的任务驱动。
/// </summary>
public interface IProgressionStage
{
    /// <summary>无副作用地读取阶段状态；不得在此创建任务、扣除资源或修改角色。</summary>
    ProgressionGateResult Evaluate(ProgressionStageContext context);

    /// <summary>创建或驱动阶段所需的外部工作；仅在 Evaluate 返回 NeedsStart 后由核心调用。</summary>
    void Start(ProgressionStageContext context);
}

/// <summary>
///     突破判定产生的载荷。载荷用于把随机判定与后续结构变换、奖励和失败效果分开。
/// </summary>
public readonly struct ProgressionResolution
{
    /// <summary>创建一次突破判定结果。</summary>
    /// <param name="state">判定状态。</param>
    /// <param name="payload">传给后续效果列表的只读判定数据。</param>
    /// <param name="reason">可选的稳定原因键。</param>
    public ProgressionResolution(ProgressionResolutionState state, object payload = null, string reason = null)
    {
        State = state;
        Payload = payload;
        Reason = reason;
    }

    /// <summary>本次判定成功、失败或无进展。</summary>
    public ProgressionResolutionState State { get; }

    /// <summary>供消耗、结构变换、奖励或失败效果使用的判定数据；没有数据时为 null。</summary>
    public object Payload { get; }

    /// <summary>判定结果的稳定原因键；没有补充原因时为 null。</summary>
    public string Reason { get; }

    /// <summary>创建成功判定，并可携带后续结算数据。</summary>
    public static ProgressionResolution Success(object payload = null) =>
        new(ProgressionResolutionState.Success, payload);

    /// <summary>创建失败判定；自然模式会进入 FailureEffects。</summary>
    public static ProgressionResolution Failure(object payload = null, string reason = null) =>
        new(ProgressionResolutionState.Failure, payload, reason);

    /// <summary>创建无进展判定；自然模式会进入 NoProgressEffects。</summary>
    public static ProgressionResolution NoProgress(object payload = null, string reason = null) =>
        new(ProgressionResolutionState.NoProgress, payload, reason);
}

/// <summary>
///     进阶状态查询结果，不会执行随机判定或修改角色。
/// </summary>
public readonly struct ProgressionQuery
{
    /// <summary>创建一份无副作用的进阶查询快照。</summary>
    public ProgressionQuery(bool available, string transitionId, ProgressionKind kind, int fromLevel,
                            int targetLevel, bool approaching, ProgressionGateResult gate)
    {
        Available = available;
        TransitionId = transitionId;
        Kind = kind;
        FromLevel = fromLevel;
        TargetLevel = targetLevel;
        Approaching = approaching;
        Gate = gate;
    }

    /// <summary>当前境界是否声明了可供查询的候选过渡。</summary>
    public bool Available { get; }

    /// <summary>候选过渡的稳定标识；Available 为 false 时为 null。</summary>
    public string TransitionId { get; }

    /// <summary>候选过渡属于小境界还是大境界。</summary>
    public ProgressionKind Kind { get; }

    /// <summary>查询时角色的主等级。</summary>
    public int FromLevel { get; }

    /// <summary>候选过渡声明的目标主等级。</summary>
    public int TargetLevel { get; }

    /// <summary>资源或状态是否已接近值得调度进阶工作的软门槛。</summary>
    public bool Approaching { get; }

    /// <summary>即时条件、准备阶段和挑战阶段合并后的首个未满足结果。</summary>
    public ProgressionGateResult Gate { get; }

    /// <summary>创建一份没有候选过渡的查询结果。</summary>
    public static ProgressionQuery None(int currentLevel = -1) =>
        new(false, null, ProgressionKind.Major, currentLevel, currentLevel, false,
            ProgressionGateResult.Blocked("progression.not_available"));
}

/// <summary>
///     进阶命令的执行结果。
/// </summary>
public readonly struct ProgressionResult
{
    /// <summary>创建一项进阶命令的执行结果。</summary>
    public ProgressionResult(ProgressionResultCode code, string transitionId, int fromLevel, int toLevel,
                             string reason = null, string workOrderId = null)
    {
        Code = code;
        TransitionId = transitionId;
        FromLevel = fromLevel;
        ToLevel = toLevel;
        Reason = reason;
        WorkOrderId = workOrderId;
    }

    /// <summary>调用完成后的可观察结果分类。</summary>
    public ProgressionResultCode Code { get; }

    /// <summary>实际处理的过渡标识；没有选中过渡或直接同步时可为 null。</summary>
    public string TransitionId { get; }

    /// <summary>命令开始时角色的主等级；角色没有体系时可为 -1。</summary>
    public int FromLevel { get; }

    /// <summary>命令返回时角色的主等级；小境界成功时通常等于 FromLevel。</summary>
    public int ToLevel { get; }

    /// <summary>拒绝、等待、失败或无进展的稳定原因键；没有补充原因时为 null。</summary>
    public string Reason { get; }

    /// <summary>本次结果关联的外部工作标识；非准备/挑战结果通常为 null。</summary>
    public string WorkOrderId { get; }

    /// <summary>命令是否已经改变角色的进阶状态或完成体系状态复制。</summary>
    public bool Changed => Code is ProgressionResultCode.MinorAdvanced
        or ProgressionResultCode.MajorAdvanced
        or ProgressionResultCode.Synchronized
        or ProgressionResultCode.Transferred;
}

/// <summary>无副作用地判断角色是否接近某条过渡的调度门槛。</summary>
public delegate bool ProgressionPredicate<T>(ActorExtend actor, CultisysAsset<T> cultisys, ref T component)
    where T : struct, ICultisysComponent;

/// <summary>无副作用地检查一项即时突破条件。</summary>
public delegate ProgressionGateResult ProgressionRequirement<T>(ActorExtend actor, CultisysAsset<T> cultisys,
                                                                 ref T component)
    where T : struct, ICultisysComponent;

/// <summary>执行一次成功率或品质判定并返回数据；不得直接修改角色或体系组件。</summary>
public delegate ProgressionResolution ProgressionResolver<T>(ActorExtend actor, CultisysAsset<T> cultisys,
                                                               ref T component)
    where T : struct, ICultisysComponent;

/// <summary>执行一个有副作用的进阶结算步骤；payload 来自当前过渡的 Resolver。</summary>
public delegate void ProgressionEffect<T>(ActorExtend actor, CultisysAsset<T> cultisys, ref T component,
                                           object payload)
    where T : struct, ICultisysComponent;

/// <summary>从当前主境界声明的过渡中选择一个候选；查询选择器必须无副作用。</summary>
public delegate ProgressionTransitionAsset<T> ProgressionTransitionSelector<T>(ActorExtend actor,
                                                                                CultisysAsset<T> cultisys,
                                                                                ref T component)
    where T : struct, ICultisysComponent;

/// <summary>复制不属于主体系组件的专属状态，例如金丹、元婴或其他附加 ECS 组件。</summary>
public delegate void ProgressionTransfer<T>(ActorExtend source, ActorExtend target, ref T sourceComponent,
                                             ref T targetComponent)
    where T : struct, ICultisysComponent;

/// <summary>
///     一条进阶过渡的完整定义。条件、消耗、结构变换、奖励和失败结算拥有独立维护入口。
/// </summary>
public sealed class ProgressionTransitionAsset<T> where T : struct, ICultisysComponent
{
    /// <summary>创建一条从指定主等级出发的进阶过渡定义。</summary>
    public ProgressionTransitionAsset(string id, ProgressionKind kind, int fromLevel, int targetLevel)
    {
        Id = id;
        Kind = kind;
        FromLevel = fromLevel;
        TargetLevel = targetLevel;
    }

    /// <summary>过渡的稳定唯一标识，用于日志、UI、存档外部工作和生命周期事件。</summary>
    public string Id { get; }

    /// <summary>过渡属于小境界进展还是大境界切换。</summary>
    public ProgressionKind Kind { get; }

    /// <summary>允许执行本过渡的主等级。</summary>
    public int FromLevel { get; }

    /// <summary>过渡成功后的主等级；小境界过渡通常等于 FromLevel。</summary>
    public int TargetLevel { get; }

    /// <summary>资源接近突破门槛的软判定，只用于查询和调度，不能执行随机或修改状态。</summary>
    public ProgressionPredicate<T> IsApproaching { get; set; }

    /// <summary>立即条件列表。每项都必须无副作用；自然突破按声明顺序检查。</summary>
    public List<ProgressionRequirement<T>> Requirements { get; } = new();

    /// <summary>可选的长期准备阶段，例如创建组织并积累资源。</summary>
    public IProgressionStage Preparation { get; set; }

    /// <summary>可选的最终挑战阶段，例如天劫或世界席位争夺。</summary>
    public IProgressionStage Challenge { get; set; }

    /// <summary>自然尝试的随机结算器，不应直接修改角色。</summary>
    public ProgressionResolver<T> ResolveNatural { get; set; }

    /// <summary>授予和同步使用的结算器，不检查自然条件或成功率，仍可生成随机品质。</summary>
    public ProgressionResolver<T> ResolveGrant { get; set; }

    /// <summary>仅自然尝试执行，并且发生在随机结算之前的消耗。</summary>
    public List<ProgressionEffect<T>> AttemptCosts { get; } = new();

    /// <summary>仅自然尝试成功后执行的消耗。</summary>
    public List<ProgressionEffect<T>> SuccessCosts { get; } = new();

    /// <summary>自然、授予和同步都会执行的必要结构变换。</summary>
    public List<ProgressionEffect<T>> Transformations { get; } = new();

    /// <summary>自然与授予成功后执行；同步不会重放奖励。</summary>
    public List<ProgressionEffect<T>> Rewards { get; } = new();

    /// <summary>仅自然尝试失败时执行的结算。</summary>
    public List<ProgressionEffect<T>> FailureEffects { get; } = new();

    /// <summary>仅自然尝试未产生进阶时执行的结算。</summary>
    public List<ProgressionEffect<T>> NoProgressEffects { get; } = new();
}

/// <summary>
///     一个主等级下可发生的所有小境界与大境界过渡。
/// </summary>
public sealed class RealmProgressionAsset<T> where T : struct, ICultisysComponent
{
    /// <summary>创建指定主等级的境界节点。</summary>
    public RealmProgressionAsset(int level)
    {
        Level = level;
    }

    /// <summary>本定义对应的修炼体系主等级。</summary>
    public int Level { get; }

    /// <summary>该主等级下可发生的全部小境界和大境界过渡。</summary>
    public List<ProgressionTransitionAsset<T>> Transitions { get; } = new();

    /// <summary>同步或传承落到本境界后执行的幂等结构修复。</summary>
    public List<ProgressionEffect<T>> SynchronizationEffects { get; } = new();

    /// <summary>无副作用地选择查询时展示和调度的候选过渡。</summary>
    public ProgressionTransitionSelector<T> SelectForQuery { get; set; }

    /// <summary>条件满足后选择本次自然尝试，允许在此时执行随机决策。</summary>
    public ProgressionTransitionSelector<T> SelectForNaturalAttempt { get; set; }

    /// <summary>
    ///     直接授予大境界时选择下一项应提交的过渡。可依次返回必要的小境界过渡，完成后必须返回大境界过渡。
    /// </summary>
    public ProgressionTransitionSelector<T> SelectForMajorGrant { get; set; }

    /// <summary>取得本境界声明的大境界过渡；不存在时返回 null。</summary>
    public ProgressionTransitionAsset<T> GetMajorTransition()
    {
        return Transitions.Find(transition => transition.Kind == ProgressionKind.Major);
    }

    /// <summary>取得本境界声明的小境界过渡；不存在时返回 null。</summary>
    public ProgressionTransitionAsset<T> GetMinorTransition()
    {
        return Transitions.Find(transition => transition.Kind == ProgressionKind.Minor);
    }
}

/// <summary>
///     一个修炼体系的进阶图。主等级只负责索引境界，具体过渡全部声明在对应 Realm 中。
/// </summary>
public sealed class CultisysProgressionProfile<T> where T : struct, ICultisysComponent
{
    /// <summary>按主等级索引境界定义；同一等级后注册的定义会替换旧值。</summary>
    private readonly Dictionary<int, RealmProgressionAsset<T>> _realms = new();

    /// <summary>传承时复制体系组件之外专属状态的可选回调。</summary>
    public ProgressionTransfer<T> TransferExtraState { get; set; }

    /// <summary>注册或替换一个主境界定义。</summary>
    public CultisysProgressionProfile<T> AddRealm(RealmProgressionAsset<T> realm)
    {
        _realms[realm.Level] = realm;
        return this;
    }

    /// <summary>按主等级取得境界定义；未声明时返回 null。</summary>
    public RealmProgressionAsset<T> GetRealm(int level)
    {
        _realms.TryGetValue(level, out var realm);
        return realm;
    }
}

/// <summary>
///     主等级提交完成后发出的只读事件，供表现层和其他观察者响应。
/// </summary>
public readonly struct ProgressionCommittedEvent
{
    /// <summary>创建一条只读的进阶提交事件。</summary>
    public ProgressionCommittedEvent(ActorExtend actor, BaseCultisysAsset cultisys, string transitionId,
                                     ProgressionKind kind, ProgressionMode mode, int fromLevel, int toLevel)
    {
        Actor = actor;
        Cultisys = cultisys;
        TransitionId = transitionId;
        Kind = kind;
        Mode = mode;
        FromLevel = fromLevel;
        ToLevel = toLevel;
    }

    /// <summary>完成进阶提交的角色。</summary>
    public ActorExtend Actor { get; }

    /// <summary>产生本次提交的修炼体系资产。</summary>
    public BaseCultisysAsset Cultisys { get; }

    /// <summary>已经提交的过渡标识。</summary>
    public string TransitionId { get; }

    /// <summary>本次提交属于小境界还是大境界。</summary>
    public ProgressionKind Kind { get; }

    /// <summary>本次提交来自自然尝试还是直接授予。</summary>
    public ProgressionMode Mode { get; }

    /// <summary>提交前的主等级。</summary>
    public int FromLevel { get; }

    /// <summary>提交后的主等级。</summary>
    public int ToLevel { get; }
}

/// <summary>
///     支持多个观察者的进阶生命周期入口，避免把体系专属表现写进通用结算层。
/// </summary>
public static class ProgressionLifecycle
{
    /// <summary>按注册顺序保存提交观察者；重复委托不会再次加入。</summary>
    private static readonly List<Action<ProgressionCommittedEvent>> _committedHandlers = new();

    /// <summary>注册一个主等级提交后的观察者；同一委托不会重复注册。</summary>
    public static void RegisterCommitted(Action<ProgressionCommittedEvent> handler)
    {
        if (handler == null || _committedHandlers.Contains(handler)) return;
        _committedHandlers.Add(handler);
    }

    /// <summary>按注册顺序通知全部观察者；仅由进阶服务在有效提交后调用。</summary>
    internal static void PublishCommitted(ProgressionCommittedEvent evt)
    {
        for (var i = 0; i < _committedHandlers.Count; i++)
        {
            _committedHandlers[i](evt);
        }
    }
}
