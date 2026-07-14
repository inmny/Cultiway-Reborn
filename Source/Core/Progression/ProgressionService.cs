using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Core.Progression;

/// <summary>
///     所有修炼体系等级写入的唯一入口，并负责统一的查询、授予、同步和传承语义。
/// </summary>
public static class ProgressionService
{
    /// <summary>
    ///     已注册修炼体系的有序列表。顺序同时决定通用任务发现多个可进阶体系时的处理优先级。
    /// </summary>
    private static readonly List<BaseCultisysAsset> _cultisyses = new();

    /// <summary>当前已注册并可供通用任务或世界工具访问的修炼体系。</summary>
    public static IReadOnlyList<BaseCultisysAsset> RegisteredCultisyses => _cultisyses;

    /// <summary>
    ///     注册可参与通用进阶任务调度的修炼体系。
    /// </summary>
    public static void Register(BaseCultisysAsset cultisys)
    {
        if (cultisys == null) return;
        for (var i = 0; i < _cultisyses.Count; i++)
        {
            if (_cultisyses[i].id != cultisys.id) continue;
            _cultisyses[i] = cultisys;
            return;
        }
        _cultisyses.Add(cultisys);
    }

    /// <summary>按稳定资产标识取得已注册修炼体系；未注册时返回 null。</summary>
    public static BaseCultisysAsset GetRegistered(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        for (var i = 0; i < _cultisyses.Count; i++)
        {
            if (_cultisyses[i].id == id) return _cultisyses[i];
        }
        return null;
    }

    /// <summary>
    ///     查询角色是否有一条现在值得调度的进阶过渡。
    /// </summary>
    public static bool CanScheduleAny(ActorExtend actor)
    {
        for (var i = 0; i < _cultisyses.Count; i++)
        {
            if (_cultisyses[i].CanScheduleProgression(actor)) return true;
        }
        return false;
    }

    /// <summary>
    ///     按修炼体系注册顺序执行第一条可调度的自然进阶。
    /// </summary>
    public static ProgressionResult TryAdvanceFirst(ActorExtend actor)
    {
        for (var i = 0; i < _cultisyses.Count; i++)
        {
            var cultisys = _cultisyses[i];
            if (!cultisys.CanScheduleProgression(actor)) continue;
            return cultisys.TryAdvanceNaturally(actor);
        }
        return new ProgressionResult(ProgressionResultCode.NotAvailable, null, -1, -1);
    }

    /// <summary>
    ///     无副作用地查询指定体系当前的候选过渡、接近度和首个未满足关卡。
    /// </summary>
    internal static ProgressionQuery Query<T>(CultisysAsset<T> cultisys, ActorExtend actor)
        where T : struct, ICultisysComponent
    {
        if (!actor.HasCultisys<T>()) return ProgressionQuery.None();

        ref var component = ref actor.GetCultisys<T>();
        var realm = cultisys.Progression.GetRealm(component.CurrLevel);
        if (realm == null) return ProgressionQuery.None(component.CurrLevel);

        var transition = SelectForQuery(realm, actor, cultisys, ref component);
        if (transition == null) return ProgressionQuery.None(component.CurrLevel);

        var approaching = transition.IsApproaching?.Invoke(actor, cultisys, ref component) ?? true;
        var gate = approaching
            ? EvaluateNaturalGates(transition, actor, cultisys, ref component, false, out _)
            : ProgressionGateResult.NotReady("progression.not_approaching");
        return new ProgressionQuery(true, transition.Id, transition.Kind, component.CurrLevel,
            transition.TargetLevel, approaching, gate);
    }

    /// <summary>
    ///     判断指定体系是否值得进入通用进阶任务。只接受已经满足关卡或可立即启动长期阶段的过渡。
    /// </summary>
    internal static bool CanSchedule<T>(CultisysAsset<T> cultisys, ActorExtend actor)
        where T : struct, ICultisysComponent
    {
        var query = Query(cultisys, actor);
        if (!query.Available || !query.Approaching) return false;
        return query.Gate.State is ProgressionGateState.Satisfied or ProgressionGateState.NeedsStart;
    }

    /// <summary>
    ///     按自然规则执行指定体系当前过渡，包括查询选择、阶段启动、随机结算和全部自然消耗。
    /// </summary>
    internal static ProgressionResult TryAdvanceNaturally<T>(CultisysAsset<T> cultisys, ActorExtend actor)
        where T : struct, ICultisysComponent
    {
        if (!actor.HasCultisys<T>())
            return new ProgressionResult(ProgressionResultCode.NotAvailable, null, -1, -1);

        ref var component = ref actor.GetCultisys<T>();
        var fromLevel = component.CurrLevel;
        var realm = cultisys.Progression.GetRealm(fromLevel);
        if (realm == null)
            return new ProgressionResult(ProgressionResultCode.NotAvailable, null, fromLevel, fromLevel);

        var preview = SelectForQuery(realm, actor, cultisys, ref component);
        if (preview == null)
            return new ProgressionResult(ProgressionResultCode.NotAvailable, null, fromLevel, fromLevel);
        if (!(preview.IsApproaching?.Invoke(actor, cultisys, ref component) ?? true))
            return new ProgressionResult(ProgressionResultCode.NotReady, preview.Id, fromLevel, fromLevel,
                "progression.not_approaching");

        var previewGate = EvaluateNaturalGates(preview, actor, cultisys, ref component, false,
            out var previewChallengeGate);
        if (previewGate.State is ProgressionGateState.NotReady or ProgressionGateState.Blocked
            or ProgressionGateState.InProgress)
        {
            return MapGateResult(preview, fromLevel, previewGate, previewChallengeGate);
        }

        var transition = realm.SelectForNaturalAttempt?.Invoke(actor, cultisys, ref component) ?? preview;
        if (transition == null)
            return new ProgressionResult(ProgressionResultCode.NotAvailable, null, fromLevel, fromLevel);

        var gate = EvaluateNaturalGates(transition, actor, cultisys, ref component, true,
            out var challengeGate);
        if (gate.State != ProgressionGateState.Satisfied)
        {
            return MapGateResult(transition, fromLevel, gate, challengeGate);
        }

        return Execute(cultisys, actor, transition, ProgressionMode.Natural);
    }

    /// <summary>跳过自然条件和消耗，授予指定体系当前主等级的下一项小境界进展。</summary>
    internal static ProgressionResult GrantNextMinor<T>(CultisysAsset<T> cultisys, ActorExtend actor)
        where T : struct, ICultisysComponent
    {
        return ExecuteExplicit(cultisys, actor, ProgressionKind.Minor, ProgressionMode.Grant);
    }

    /// <summary>跳过自然条件和消耗，逐项结算当前境界声明的必要小境界后，再授予大境界过渡。</summary>
    internal static ProgressionResult GrantNextRealm<T>(CultisysAsset<T> cultisys, ActorExtend actor)
        where T : struct, ICultisysComponent
    {
        return ExecuteExplicit(cultisys, actor, ProgressionKind.Major, ProgressionMode.Grant);
    }

    /// <summary>逐级结算必要小境界与大境界过渡，授予到目标主等级并正常发放每一步奖励。</summary>
    internal static ProgressionResult GrantToRealm<T>(CultisysAsset<T> cultisys, ActorExtend actor, int targetLevel)
        where T : struct, ICultisysComponent
    {
        return AdvanceToRealm(cultisys, actor, targetLevel, ProgressionMode.Grant);
    }

    /// <summary>沿当前进阶图查询从指定等级开始能够完整授予到的最高大境界。</summary>
    internal static int GetHighestGrantableRealm<T>(CultisysAsset<T> cultisys, int startLevel)
        where T : struct, ICultisysComponent
    {
        if (startLevel < 0 || startLevel >= cultisys.LevelNumber) return -1;
        return TraverseGrantPath(cultisys, startLevel, cultisys.LevelNumber - 1);
    }

    /// <summary>把角色校准到目标主等级，只执行必要结构变换和幂等修复。</summary>
    internal static ProgressionResult SynchronizeToRealm<T>(CultisysAsset<T> cultisys, ActorExtend actor,
                                                              int targetLevel)
        where T : struct, ICultisysComponent
    {
        return AdvanceToRealm(cultisys, actor, targetLevel, ProgressionMode.Synchronize);
    }

    /// <summary>
    ///     把来源角色的体系组件及专属附加状态复制给目标角色，并按复制后的主等级修复结构。
    /// </summary>
    internal static ProgressionResult Transfer<T>(CultisysAsset<T> cultisys, ActorExtend source,
                                                   ActorExtend target)
        where T : struct, ICultisysComponent
    {
        if (!source.HasCultisys<T>())
            return new ProgressionResult(ProgressionResultCode.NotAvailable, null, -1, -1);

        if (!target.HasCultisys<T>()) target.NewCultisys(cultisys);
        ref var sourceComponent = ref source.GetCultisys<T>();
        ref var targetComponent = ref target.GetCultisys<T>();
        targetComponent = sourceComponent;
        cultisys.Progression.TransferExtraState?.Invoke(source, target, ref sourceComponent, ref targetComponent);
        NormalizeCurrentRealm(cultisys, target);
        var transferredLevel = target.GetCultisys<T>().CurrLevel;
        return new ProgressionResult(ProgressionResultCode.Transferred, null, transferredLevel,
            transferredLevel);
    }

    /// <summary>
    ///     实现逐级授予和状态同步。授予会先完成每一级声明的必要小境界；同步允许降低等级，
    ///     并可在缺少中间过渡时直接写入目标等级后修复。
    /// </summary>
    private static ProgressionResult AdvanceToRealm<T>(CultisysAsset<T> cultisys, ActorExtend actor,
                                                        int targetLevel, ProgressionMode mode)
        where T : struct, ICultisysComponent
    {
        var ownsCultisys = actor.HasCultisys<T>();
        var defaultComponent = cultisys.DefaultComponent;
        var initialLevel = ownsCultisys ? actor.GetCultisys<T>().CurrLevel : defaultComponent.CurrLevel;
        if (targetLevel < 0 || targetLevel >= cultisys.LevelNumber)
            return new ProgressionResult(ProgressionResultCode.Blocked, null, initialLevel, initialLevel,
                "progression.invalid_target_level");
        if (targetLevel < initialLevel)
        {
            if (mode != ProgressionMode.Synchronize)
                return new ProgressionResult(ProgressionResultCode.Blocked, null, initialLevel, initialLevel,
                    "progression.cannot_grant_lower_realm");
            if (!ownsCultisys) actor.NewCultisys(cultisys);
            actor.GetCultisys<T>().CurrLevel = targetLevel;
            NormalizeCurrentRealm(cultisys, actor);
            return new ProgressionResult(ProgressionResultCode.Synchronized, null, initialLevel, targetLevel);
        }
        if (mode == ProgressionMode.Grant
            && TraverseGrantPath(cultisys, initialLevel, targetLevel) != targetLevel)
        {
            return new ProgressionResult(ProgressionResultCode.Blocked, null, initialLevel, initialLevel,
                "progression.missing_major_transition");
        }
        if (!ownsCultisys) actor.NewCultisys(cultisys);
        if (targetLevel == initialLevel)
        {
            if (mode == ProgressionMode.Synchronize)
            {
                NormalizeCurrentRealm(cultisys, actor);
                return new ProgressionResult(ProgressionResultCode.Synchronized, null, initialLevel,
                    initialLevel);
            }
            return new ProgressionResult(ProgressionResultCode.NoProgress, null, initialLevel, initialLevel);
        }

        ProgressionResult last = default;
        var usedDirectSynchronization = false;
        while (actor.GetCultisys<T>().CurrLevel < targetLevel)
        {
            var currentLevel = actor.GetCultisys<T>().CurrLevel;
            var realm = cultisys.Progression.GetRealm(currentLevel);
            var transition = realm?.GetMajorTransition();
            if (transition == null)
            {
                if (mode == ProgressionMode.Synchronize)
                {
                    actor.GetCultisys<T>().CurrLevel = targetLevel;
                    usedDirectSynchronization = true;
                    break;
                }
                return new ProgressionResult(ProgressionResultCode.Blocked, null, initialLevel,
                    currentLevel, "progression.missing_major_transition");
            }
            last = mode == ProgressionMode.Grant
                ? ExecuteGrantedMajor(cultisys, actor, realm, transition)
                : Execute(cultisys, actor, transition, mode);
            if (last.Code is not ProgressionResultCode.MajorAdvanced
                and not ProgressionResultCode.Synchronized)
            {
                return last;
            }
        }

        if (mode == ProgressionMode.Synchronize) NormalizeCurrentRealm(cultisys, actor);
        return new ProgressionResult(mode == ProgressionMode.Synchronize
                ? ProgressionResultCode.Synchronized
                : ProgressionResultCode.MajorAdvanced,
            usedDirectSynchronization ? null : last.TransitionId, initialLevel,
            actor.GetCultisys<T>().CurrLevel);
    }

    /// <summary>
    ///     无副作用地沿大境界过渡前进，遇到断链、越级、无授予结算器或未声明目标境界时停止。
    /// </summary>
    private static int TraverseGrantPath<T>(CultisysAsset<T> cultisys, int startLevel, int targetLimit)
        where T : struct, ICultisysComponent
    {
        var currentLevel = startLevel;
        while (currentLevel < targetLimit)
        {
            var transition = cultisys.Progression.GetRealm(currentLevel)?.GetMajorTransition();
            if (transition == null
                || transition.FromLevel != currentLevel
                || transition.TargetLevel <= currentLevel
                || transition.TargetLevel > targetLimit
                || transition.TargetLevel >= cultisys.LevelNumber
                || transition.ResolveGrant == null
                || cultisys.Progression.GetRealm(transition.TargetLevel) == null)
            {
                break;
            }
            currentLevel = transition.TargetLevel;
        }
        return currentLevel;
    }

    /// <summary>从角色当前境界中按类型取得一条明确过渡，并使用指定非自然模式执行。</summary>
    private static ProgressionResult ExecuteExplicit<T>(CultisysAsset<T> cultisys, ActorExtend actor,
                                                         ProgressionKind kind, ProgressionMode mode)
        where T : struct, ICultisysComponent
    {
        if (!actor.HasCultisys<T>()) actor.NewCultisys(cultisys);
        var currentLevel = actor.GetCultisys<T>().CurrLevel;
        var realm = cultisys.Progression.GetRealm(currentLevel);
        var transition = kind == ProgressionKind.Major
            ? realm?.GetMajorTransition()
            : realm?.GetMinorTransition();
        if (transition == null)
            return new ProgressionResult(ProgressionResultCode.NotAvailable, null, currentLevel, currentLevel);
        if (kind == ProgressionKind.Major && mode == ProgressionMode.Grant)
            return ExecuteGrantedMajor(cultisys, actor, realm, transition);
        return Execute(cultisys, actor, transition, mode);
    }

    /// <summary>
    ///     按境界的授予选择器逐项提交必要的小境界；选择器返回大境界后再完成主等级切换。
    /// </summary>
    private static ProgressionResult ExecuteGrantedMajor<T>(CultisysAsset<T> cultisys, ActorExtend actor,
        RealmProgressionAsset<T> realm, ProgressionTransitionAsset<T> majorTransition)
        where T : struct, ICultisysComponent
    {
        if (realm.SelectForMajorGrant == null)
            return Execute(cultisys, actor, majorTransition, ProgressionMode.Grant);

        while (true)
        {
            ProgressionTransitionAsset<T> transition;
            {
                ref var component = ref actor.GetCultisys<T>();
                transition = realm.SelectForMajorGrant(actor, cultisys, ref component);
            }
            if (transition == null)
            {
                var level = actor.GetCultisys<T>().CurrLevel;
                return new ProgressionResult(ProgressionResultCode.Blocked, null, level, level,
                    "progression.missing_major_grant_transition");
            }
            if (transition.Kind == ProgressionKind.Major)
                return Execute(cultisys, actor, transition, ProgressionMode.Grant);

            var minorResult = Execute(cultisys, actor, transition, ProgressionMode.Grant);
            if (!minorResult.Changed) return minorResult;
        }
    }

    /// <summary>
    ///     执行一条已经选定的过渡。该方法统一规定各效果阶段的顺序，并且是大境界 CurrLevel 的提交入口。
    /// </summary>
    private static ProgressionResult Execute<T>(CultisysAsset<T> cultisys, ActorExtend actor,
                                                 ProgressionTransitionAsset<T> transition,
                                                 ProgressionMode mode)
        where T : struct, ICultisysComponent
    {
        var fromLevel = actor.GetCultisys<T>().CurrLevel;
        var resolver = mode == ProgressionMode.Natural ? transition.ResolveNatural : transition.ResolveGrant;
        if (resolver == null)
            return new ProgressionResult(ProgressionResultCode.Blocked, transition.Id, fromLevel, fromLevel,
                "progression.missing_resolver");

        if (mode == ProgressionMode.Natural)
        {
            Apply(transition.AttemptCosts, actor, cultisys, null);
        }

        ProgressionResolution resolution;
        {
            ref var resolvingComponent = ref actor.GetCultisys<T>();
            resolution = resolver(actor, cultisys, ref resolvingComponent);
        }
        switch (resolution.State)
        {
            case ProgressionResolutionState.Failure:
                if (mode == ProgressionMode.Natural)
                    Apply(transition.FailureEffects, actor, cultisys, resolution.Payload);
                actor.MarkCultiwayStatsDirty();
                return new ProgressionResult(ProgressionResultCode.Failed, transition.Id, fromLevel,
                    actor.GetCultisys<T>().CurrLevel, resolution.Reason);
            case ProgressionResolutionState.NoProgress:
                if (mode == ProgressionMode.Natural)
                    Apply(transition.NoProgressEffects, actor, cultisys, resolution.Payload);
                actor.MarkCultiwayStatsDirty();
                return new ProgressionResult(ProgressionResultCode.NoProgress, transition.Id, fromLevel,
                    actor.GetCultisys<T>().CurrLevel, resolution.Reason);
        }

        if (mode == ProgressionMode.Natural)
            Apply(transition.SuccessCosts, actor, cultisys, resolution.Payload);
        Apply(transition.Transformations, actor, cultisys, resolution.Payload);

        if (transition.Kind == ProgressionKind.Major)
        {
            actor.GetCultisys<T>().CurrLevel = transition.TargetLevel;
        }

        if (mode != ProgressionMode.Synchronize)
            Apply(transition.Rewards, actor, cultisys, resolution.Payload);

        actor.MarkCultiwayStatsDirty();
        var toLevel = actor.GetCultisys<T>().CurrLevel;
        if (transition.Kind == ProgressionKind.Minor)
        {
            return new ProgressionResult(ProgressionResultCode.MinorAdvanced, transition.Id, fromLevel,
                toLevel);
        }

        actor.UpgradePowerLevel(cultisys.PowerLevels[toLevel]);
        if (mode != ProgressionMode.Synchronize)
        {
            ref var committedComponent = ref actor.GetCultisys<T>();
            ModClass.I.WorldRecord.CheckAndLogFirstLevelup(cultisys.id, actor, ref committedComponent);
            ProgressionLifecycle.PublishCommitted(new ProgressionCommittedEvent(actor, cultisys, transition.Id,
                transition.Kind, mode, fromLevel, toLevel));
        }

        return new ProgressionResult(mode == ProgressionMode.Synchronize
                ? ProgressionResultCode.Synchronized
                : ProgressionResultCode.MajorAdvanced,
            transition.Id, fromLevel, toLevel);
    }

    /// <summary>选择无副作用查询使用的候选过渡；未提供选择器时采用声明顺序中的第一条。</summary>
    private static ProgressionTransitionAsset<T> SelectForQuery<T>(RealmProgressionAsset<T> realm,
                                                                    ActorExtend actor,
                                                                    CultisysAsset<T> cultisys,
                                                                    ref T component)
        where T : struct, ICultisysComponent
    {
        return realm.SelectForQuery?.Invoke(actor, cultisys, ref component)
               ?? (realm.Transitions.Count > 0 ? realm.Transitions[0] : null);
    }

    /// <summary>
    ///     依次检查即时条件、准备阶段和挑战阶段；startStages 决定是否允许启动 NeedsStart 阶段。
    /// </summary>
    private static ProgressionGateResult EvaluateNaturalGates<T>(ProgressionTransitionAsset<T> transition,
                                                                  ActorExtend actor,
                                                                  CultisysAsset<T> cultisys,
                                                                  ref T component,
                                                                  bool startStages,
                                                                  out bool challengeGate)
        where T : struct, ICultisysComponent
    {
        challengeGate = false;
        for (var i = 0; i < transition.Requirements.Count; i++)
        {
            var result = transition.Requirements[i](actor, cultisys, ref component);
            if (result.State != ProgressionGateState.Satisfied) return result;
        }

        var context = CreateStageContext(actor, cultisys, transition, ProgressionMode.Natural,
            component.CurrLevel);
        var preparation = EvaluateStage(transition.Preparation, context, startStages);
        if (preparation.State != ProgressionGateState.Satisfied) return preparation;
        challengeGate = transition.Challenge != null;
        return EvaluateStage(transition.Challenge, context, startStages);
    }

    /// <summary>读取一个可选长期阶段，并在允许启动且返回 NeedsStart 时调用其 Start。</summary>
    private static ProgressionGateResult EvaluateStage(IProgressionStage stage, ProgressionStageContext context,
                                                        bool start)
    {
        if (stage == null) return ProgressionGateResult.Satisfied;
        var result = stage.Evaluate(context);
        if (start && result.State == ProgressionGateState.NeedsStart) stage.Start(context);
        return result;
    }

    /// <summary>为不依赖具体体系组件类型的准备或挑战阶段创建稳定上下文。</summary>
    private static ProgressionStageContext CreateStageContext<T>(ActorExtend actor, CultisysAsset<T> cultisys,
                                                                  ProgressionTransitionAsset<T> transition,
                                                                  ProgressionMode mode, int fromLevel)
        where T : struct, ICultisysComponent
    {
        return new ProgressionStageContext(actor, cultisys, transition.Id, transition.Kind, mode, fromLevel,
            transition.TargetLevel);
    }

    /// <summary>把关卡状态映射为调用方可观察的结果码，并保留原因及外部工作标识。</summary>
    private static ProgressionResult MapGateResult<T>(ProgressionTransitionAsset<T> transition, int level,
                                                       ProgressionGateResult gate, bool challenge)
        where T : struct, ICultisysComponent
    {
        var code = gate.State switch
        {
            ProgressionGateState.NotReady => ProgressionResultCode.NotReady,
            ProgressionGateState.Blocked => ProgressionResultCode.Blocked,
            ProgressionGateState.NeedsStart => challenge
                ? ProgressionResultCode.ChallengeStarted
                : ProgressionResultCode.PreparationStarted,
            ProgressionGateState.InProgress => challenge
                ? ProgressionResultCode.ChallengeInProgress
                : ProgressionResultCode.PreparationInProgress,
            _ => ProgressionResultCode.Blocked
        };
        return new ProgressionResult(code, transition.Id, level, level, gate.Reason, gate.WorkOrderId);
    }

    /// <summary>
    ///     按声明顺序执行效果。每次调用前重新取得组件引用，以适配前一效果增删 ECS 组件造成的存储迁移。
    /// </summary>
    private static void Apply<T>(List<ProgressionEffect<T>> effects, ActorExtend actor,
                                 CultisysAsset<T> cultisys, object payload)
        where T : struct, ICultisysComponent
    {
        for (var i = 0; i < effects.Count; i++)
        {
            ref var component = ref actor.GetCultisys<T>();
            effects[i](actor, cultisys, ref component, payload);
        }
    }

    /// <summary>执行角色当前境界声明的幂等修复，并刷新战力等级和缓存属性。</summary>
    private static void NormalizeCurrentRealm<T>(CultisysAsset<T> cultisys, ActorExtend actor)
        where T : struct, ICultisysComponent
    {
        var level = actor.GetCultisys<T>().CurrLevel;
        var realm = cultisys.Progression.GetRealm(level);
        if (realm != null) Apply(realm.SynchronizationEffects, actor, cultisys, null);
        actor.UpgradePowerLevel(cultisys.PowerLevels[level]);
        actor.MarkCultiwayStatsDirty();
    }
}
