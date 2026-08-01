using System;
using System.Collections.Generic;
using ai;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Impacts;
using Cultiway.Core.SkillLibV3.Systems;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using HarmonyLib;
using strings;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 维护当前世界的战斗运行时，并衔接主线程采样、工作线程规划和主线程提交。
/// </summary>
public static class CombatWorldService
{
    private static readonly Dictionary<long, CombatActorRuntime> ActorStates = new();
    private static readonly Dictionary<long, CombatArmyRuntime> ArmyStates = new();
    private static bool initialized;
    private static double nextGlobalCleanupAt;

    /// <summary>注册世界清理回调。该方法由 Content 初始化阶段调用一次。</summary>
    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        PatchMapBox.RegisterActionOnClearWorld(ClearWorldState);
    }

    /// <summary>
    /// 验证所有会决定新旧战斗层归属的 Actor 前缀已经安装；任一缺失时整体关闭新系统。
    /// </summary>
    public static void ValidateCriticalPatches()
    {
        string[] methods =
        [
            nameof(Actor.b2_checkCurrentEnemyTarget),
            nameof(Actor.b3_findEnemyTarget),
            nameof(Actor.b6_0_updateDecision),
            nameof(Actor.tryToAttack),
            nameof(Actor.tryToUseAdvancedCombatAction)
        ];
        for (int i = 0; i < methods.Length; i++)
        {
            var original = AccessTools.Method(typeof(Actor), methods[i]);
            Patches patches = original == null ? null : Harmony.GetPatchInfo(original);
            bool installed = false;
            if (patches != null)
            {
                for (int prefixIndex = 0; prefixIndex < patches.Prefixes.Count; prefixIndex++)
                {
                    HarmonyLib.Patch prefix = patches.Prefixes[prefixIndex];
                    if (prefix.owner != "inmny.cultiway" ||
                        prefix.PatchMethod?.DeclaringType != typeof(PatchActor))
                        continue;
                    installed = true;
                    break;
                }
            }
            if (installed) continue;
            TacticalCombatSettings.DisableForCriticalFailure(
                $"缺少 Actor.{methods[i]} 的战术前缀");
            return;
        }
    }

    /// <summary>清除不参与存档的观察、士气、指令、计划和动作冷却。</summary>
    public static void ClearWorldState()
    {
        ActorStates.Clear();
        ArmyStates.Clear();
        nextGlobalCleanupAt = 0d;
        LogicSkillPersistentSystem.ClearCombatSnapshots();
        CombatDiagnostics.Reset();
    }

    /// <summary>
    /// 关闭接管时释放已经进入战术任务的角色；保留攻击目标供原版逻辑继续处理。
    /// </summary>
    internal static void ReleaseTakeovers()
    {
        if (World.world?.units != null)
        {
            using var actorIds = new ListPool<long>(ActorStates.Keys);
            for (int i = 0; i < actorIds.Count; i++)
            {
                Actor actor = World.world.units.get(actorIds[i]);
                if (!actor.isRekt() &&
                    actor.isTask(TacticalCombatSettings.TacticalTaskId))
                    actor.cancelAllBeh();
            }
        }
        ActorStates.Clear();
        ArmyStates.Clear();
        nextGlobalCleanupAt = 0d;
    }

    /// <summary>判断指定角色是否由新战斗层接管自主战斗。</summary>
    public static bool ShouldTakeOver(Actor actor)
    {
        return TacticalCombatSettings.Enabled &&
               !actor.isRekt() &&
               actor.current_tile != null &&
               !actor.under_forces &&
               !actor.isInMagnet() &&
               !ControllableUnit.isControllingUnit(actor);
    }

    /// <summary>返回角色当前是否处在由战术层维持的战斗任务中。</summary>
    public static bool IsEngaged(Actor actor)
    {
        return actor != null &&
               ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) &&
               runtime.IsEngaged;
    }

    /// <summary>
    /// 返回任务栏应显示的粗粒度战术活动及该活动的开始时间。
    /// 内部任务仍保持为战术交战，避免展示变化重置行为和路径。
    /// </summary>
    public static bool TryGetDisplayedActivity(
        Actor actor,
        out CombatActivityPresentation activity,
        out double startedAt)
    {
        activity = default;
        startedAt = 0d;
        if (actor == null ||
            !actor.isTask(TacticalCombatSettings.TacticalTaskId) ||
            !ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) ||
            !runtime.IsEngaged)
            return false;

        double now = CurrentTime;
        activity = ResolveDisplayedActivity(actor, runtime, now);
        if (activity.Movement == CombatActivityMovement.None &&
            activity.Action == CombatActivityAction.None)
            return false;
        SetDisplayedActivity(runtime, activity, now);
        startedAt = runtime.DisplayedActivityStartedAt;
        return true;
    }

    /// <summary>
    /// 角色离开战术接管条件时释放自主计划，但保留冷却、观察以及外部仍可能使用的攻击目标。
    /// </summary>
    public static void ReleaseActorTakeover(Actor actor)
    {
        if (actor == null) return;
        if (ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) &&
            (runtime.Plan != null || runtime.IsEngaged))
        {
            runtime.Plan = null;
            runtime.IsEngaged = false;
            runtime.CurrentTargetId = 0;
            runtime.ExternalTargetDirty = false;
            runtime.NextPlanAt = 0d;
            runtime.NextActionAttemptAt = 0d;
            runtime.TargetPathFailures = 0;
            runtime.LastProgressAt = CurrentTime;
            runtime.LastProgressPosition = actor.current_position;
            ResetDisplayedActivity(runtime);
            runtime.TouchRevision();
            CombatMovementService.Clear(
                actor,
                runtime,
                stopMovement: true,
                clearBearing: true);
        }
        if (actor.isTask(TacticalCombatSettings.TacticalTaskId))
            actor.cancelAllBeh();
    }

    /// <summary>
    /// 为普通模拟路径同步执行一次到期规划。未到规划时间时只维持现有计划。
    /// </summary>
    public static bool PlanSynchronously(Actor actor)
    {
        CombatPlanningWorkItem item = PreparePlanning(actor);
        if (item == null) return IsEngaged(actor);
        item.Plan();
        item.Commit();
        return IsEngaged(actor);
    }

    /// <summary>
    /// 在 b2 阶段推进既有计划，必要时执行动作或触发无进展恢复。
    /// 返回 true 表示本帧已经出手或原地等待，应阻止后续 AI 行为；移动中的计划返回 false。
    /// </summary>
    public static bool TickExecution(Actor actor, float elapsed)
    {
        if (!ShouldTakeOver(actor)) return false;
        CombatActorRuntime runtime = GetOrCreateActorState(actor);
        ImportExternalTarget(actor, runtime);
        if (CombatMovementService.Tick(actor, runtime, CurrentTime))
        {
            runtime.LastProgressAt = CurrentTime;
            return true;
        }
        if (runtime.Plan == null || !runtime.Plan.HasEnemy)
            return false;

        BaseSimObject enemy = runtime.Plan.PrimaryEnemy.Object;
        if (!IsValidEnemy(
                actor,
                enemy,
                runtime.Plan.PrimaryEnemy.ThreatSource != CombatThreatSource.None))
        {
            RequestMovementRefresh(runtime, clearBearing: true);
            return false;
        }

        if (actor.attack_timer > 0f)
        {
            runtime.LastProgressAt = CurrentTime;
            return false;
        }
        UpdateProgress(actor, enemy, runtime);
        if (runtime.Plan == null || runtime.Plan.Action == null) return false;
        double now = CurrentTime;
        if (now < runtime.NextActionAttemptAt) return false;

        BaseSimObject actionTarget = ResolveActionTarget(actor, runtime.Plan);
        if (actionTarget.isRekt())
        {
            RequestDecisionRefresh(runtime);
            return false;
        }
        if (!IsWithinActionRange(actor, actionTarget, runtime.Plan.Action.Profile)) return false;

        Vector3 targetPosition = actionTarget.GetSimPos();
        var context = new CombatActionExecutionContext(
            actor.GetExtend(),
            enemy,
            actionTarget,
            targetPosition);
        CombatActionCandidate executedAction = runtime.Plan.Action;
        CombatActionUse executedUse = runtime.Plan.ActionUse;
        CombatExecutionStatus result = CombatActionService.Execute(executedAction, context);
        if (result != CombatExecutionStatus.Started && runtime.Plan.BackupAction != null)
        {
            BaseSimObject backupTarget = ResolveActionTarget(
                actor,
                runtime.Plan,
                runtime.Plan.BackupAction);
            if (!backupTarget.isRekt() &&
                IsWithinActionRange(actor, backupTarget, runtime.Plan.BackupAction.Profile))
            {
                var backupContext = new CombatActionExecutionContext(
                    actor.GetExtend(),
                    enemy,
                    backupTarget,
                    backupTarget.GetSimPos());
                CombatExecutionStatus backupResult = CombatActionService.Execute(
                    runtime.Plan.BackupAction,
                    backupContext);
                if (backupResult == CombatExecutionStatus.Started)
                {
                    executedAction = runtime.Plan.BackupAction;
                    executedUse = runtime.Plan.BackupActionUse;
                }
                result = backupResult;
            }
        }

        bool blockMovementForAction = false;
        if (result == CombatExecutionStatus.Started)
        {
            runtime.NextActionAttemptAt = 0d;
            runtime.TargetPathFailures = 0;
            runtime.LastProgressAt = now;
            runtime.ActiveActionUse = executedUse;
            runtime.ActionPresentationUntil = now +
                                              TacticalCombatSettings.ActionPresentationDuration;
            switch (executedAction.Profile.MovementMode)
            {
                case CombatActionMovementMode.BriefStop:
                    CombatMovementService.PauseBriefly(actor, runtime, now);
                    blockMovementForAction = true;
                    break;
                case CombatActionMovementMode.StationaryDuringRecovery:
                    CombatMovementService.LockUntilRecovery(actor, runtime, now);
                    blockMovementForAction = true;
                    break;
            }
        }
        else
        {
            runtime.NextActionAttemptAt = now + ResolveActionRetryDelay(actor, runtime.Revision);
            RequestDecisionRefresh(runtime);
        }
        CombatDiagnostics.RecordExecution(result);
        return result == CombatExecutionStatus.Started && blockMovementForAction;
    }

    /// <summary>供平滑移动入口查询战术动作是否暂时冻结位移。</summary>
    internal static bool ShouldPauseMovement(Actor actor)
    {
        return actor?.data != null &&
               ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) &&
               runtime.IsEngaged &&
               CombatMovementService.ShouldPause(runtime, CurrentTime);
    }

    /// <summary>在格子边界落实战术层请求的平滑停步。</summary>
    internal static bool TryCompletePendingMovementStop(Actor actor)
    {
        return actor?.data != null &&
               ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) &&
               runtime.IsEngaged &&
               CombatMovementService.TryCompletePendingStopAtBoundary(actor, runtime);
    }

    /// <summary>
    /// 给协作模拟器准备一份主线程快照。返回 null 表示本轮无需重新规划。
    /// </summary>
    public static CombatPlanningWorkItem PreparePlanning(Actor actor)
    {
        if (!ShouldTakeOver(actor)) return null;
        if (actor._update_done ||
            actor._beh_skip ||
            !actor.isAllowedToLookForEnemies() ||
            actor.isInWaterAndCantAttack() ||
            actor._has_status_strange_urge)
            return null;

        CombatActorRuntime runtime = GetOrCreateActorState(actor);
        ImportExternalTarget(actor, runtime);
        double now = CurrentTime;
        if (!runtime.ExternalTargetDirty && now < runtime.NextPlanAt)
            return null;

        runtime.ExternalTargetDirty = false;
        runtime.TouchRevision();
        CombatPlanningSnapshot snapshot = BuildSnapshot(actor, runtime, now);
        return new CombatPlanningWorkItem(actor, snapshot);
    }

    /// <summary>
    /// 供原版或其他 Mod 的直接 tryToAttack 调用使用；不启动自主规划，但沿用统一执行副作用。
    /// </summary>
    public static bool TryExecuteImmediate(
        ActorExtend caster,
        BaseSimObject target,
        Action killAction,
        float bonusAreaEffect,
        bool doChecks)
    {
        Actor actor = caster?.Base;
        if (actor == null || actor.isRekt() || target.isRekt()) return false;
        if (doChecks &&
            (actor.isInWaterAndCantAttack() || !actor.isAttackPossible()))
            return false;

        using var candidates = new ListPool<CombatActionCandidate>();
        CombatActionService.Collect(caster, target, null, 1f, null, candidates);
        CombatActionCandidate best = null;
        CombatActionCandidate backup = null;
        float bestScore = float.MinValue;
        float backupScore = float.MinValue;
        for (int i = 0; i < candidates.Count; i++)
        {
            CombatActionCandidate candidate = candidates[i];
            if (!candidate.IsReady) continue;
            if (!candidate.Profile.HasPurpose(CombatActionPurpose.Offense) &&
                !candidate.Profile.HasPurpose(CombatActionPurpose.Control) &&
                !candidate.Profile.HasPurpose(CombatActionPurpose.Mobility))
                continue;
            BaseSimObject actionTarget = candidate.Profile.TargetMode ==
                                         SkillLibV3.ActiveAbilities.ActiveAbilityTargetMode.Self
                ? actor
                : target;
            if (!IsWithinActionRange(actor, actionTarget, candidate.Profile)) continue;
            float score = candidate.Profile.Power +
                          candidate.Profile.Control +
                          candidate.Profile.Utility +
                          candidate.Profile.BaseWeight * 0.1f;
            if (score > bestScore)
            {
                backup = best;
                backupScore = bestScore;
                best = candidate;
                bestScore = score;
            }
            else if (score > backupScore)
            {
                backup = candidate;
                backupScore = score;
            }
        }
        if (best == null) return false;

        BaseSimObject bestTarget = best.Profile.TargetMode ==
                                   SkillLibV3.ActiveAbilities.ActiveAbilityTargetMode.Self
            ? actor
            : target;
        var context = new CombatActionExecutionContext(
            caster,
            target,
            bestTarget,
            bestTarget.GetSimPos(),
            killAction,
            bonusAreaEffect);
        CombatExecutionStatus result = CombatActionService.Execute(best, context);
        if (result == CombatExecutionStatus.Started || backup == null)
            return result == CombatExecutionStatus.Started;

        BaseSimObject backupTarget = backup.Profile.TargetMode ==
                                     SkillLibV3.ActiveAbilities.ActiveAbilityTargetMode.Self
            ? actor
            : target;
        var backupContext = new CombatActionExecutionContext(
            caster,
            target,
            backupTarget,
            backupTarget.GetSimPos(),
            killAction,
            bonusAreaEffect);
        return CombatActionService.Execute(backup, backupContext) ==
               CombatExecutionStatus.Started;
    }

    /// <summary>记录一次攻击尝试，包括最终无效或零伤害的攻击。</summary>
    public static void RecordAttackAttempt(BaseSimObject attacker, Actor target)
    {
        if (!TacticalCombatSettings.Enabled ||
            attacker.isRekt() ||
            !attacker.isActor() ||
            target.isRekt())
            return;
        Actor source = attacker.a;
        double now = CurrentTime;
        CombatActorRuntime sourceRuntime = GetOrCreateActorState(source);
        CombatObservationService.RecordAttempt(sourceRuntime, source, target, now);
        PublishObservationToArmy(source, sourceRuntime.Observations[target.getID()], now);

        CombatActorRuntime targetRuntime = GetOrCreateActorState(target);
        CombatObservation incomingObservation = CombatObservationService.ObserveVisible(
            targetRuntime,
            target,
            source,
            now);
        RecordIncomingThreat(
            source,
            target,
            targetRuntime,
            incomingObservation,
            0.2f,
            now);
        targetRuntime.Morale = Mathf.Clamp01(targetRuntime.Morale - 0.005f);
    }

    /// <summary>
    /// 记录已经启动但尚未结算的敌对动作。该入口只更新威胁关系，不增加命中尝试统计。
    /// </summary>
    public static void RecordThreateningAction(Actor attacker, Actor target)
    {
        if (!TacticalCombatSettings.Enabled ||
            attacker.isRekt() ||
            target.isRekt() ||
            attacker == target)
            return;

        double now = CurrentTime;
        CombatActorRuntime attackerRuntime = GetOrCreateActorState(attacker);
        CombatObservation observation = CombatObservationService.ObserveVisible(
            attackerRuntime,
            attacker,
            target,
            now);
        PublishObservationToArmy(attacker, observation, now);

        CombatActorRuntime targetRuntime = GetOrCreateActorState(target);
        CombatObservation incomingObservation = CombatObservationService.ObserveVisible(
            targetRuntime,
            target,
            attacker,
            now);
        RecordIncomingThreat(
            attacker,
            target,
            targetRuntime,
            incomingObservation,
            0.2f,
            now);
    }

    /// <summary>记录攻击的最终伤害结算，用于更新认知、士气和重新选策。</summary>
    public static void RecordDamageOutcome(
        BaseSimObject attacker,
        Actor target,
        float damage,
        bool ineffective)
    {
        if (!TacticalCombatSettings.Enabled ||
            attacker.isRekt() ||
            !attacker.isActor() ||
            target == null)
            return;
        Actor source = attacker.a;
        double now = CurrentTime;
        CombatActorRuntime sourceRuntime = GetOrCreateActorState(source);
        CombatObservationService.RecordOutcome(
            sourceRuntime,
            source,
            target,
            Mathf.Max(0f, damage),
            ineffective,
            now);
        PublishObservationToArmy(source, sourceRuntime.Observations[target.getID()], now);

        CombatActorRuntime targetRuntime = GetOrCreateActorState(target);
        float maxHealth = Mathf.Max(1f, target.getMaxHealth());
        float pressure = Mathf.Clamp01(Mathf.Max(0f, damage) / maxHealth);
        CombatObservation incomingObservation = CombatObservationService.ObserveVisible(
            targetRuntime,
            target,
            source,
            now);
        RecordIncomingThreat(
            source,
            target,
            targetRuntime,
            incomingObservation,
            Mathf.Clamp01(0.2f + pressure),
            now);
        targetRuntime.Morale = Mathf.Clamp01(
            targetRuntime.Morale - pressure * 0.45f - (ineffective ? 0f : 0.01f));
        sourceRuntime.Morale = Mathf.Clamp01(
            sourceRuntime.Morale + (damage > 0f ? 0.015f : -0.005f));
    }

    /// <summary>通知战斗层某角色的寻路失败，并在重复失败后更换目标。</summary>
    public static bool ReportPathFailure(Actor actor)
    {
        if (!ShouldTakeOver(actor) ||
            !ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) ||
            !runtime.IsEngaged)
            return false;

        CombatDiagnostics.RecordPathFailure();
        runtime.TargetPathFailures++;
        if (runtime.TargetPathFailures >= 3 && runtime.Plan?.HasEnemy == true)
        {
            runtime.IgnoredTargetId = runtime.Plan.PrimaryEnemy.Id;
            runtime.IgnoreCurrentTargetUntil = CurrentTime + 3d;
            runtime.Plan = null;
            actor.clearAttackTarget();
            CombatMovementService.Clear(
                actor,
                runtime,
                stopMovement: true,
                clearBearing: true);
            runtime.TargetPathFailures = 0;
        }
        RequestMovementRefresh(runtime, clearBearing: false);
        return true;
    }

    /// <summary>移除角色运行时，避免死亡或销毁后的引用继续存活。</summary>
    public static void RemoveActor(Actor actor)
    {
        if (actor == null) return;
        ActorStates.Remove(actor.getID());
    }

    /// <summary>判断具体动作的独立冷却是否结束。</summary>
    public static bool IsActionReady(Actor actor, CombatActionKey key)
    {
        if (actor == null ||
            !ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) ||
            !runtime.Cooldowns.TryGetValue(key, out double readyAt))
            return true;
        return readyAt <= CurrentTime;
    }

    /// <summary>为具体动作写入独立冷却，不影响同资产上的其他能力实例。</summary>
    public static void StartActionCooldown(
        Actor actor,
        CombatActionKey key,
        float duration)
    {
        if (actor == null || duration <= 0f) return;
        CombatActorRuntime runtime = GetOrCreateActorState(actor);
        double readyAt = CurrentTime + duration;
        if (!runtime.Cooldowns.TryGetValue(key, out double current) || current < readyAt)
            runtime.Cooldowns[key] = readyAt;
    }

    /// <summary>设置军队临时战斗指令；持续时间小于等于零表示保持到外部再次修改。</summary>
    public static void SetDirective(
        Army army,
        CombatDirective directive,
        float durationSeconds = 0f)
    {
        if (army == null) return;
        CombatArmyRuntime runtime = GetOrCreateArmyState(army);
        runtime.Directive = directive;
        runtime.DirectiveExpiresAt = durationSeconds > 0f
            ? CurrentTime + durationSeconds
            : double.MaxValue;
        RequestArmyReplan(
            army,
            clearPlans: directive == CombatDirective.Retreat,
            stopMovement: directive == CombatDirective.Retreat);
    }

    internal static void Commit(Actor actor, CombatPlanningSnapshot snapshot, CombatPlan plan)
    {
        if (!ShouldTakeOver(actor) ||
            actor._update_done ||
            actor._beh_skip ||
            !ActorStates.TryGetValue(actor.getID(), out CombatActorRuntime runtime) ||
            runtime.Revision != snapshot.Revision)
        {
            CombatDiagnostics.RecordCommit(false);
            return;
        }
        if (Vector2.Distance(actor.current_position, snapshot.Position) > 2f)
        {
            RequestDecisionRefresh(runtime);
            CombatDiagnostics.RecordCommit(false);
            return;
        }
        CombatDiagnostics.RecordCommit(true);

        double now = CurrentTime;
        runtime.Plan = plan;
        runtime.NextPlanAt = now + ResolvePlanInterval(actor, snapshot.HighFidelity, snapshot.Revision);
        if (plan == null ||
            !plan.HasEnemy ||
            !IsValidEnemy(
                actor,
                plan.PrimaryEnemy.Object,
                plan.PrimaryEnemy.ThreatSource != CombatThreatSource.None))
        {
            UpdateArmyRout(actor, default, true);
            if (runtime.IsEngaged)
            {
                if (runtime.LostContactSince <= 0d) runtime.LostContactSince = now;
                if (now - runtime.LostContactSince < TacticalCombatSettings.LostContactGrace)
                {
                    runtime.CurrentTargetId = 0;
                    CombatMovementService.Clear(
                        actor,
                        runtime,
                        stopMovement: true,
                        clearBearing: true);
                    if (!actor.attack_target.isRekt()) actor.clearAttackTarget();
                    if (!actor.isTask(TacticalCombatSettings.TacticalTaskId))
                    {
                        actor.setTask(
                            TacticalCombatSettings.TacticalTaskId,
                            pClean: false,
                            pCleanJob: true);
                    }
                    return;
                }
            }
            LeaveCombat(actor, runtime);
            actor._timeout_targets = Mathf.Max(
                actor._timeout_targets,
                snapshot.HighFidelity ? 0.25f : 0.8f);
            return;
        }

        BaseSimObject enemy = plan.PrimaryEnemy.Object;
        runtime.LostContactSince = 0d;
        runtime.IsEngaged = true;
        runtime.CurrentTargetId = enemy.getID();
        actor.setAttackTarget(enemy);
        actor.beh_actor_target = enemy;
        if (!actor.isTask(TacticalCombatSettings.TacticalTaskId))
        {
            actor.setTask(
                TacticalCombatSettings.TacticalTaskId,
                pClean: false,
                pCleanJob: true);
        }

        if (UpdateArmyRout(actor, plan.Outcome, false))
        {
            runtime.Plan = null;
            CombatMovementService.Clear(
                actor,
                runtime,
                stopMovement: true,
                clearBearing: false);
            return;
        }

        CombatMovementService.Apply(actor, runtime, snapshot, plan, now);
    }

    private static CombatPlanningSnapshot BuildSnapshot(
        Actor actor,
        CombatActorRuntime runtime,
        double now)
    {
        bool highFidelity = actor.is_visible ||
                            actor.isFavorite() ||
                            actor.GetExtend().GetPowerLevel() >= 3f;
        int enemyCap = highFidelity ? 12 : 6;
        // Self 不在 Allies 中，因此友军上限必须比敌军少一，双方局部样本才代表相同规模。
        int allyCap = enemyCap - 1;
        int positionCap = highFidelity ? 16 : 8;
        RemoveExpiredRuntimeEntries(runtime, now);
        RecoverActorMorale(runtime, now);
        CleanupStaleArmyStates(now);
        CombatArmyRuntime armyRuntime = ResolveArmyState(actor, now);
        CombatDirective directive = ResolveDirective(actor, armyRuntime, now);
        float selfPower = ResolveActualPower(actor);
        var snapshot = new CombatPlanningSnapshot
        {
            ActorId = actor.getID(),
            Revision = runtime.Revision,
            Position = actor.current_position,
            HealthRatio = ResolveHealthRatio(actor),
            StaminaRatio = actor.data.stamina / Mathf.Max(1f, actor.getMaxStamina()),
            ManaRatio = actor.data.mana / Mathf.Max(1f, actor.getMaxMana()),
            SelfPower = selfPower,
            Morale = runtime.Morale,
            Aggression = Mathf.Clamp01(actor.stats["personality_aggression"]),
            Rationality = Mathf.Clamp(actor.stats["personality_rationality"], -1f, 1f),
            FormationCohesion = 1f,
            CurrentTargetId = runtime.CurrentTargetId,
            CurrentActionKey = runtime.Plan?.Action?.Key,
            CurrentActionUse = runtime.Plan?.ActionUse ?? CombatActionUse.None,
            CurrentIntent = runtime.Plan?.Intent ?? CombatIntent.None,
            CanRetreat = CanRetreat(actor),
            HighFidelity = highFidelity,
            ArmyRouted = armyRuntime?.Routed ?? false,
            Directive = directive
        };

        using var nearbyAllies = new ListPool<Actor>();
        CollectNearbyCombatAllies(actor, nearbyAllies);
        Dictionary<long, CombatThreatContext> threatContexts = CollectRelevantThreats(
            actor,
            runtime,
            armyRuntime,
            nearbyAllies,
            now);

        List<BaseSimObject> enemyObjects = CollectEnemyObjects(
            actor,
            runtime,
            armyRuntime,
            threatContexts,
            directive,
            enemyCap,
            now,
            out HashSet<long> visibleEnemyIds);
        if (enemyObjects.Count == 0) return snapshot;

        CombatObstacleSnapshot[] obstacles = BuildObstacleSnapshots(actor);
        CombatantSnapshot[] enemies = BuildEnemySnapshots(
            actor,
            runtime,
            armyRuntime,
            enemyObjects,
            visibleEnemyIds,
            threatContexts,
            obstacles,
            now);
        long preferredThreatenedAllyId = ResolvePreferredThreatenedAllyId(
            threatContexts,
            actor.getID());
        CombatantSnapshot[] allies = BuildAllySnapshots(
            actor,
            nearbyAllies,
            allyCap,
            preferredThreatenedAllyId);
        snapshot.FormationCohesion = ResolveFormationCohesion(snapshot.Position, allies);
        CombatantSnapshot provisionalEnemy = ResolveProvisionalEnemy(actor, enemies);
        Actor preferredAlly = ResolvePreferredAlly(
            allies,
            provisionalEnemy.ThreatenedAllyId);
        Vector2 engagementBearing = CombatMovementService.ResolveEngagementBearing(
            actor,
            runtime,
            provisionalEnemy);
        float hostilePower = 0f;
        for (int i = 0; i < enemies.Length; i++) hostilePower += enemies[i].EstimatedPower;
        float threatRatio = hostilePower / Mathf.Max(0.01f, selfPower);

        using var actions = new ListPool<CombatActionCandidate>();
        if (!provisionalEnemy.Object.isRekt())
        {
            CombatActionService.Collect(
                actor.GetExtend(),
                provisionalEnemy.Object,
                preferredAlly,
                threatRatio,
                nearbyAllies,
                actions);
        }
        CombatActionCandidate[] actionArray = LimitActions(actions, highFidelity ? 16 : 8);
        CombatPositionCandidate[] positions = BuildPositionCandidates(
            actor,
            provisionalEnemy,
            allies,
            enemies,
            actionArray,
            obstacles,
            engagementBearing,
            positionCap,
            directive,
            armyRuntime?.Routed ?? false);

        snapshot.Enemies = enemies;
        snapshot.Allies = allies;
        snapshot.Actions = actionArray;
        snapshot.Positions = positions;
        snapshot.Obstacles = obstacles;
        return snapshot;
    }

    /// <summary>
    /// 合并个人受袭、军队共享与同国近邻的近期威胁。结果按攻击者去重，避免同一目标重复进入快照。
    /// </summary>
    private static Dictionary<long, CombatThreatContext> CollectRelevantThreats(
        Actor actor,
        CombatActorRuntime runtime,
        CombatArmyRuntime armyRuntime,
        IReadOnlyList<Actor> nearbyAllies,
        double now)
    {
        var result = new Dictionary<long, CombatThreatContext>();
        AddRelevantThreats(
            actor,
            runtime.IncomingThreats,
            CombatThreatSource.Personal,
            float.MaxValue,
            now,
            result,
            allowSameKingdom: true);

        if (armyRuntime != null)
        {
            AddRelevantThreats(
                actor,
                armyRuntime.SharedThreats,
                CombatThreatSource.Army,
                TacticalCombatSettings.ArmyAssistRadius,
                now,
                result,
                allowSameKingdom: false);
        }

        if (actor.kingdom == null) return result;
        for (int i = 0; i < nearbyAllies.Count; i++)
        {
            Actor ally = nearbyAllies[i];
            if (Toolbox.SquaredDistVec2Float(actor.current_position, ally.current_position) >
                TacticalCombatSettings.NearbyAssistRadius *
                TacticalCombatSettings.NearbyAssistRadius)
                continue;
            if (!ActorStates.TryGetValue(ally.getID(), out CombatActorRuntime allyRuntime))
                continue;
            AddRelevantThreats(
                actor,
                allyRuntime.IncomingThreats,
                CombatThreatSource.NearbyAlly,
                TacticalCombatSettings.NearbyAssistRadius,
                now,
                result,
                allowSameKingdom: false);
        }
        return result;
    }

    /// <summary>筛选指定来源中仍新鲜、合法且位于响应半径内的威胁。</summary>
    private static void AddRelevantThreats(
        Actor actor,
        IReadOnlyDictionary<CombatThreatKey, CombatThreatSignal> source,
        CombatThreatSource threatSource,
        float radius,
        double now,
        IDictionary<long, CombatThreatContext> output,
        bool allowSameKingdom)
    {
        float radiusSquared = radius * radius;
        foreach (CombatThreatSignal signal in source.Values)
        {
            if (!IsUsableThreatSignal(actor, signal, allowSameKingdom, now)) continue;
            if (radius < float.MaxValue)
            {
                float victimDistance = Toolbox.SquaredDistVec2Float(
                    actor.current_position,
                    signal.VictimPosition);
                float attackerDistance = Toolbox.SquaredDistVec2Float(
                    actor.current_position,
                    signal.AttackerPosition);
                if (victimDistance > radiusSquared && attackerDistance > radiusSquared)
                    continue;
            }

            var candidate = new CombatThreatContext(signal, threatSource);
            if (!output.TryGetValue(signal.AttackerId, out CombatThreatContext current) ||
                ShouldReplaceThreat(current, candidate))
                output[signal.AttackerId] = candidate;
        }
    }

    /// <summary>验证威胁关系本身，不在这里执行目标阵营判断。</summary>
    private static bool IsUsableThreatSignal(
        Actor actor,
        CombatThreatSignal signal,
        bool allowSameKingdom,
        double now)
    {
        if (signal == null ||
            signal.Attacker.isRekt() ||
            signal.Victim.isRekt() ||
            now - signal.LastThreatAt > TacticalCombatSettings.ThreatLifetime)
            return false;
        if (signal.Victim != actor && signal.Victim.kingdom != actor.kingdom) return false;
        return allowSameKingdom || signal.Attacker.kingdom != actor.kingdom;
    }

    /// <summary>个人威胁优先于军队和近邻，同来源时保留更严重或更新的记录。</summary>
    private static bool ShouldReplaceThreat(
        CombatThreatContext current,
        CombatThreatContext candidate)
    {
        int currentPriority = ResolveThreatSourcePriority(current.Source);
        int candidatePriority = ResolveThreatSourcePriority(candidate.Source);
        if (currentPriority != candidatePriority) return candidatePriority < currentPriority;
        if (!Mathf.Approximately(current.Signal.Severity, candidate.Signal.Severity))
            return candidate.Signal.Severity > current.Signal.Severity;
        return candidate.Signal.LastThreatAt > current.Signal.LastThreatAt;
    }

    /// <summary>返回越小越优先的威胁来源顺序。</summary>
    private static int ResolveThreatSourcePriority(CombatThreatSource source)
    {
        return source switch
        {
            CombatThreatSource.Personal => 0,
            CombatThreatSource.Army => 1,
            CombatThreatSource.NearbyAlly => 2,
            _ => 3
        };
    }

    /// <summary>选择最应被保留在友军快照中的受援者。</summary>
    private static long ResolvePreferredThreatenedAllyId(
        IReadOnlyDictionary<long, CombatThreatContext> threats,
        long actorId)
    {
        long result = 0;
        float severity = float.MinValue;
        double time = double.MinValue;
        foreach (CombatThreatContext context in threats.Values)
        {
            CombatThreatSignal signal = context.Signal;
            if (signal.VictimId == actorId) continue;
            if (signal.Severity < severity ||
                Mathf.Approximately(signal.Severity, severity) && signal.LastThreatAt <= time)
                continue;
            result = signal.VictimId;
            severity = signal.Severity;
            time = signal.LastThreatAt;
        }
        return result;
    }

    private static List<BaseSimObject> CollectEnemyObjects(
        Actor actor,
        CombatActorRuntime runtime,
        CombatArmyRuntime armyRuntime,
        IReadOnlyDictionary<long, CombatThreatContext> threatContexts,
        CombatDirective directive,
        int cap,
        double now,
        out HashSet<long> visibleEnemyIds)
    {
        var result = new List<BaseSimObject>(cap * 2);
        var seen = new HashSet<long>();
        var visible = new HashSet<long>();
        visibleEnemyIds = visible;
        AddEnemyCandidate(actor, actor.attack_target, runtime, now, seen, result);

        foreach (CombatThreatContext context in threatContexts.Values)
        {
            AddEnemyCandidate(
                actor,
                context.Signal.Attacker,
                runtime,
                now,
                seen,
                result,
                confirmedThreat: true);
        }

        EnemyFinderData enemyData = EnemiesFinder.findEnemiesFrom(actor.current_tile, actor.kingdom);
        List<BaseSimObject> primary = enemyData.list;
        for (int i = 0; i < primary.Count; i++)
        {
            if (!primary[i].isRekt()) visible.Add(primary[i].getID());
            AddEnemyCandidate(actor, primary[i], runtime, now, seen, result);
        }
        foreach (long targetId in actor._aggression_targets)
        {
            AddEnemyCandidate(
                actor,
                World.world.units.get(targetId),
                runtime,
                now,
                seen,
                result,
                confirmedThreat: true);
        }
        foreach (long attackerId in runtime.RecentAttackers.Keys)
        {
            AddEnemyCandidate(
                actor,
                World.world.units.get(attackerId),
                runtime,
                now,
                seen,
                result,
                confirmedThreat: true);
        }
        if (armyRuntime != null &&
            directive is CombatDirective.Attack or CombatDirective.Protect)
        {
            foreach (CombatObservation observation in armyRuntime.SharedObservations.Values)
            {
                if (observation.Confidence <= 0.05f ||
                    now - observation.LastLocationAt >
                    TacticalCombatSettings.TacticalLocationLifetime)
                    continue;
                AddEnemyCandidate(
                    actor,
                    observation.TargetObject,
                    runtime,
                    now,
                    seen,
                    result);
            }
        }
        for (int i = 0; i < result.Count; i++)
        {
            BaseSimObject candidate = result[i];
            if (candidate.current_tile == null) continue;
            int range = SimGlobals.m.unit_chunk_sight_range;
            if (Math.Abs(actor.current_tile.chunk.x - candidate.current_tile.chunk.x) <= range &&
                Math.Abs(actor.current_tile.chunk.y - candidate.current_tile.chunk.y) <= range)
                visible.Add(candidate.getID());
        }

        result.Sort((left, right) =>
        {
            bool leftThreat = threatContexts.TryGetValue(
                left.getID(),
                out CombatThreatContext leftContext);
            bool rightThreat = threatContexts.TryGetValue(
                right.getID(),
                out CombatThreatContext rightContext);
            if (leftThreat != rightThreat) return leftThreat ? -1 : 1;
            if (leftThreat)
            {
                int source = ResolveThreatSourcePriority(leftContext.Source)
                             .CompareTo(ResolveThreatSourcePriority(rightContext.Source));
                if (source != 0) return source;
                int severity = rightContext.Signal.Severity.CompareTo(leftContext.Signal.Severity);
                if (severity != 0) return severity;
            }
            bool leftRecent = runtime.RecentAttackers.ContainsKey(left.getID());
            bool rightRecent = runtime.RecentAttackers.ContainsKey(right.getID());
            if (leftRecent != rightRecent) return leftRecent ? -1 : 1;
            float leftDistance = Toolbox.SquaredDistVec2Float(
                actor.current_position,
                leftThreat
                    ? leftContext.Signal.AttackerPosition
                    : ResolveKnownPosition(
                        left,
                        visible,
                        runtime,
                        armyRuntime));
            float rightDistance = Toolbox.SquaredDistVec2Float(
                actor.current_position,
                rightThreat
                    ? rightContext.Signal.AttackerPosition
                    : ResolveKnownPosition(
                        right,
                        visible,
                        runtime,
                        armyRuntime));
            return leftDistance.CompareTo(rightDistance);
        });
        if (result.Count > cap) result.RemoveRange(cap, result.Count - cap);
        return result;
    }

    /// <summary>返回排序时允许角色掌握的目标位置，避免不可见目标按实时坐标获得优先级。</summary>
    private static Vector2 ResolveKnownPosition(
        BaseSimObject target,
        ISet<long> visibleEnemyIds,
        CombatActorRuntime runtime,
        CombatArmyRuntime armyRuntime)
    {
        long targetId = target.getID();
        if (visibleEnemyIds.Contains(targetId)) return target.current_position;
        if (runtime.Observations.TryGetValue(targetId, out CombatObservation personal))
            return personal.LastPosition;
        if (armyRuntime != null &&
            armyRuntime.SharedObservations.TryGetValue(targetId, out CombatObservation shared))
            return shared.LastPosition;
        return target.current_position;
    }

    private static void AddEnemyCandidate(
        Actor actor,
        BaseSimObject candidate,
        CombatActorRuntime runtime,
        double now,
        ISet<long> seen,
        ICollection<BaseSimObject> output)
    {
        AddEnemyCandidate(actor, candidate, runtime, now, seen, output, confirmedThreat: false);
    }

    /// <summary>验证普通敌人或已由真实事件确认的攻击者，并加入去重后的目标池。</summary>
    private static void AddEnemyCandidate(
        Actor actor,
        BaseSimObject candidate,
        CombatActorRuntime runtime,
        double now,
        ISet<long> seen,
        ICollection<BaseSimObject> output,
        bool confirmedThreat)
    {
        if (candidate.isRekt() || candidate == actor) return;
        long id = candidate.getID();
        if (seen.Contains(id)) return;
        if (runtime.IgnoreCurrentTargetUntil > now &&
            runtime.IgnoredTargetId == id)
            return;
        if (actor.shouldIgnoreTarget(candidate)) return;
        if (!actor.canAttackTarget(
                candidate,
                pCheckForFactions: !confirmedThreat,
                pAttackBuildings: actor.asset.can_attack_buildings))
            return;
        seen.Add(id);
        output.Add(candidate);
    }

    private static CombatantSnapshot[] BuildEnemySnapshots(
        Actor actor,
        CombatActorRuntime runtime,
        CombatArmyRuntime armyRuntime,
        IReadOnlyList<BaseSimObject> enemies,
        ISet<long> visibleEnemyIds,
        IReadOnlyDictionary<long, CombatThreatContext> threatContexts,
        IReadOnlyList<CombatObstacleSnapshot> obstacles,
        double now)
    {
        var result = new CombatantSnapshot[enemies.Count];
        for (int i = 0; i < enemies.Count; i++)
        {
            BaseSimObject enemy = enemies[i];
            bool visible = visibleEnemyIds.Contains(enemy.getID());
            bool hasThreat = threatContexts.TryGetValue(
                enemy.getID(),
                out CombatThreatContext threatContext);
            CombatObservation observation = visible || !hasThreat
                ? CombatObservationService.ResolveKnown(
                    runtime,
                    armyRuntime,
                    actor,
                    enemy,
                    now,
                    visible)
                : null;
            Vector2 knownPosition = visible
                ? enemy.current_position
                : hasThreat
                    ? threatContext.Signal.AttackerPosition
                    : observation.LastPosition;
            float healthRatio = visible
                ? ResolveHealthRatio(enemy)
                : hasThreat
                    ? threatContext.Signal.AttackerHealthRatio
                    : observation.LastHealthRatio;
            float estimatedPower = hasThreat && !visible
                ? threatContext.Signal.AttackerPower
                : observation.EstimatedPower;
            float confidence = hasThreat && !visible
                ? threatContext.Signal.Confidence
                : observation.Confidence;
            float size = hasThreat && !visible
                ? threatContext.Signal.AttackerSize
                : observation.LastSize;
            bool airborne = hasThreat && !visible
                ? threatContext.Signal.AttackerAirborne
                : observation.LastAirborne;
            bool attackingPlanner = visible &&
                                    enemy.isActor() &&
                                    enemy.a.has_attack_target &&
                                    enemy.a.attack_target == actor;
            result[i] = new CombatantSnapshot(
                enemy,
                i,
                enemy.getID(),
                knownPosition,
                healthRatio,
                estimatedPower,
                confidence,
                size,
                enemy.isActor(),
                airborne,
                runtime.RecentAttackers.TryGetValue(enemy.getID(), out double attackedAt) &&
                now - attackedAt <= TacticalCombatSettings.TacticalLocationLifetime,
                attackingPlanner,
                !CombatPlanner.IsShotBlocked(
                    actor.current_position,
                    knownPosition,
                    obstacles),
                hasThreat ? threatContext.Signal.VictimId : 0,
                hasThreat ? threatContext.Signal.VictimPosition : default,
                hasThreat ? threatContext.Signal.Severity : 0f,
                hasThreat ? threatContext.Source : CombatThreatSource.None);
        }
        return result;
    }

    /// <summary>收集明确局部半径内、能够实际参加战斗的同国单位。</summary>
    private static void CollectNearbyCombatAllies(Actor actor, ListPool<Actor> output)
    {
        float radiusSquared = TacticalCombatSettings.LocalCombatRadius *
                              TacticalCombatSettings.LocalCombatRadius;
        foreach (Actor candidate in Finder.getUnitsFromChunk(actor.current_tile, 3))
        {
            if (candidate.isRekt() ||
                candidate == actor ||
                candidate.kingdom != actor.kingdom ||
                candidate.current_tile == null ||
                candidate.asset.skip_fight_logic ||
                candidate.is_unconscious ||
                !candidate.isAllowedToLookForEnemies() ||
                Toolbox.SquaredDistVec2Float(
                    actor.current_position,
                    candidate.current_position) > radiusSquared)
                continue;
            output.Add(candidate);
        }

        output.Sort((left, right) =>
        {
            float leftDistance = Toolbox.SquaredDistVec2Float(
                actor.current_position,
                left.current_position);
            float rightDistance = Toolbox.SquaredDistVec2Float(
                actor.current_position,
                right.current_position);
            return leftDistance.CompareTo(rightDistance);
        });
    }

    /// <summary>将局部友军冻结为规划快照，并保证受援者不会因数量上限被遗漏。</summary>
    private static CombatantSnapshot[] BuildAllySnapshots(
        Actor actor,
        IReadOnlyList<Actor> nearby,
        int cap,
        long preferredThreatenedAllyId)
    {
        int count = Math.Min(cap, nearby.Count);
        if (count <= 0) return Array.Empty<CombatantSnapshot>();
        var selected = new Actor[count];
        for (int i = 0; i < count; i++) selected[i] = nearby[i];
        if (preferredThreatenedAllyId != 0)
        {
            for (int i = count; i < nearby.Count; i++)
            {
                if (nearby[i].getID() != preferredThreatenedAllyId) continue;
                selected[count - 1] = nearby[i];
                break;
            }
        }

        var result = new CombatantSnapshot[count];
        for (int i = 0; i < count; i++)
        {
            Actor ally = selected[i];
            result[i] = new CombatantSnapshot(
                ally,
                -1,
                ally.getID(),
                ally.current_position,
                ResolveHealthRatio(ally),
                ResolveActualPower(ally),
                1f,
                ally.stats[S.size],
                true,
                ally.isFlying(),
                false,
                false,
                true);
        }
        return result;
    }

    /// <summary>
    /// 根据最近四名友军的距离估算队形凝聚度。只看最近成员，避免远处同阵营单位让已经成形的小队反复集结。
    /// </summary>
    private static float ResolveFormationCohesion(
        Vector2 position,
        IReadOnlyList<CombatantSnapshot> allies)
    {
        if (allies.Count == 0) return 1f;
        int count = Math.Min(4, allies.Count);
        float cohesion = 0f;
        for (int i = 0; i < count; i++)
        {
            float distance = Vector2.Distance(position, allies[i].Position);
            cohesion += 1f - Mathf.InverseLerp(3f, 12f, distance);
        }
        return Mathf.Clamp01(cohesion / count);
    }

    private static Actor ResolvePreferredAlly(
        IReadOnlyList<CombatantSnapshot> allies,
        long threatenedAllyId)
    {
        Actor result = null;
        float health = 1f;
        for (int i = 0; i < allies.Count; i++)
        {
            if (threatenedAllyId != 0 && allies[i].Id == threatenedAllyId)
                return allies[i].Object.a;
            if (!allies[i].IsActor || allies[i].HealthRatio >= health) continue;
            result = allies[i].Object.a;
            health = allies[i].HealthRatio;
        }
        return result;
    }

    private static CombatantSnapshot ResolveProvisionalEnemy(
        Actor actor,
        IReadOnlyList<CombatantSnapshot> enemies)
    {
        if (!actor.attack_target.isRekt())
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                if (enemies[i].Object == actor.attack_target) return enemies[i];
            }
        }
        return enemies.Count > 0 ? enemies[0] : default;
    }

    private static CombatActionCandidate[] LimitActions(
        IList<CombatActionCandidate> actions,
        int cap)
    {
        if (actions.Count == 0 || cap <= 0) return Array.Empty<CombatActionCandidate>();
        var ordered = new CombatActionCandidate[actions.Count];
        for (int i = 0; i < actions.Count; i++) ordered[i] = actions[i];
        Array.Sort(ordered, (left, right) =>
        {
            float leftValue = left.Profile.Power + left.Profile.Control + left.Profile.Utility +
                              left.Profile.BaseWeight * 0.1f;
            float rightValue = right.Profile.Power + right.Profile.Control + right.Profile.Utility +
                               right.Profile.BaseWeight * 0.1f;
            return rightValue.CompareTo(leftValue);
        });
        if (ordered.Length <= cap) return ordered;

        var selected = new List<CombatActionCandidate>(cap);
        for (int i = 0; i < ordered.Length && selected.Count < cap; i++)
        {
            if (ordered[i].Key.ProviderId == PhysicalCombatActionProvider.ProviderId)
                selected.Add(ordered[i]);
        }
        AddBestAction(
            ordered,
            selected,
            cap,
            candidate => candidate.Profile.HasPurpose(CombatActionPurpose.Mobility));
        AddBestAction(
            ordered,
            selected,
            cap,
            candidate => candidate.Profile.HasPurpose(CombatActionPurpose.Defense));
        AddBestAction(
            ordered,
            selected,
            cap,
            candidate => candidate.Profile.HasPurpose(CombatActionPurpose.Support));
        AddBestAction(
            ordered,
            selected,
            cap,
            candidate => candidate.Profile.HasPurpose(CombatActionPurpose.Control));
        for (int i = 0; i < ordered.Length && selected.Count < cap; i++)
        {
            if (!selected.Contains(ordered[i])) selected.Add(ordered[i]);
        }
        return selected.ToArray();
    }

    /// <summary>为动作上限保留一种战术用途，避免高评分法术把近战或保命动作全部挤出。</summary>
    private static void AddBestAction(
        IReadOnlyList<CombatActionCandidate> ordered,
        ICollection<CombatActionCandidate> selected,
        int cap,
        Func<CombatActionCandidate, bool> predicate)
    {
        if (selected.Count >= cap) return;
        for (int i = 0; i < ordered.Count; i++)
        {
            CombatActionCandidate candidate = ordered[i];
            if (!predicate(candidate) || selected.Contains(candidate)) continue;
            selected.Add(candidate);
            return;
        }
    }

    private static CombatObstacleSnapshot[] BuildObstacleSnapshots(Actor actor)
    {
        using var source = new ListPool<SkillPersistentCombatSnapshot>();
        LogicSkillPersistentSystem.CopyCombatSnapshots(
            actor.current_position,
            72f,
            source);
        var result = new CombatObstacleSnapshot[source.Count];
        for (int i = 0; i < source.Count; i++)
        {
            SkillPersistentCombatSnapshot obstacle = source[i];
            SkillImpactKind kind = obstacle.Kind switch
            {
                SkillPersistentKind.Field => SkillImpactKind.Field,
                SkillPersistentKind.Barrier => SkillImpactKind.Wall,
                SkillPersistentKind.Shield => SkillImpactKind.Shield,
                _ => SkillImpactKind.Field
            };
            bool hostile = actor.kingdom != null &&
                           obstacle.Kingdom != null &&
                           actor.kingdom.isEnemy(obstacle.Kingdom);
            result[i] = new CombatObstacleSnapshot(
                obstacle.SourceId,
                obstacle.Kingdom,
                kind,
                obstacle.Position,
                obstacle.Direction,
                obstacle.Length,
                obstacle.Width,
                obstacle.Durability,
                hostile);
        }
        return result;
    }

    private static CombatPositionCandidate[] BuildPositionCandidates(
        Actor actor,
        CombatantSnapshot provisionalEnemy,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatActionCandidate> actions,
        IReadOnlyList<CombatObstacleSnapshot> obstacles,
        Vector2 engagementBearing,
        int cap,
        CombatDirective directive,
        bool armyRouted)
    {
        if (provisionalEnemy.Object.isRekt()) return Array.Empty<CombatPositionCandidate>();

        var result = new List<CombatPositionCandidate>(cap);
        var seen = new HashSet<int>();
        AddPositionCandidate(
            actor,
            actor.current_tile,
            CombatPositionRole.Tactical,
            provisionalEnemy,
            allies,
            enemies,
            obstacles,
            seen,
            result);
        bool retreatFirst = armyRouted || directive == CombatDirective.Retreat;
        if (retreatFirst &&
            actor.city != null &&
            result.Count < cap)
        {
            AddPositionCandidate(
                actor,
                actor.city.getTile(),
                CombatPositionRole.CityRetreat,
                provisionalEnemy,
                allies,
                enemies,
                obstacles,
                seen,
                result);
        }

        float[] preferredRanges = ResolvePositionRanges(actions);
        if (!retreatFirst)
        {
            AddRangePositionCandidates(
                actor,
                provisionalEnemy,
                allies,
                enemies,
                obstacles,
                preferredRanges,
                engagementBearing,
                [0f],
                cap,
                seen,
                result);
        }
        AddAssistancePositionCandidates(
            actor,
            provisionalEnemy,
            allies,
            enemies,
            obstacles,
            cap,
            seen,
            result);
        AddSafePositionCandidates(
            actor,
            provisionalEnemy,
            allies,
            enemies,
            obstacles,
            cap,
            seen,
            result);
        if (allies.Count > 0 && result.Count < cap)
        {
            Vector2 center = Vector2.zero;
            for (int i = 0; i < allies.Count; i++) center += allies[i].Position;
            center /= allies.Count;
            AddPositionCandidate(
                actor,
                ResolveRallySlotTile(actor, center, 2.25f),
                CombatPositionRole.AllyRally,
                provisionalEnemy,
                allies,
                enemies,
                obstacles,
                seen,
                result);
        }
        if (actor.army != null && result.Count < cap)
        {
            Actor captain = actor.army.getCaptain();
            if (!captain.isRekt())
            {
                AddPositionCandidate(
                    actor,
                    captain == actor
                        ? actor.current_tile
                        : ResolveRallySlotTile(actor, captain.current_position, 2.5f),
                    CombatPositionRole.CaptainRally,
                    provisionalEnemy,
                    allies,
                    enemies,
                    obstacles,
                    seen,
                    result);
            }
        }
        if (retreatFirst)
        {
            AddRangePositionCandidates(
                actor,
                provisionalEnemy,
                allies,
                enemies,
                obstacles,
                preferredRanges,
                engagementBearing,
                [0f],
                cap,
                seen,
                result);
        }

        float[] flankAngles = cap >= 16
            ? [45f, -45f, 90f, -90f, 180f]
            : [60f, -60f, 180f];
        AddRangePositionCandidates(
            actor,
            provisionalEnemy,
            allies,
            enemies,
            obstacles,
            preferredRanges,
            engagementBearing,
            flankAngles,
            cap,
            seen,
            result);
        return result.ToArray();
    }

    /// <summary>围绕受袭友军生成援助槽位和攻击者方向上的插入槽位。</summary>
    private static void AddAssistancePositionCandidates(
        Actor actor,
        CombatantSnapshot threat,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatObstacleSnapshot> obstacles,
        int cap,
        ISet<int> seen,
        ICollection<CombatPositionCandidate> output)
    {
        if (threat.ThreatenedAllyId == 0 ||
            threat.ThreatenedAllyId == actor.getID() ||
            output.Count >= cap)
            return;

        Vector2 threatenedPosition = threat.ThreatenedAllyPosition;
        for (int i = 0; i < allies.Count; i++)
        {
            if (allies[i].Id != threat.ThreatenedAllyId) continue;
            threatenedPosition = allies[i].Position;
            break;
        }
        AddPositionCandidate(
            actor,
            ResolveRallySlotTile(actor, threatenedPosition, 2f),
            CombatPositionRole.AssistRally,
            threat,
            allies,
            enemies,
            obstacles,
            seen,
            output,
            threat.ThreatenedAllyId);
        if (output.Count >= cap) return;

        Vector2 direction = threat.Position - threatenedPosition;
        if (direction.sqrMagnitude < 0.01f) direction = Vector2.right;
        else direction.Normalize();
        Vector2 interpose = threatenedPosition + direction * 2f;
        AddPositionCandidate(
            actor,
            World.world.GetTile(
                Mathf.RoundToInt(interpose.x),
                Mathf.RoundToInt(interpose.y)),
            CombatPositionRole.Interpose,
            threat,
            allies,
            enemies,
            obstacles,
            seen,
            output,
            threat.ThreatenedAllyId);
    }

    /// <summary>
    /// 在集结中心周围为角色分配稳定槽位，避免所有成员反复寻路到同一个地块。
    /// 槽位只由角色 ID 决定，同一次战斗中不会随规划轮次抖动。
    /// </summary>
    private static WorldTile ResolveRallySlotTile(Actor actor, Vector2 center, float baseRadius)
    {
        ulong hash = ResolveFormationHash(actor.getID());
        int slot = (int)(hash % 16UL);
        int ring = (int)((hash >> 8) % 3UL);
        float angle = slot * 22.5f + ring * 7.5f;
        float radius = baseRadius + ring * 1.25f;
        Vector2 position = center + Toolbox.rotateVector(Vector2.right, angle) * radius;
        return World.world.GetTile(
            Mathf.RoundToInt(position.x),
            Mathf.RoundToInt(position.y));
    }

    /// <summary>生成只依赖角色 ID 的稳定编队散列。</summary>
    private static ulong ResolveFormationHash(long actorId)
    {
        ulong hash = unchecked((ulong)actorId);
        hash ^= hash >> 30;
        hash *= 0xBF58476D1CE4E5B9UL;
        hash ^= hash >> 27;
        return hash;
    }

    /// <summary>
    /// 从可用攻击动作中提取近、中、远三个代表距离，保证混合近战施法者两种站位都有候选点。
    /// </summary>
    private static float[] ResolvePositionRanges(IReadOnlyList<CombatActionCandidate> actions)
    {
        float min = float.MaxValue;
        float max = 0f;
        for (int i = 0; i < actions.Count; i++)
        {
            CombatActionProfile profile = actions[i].Profile;
            if (!profile.HasPurpose(CombatActionPurpose.Offense) &&
                !profile.HasPurpose(CombatActionPurpose.Control))
                continue;
            float range = Mathf.Clamp(profile.PreferredRange, 1.5f, 20f);
            min = Mathf.Min(min, range);
            max = Mathf.Max(max, range);
        }
        if (min == float.MaxValue) return [1.5f];
        if (max - min < 1.5f) return [max];
        float middle = Mathf.Lerp(min, max, 0.5f);
        return max - min >= 4f
            ? [min, max, middle]
            : [min, max];
    }

    /// <summary>在每个角度批次内优先覆盖全部代表距离，使小候选上限下仍有近战和远程站位。</summary>
    private static void AddRangePositionCandidates(
        Actor actor,
        CombatantSnapshot primary,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatObstacleSnapshot> obstacles,
        IReadOnlyList<float> ranges,
        Vector2 radial,
        IReadOnlyList<float> angles,
        int cap,
        ISet<int> seen,
        ICollection<CombatPositionCandidate> output)
    {
        for (int angleIndex = 0; angleIndex < angles.Count && output.Count < cap; angleIndex++)
        {
            Vector2 direction = Toolbox.rotateVector(radial, angles[angleIndex]);
            for (int rangeIndex = 0; rangeIndex < ranges.Count && output.Count < cap; rangeIndex++)
            {
                Vector2 position = primary.Position + direction * ranges[rangeIndex];
                AddPositionCandidate(
                    actor,
                    World.world.GetTile(
                        Mathf.RoundToInt(position.x),
                        Mathf.RoundToInt(position.y)),
                    CombatPositionRole.Tactical,
                    primary,
                    allies,
                    enemies,
                    obstacles,
                    seen,
                    output);
            }
        }
    }

    /// <summary>
    /// 从局部敌群质心反方向采样安全点，使近战单位也能产生有意义的脱离距离。
    /// </summary>
    private static void AddSafePositionCandidates(
        Actor actor,
        CombatantSnapshot primary,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatObstacleSnapshot> obstacles,
        int cap,
        ISet<int> seen,
        ICollection<CombatPositionCandidate> output)
    {
        Vector2 enemyCenter = Vector2.zero;
        float totalWeight = 0f;
        for (int i = 0; i < enemies.Count; i++)
        {
            float weight = Mathf.Max(0.1f, enemies[i].EstimatedPower);
            enemyCenter += enemies[i].Position * weight;
            totalWeight += weight;
        }
        if (totalWeight > 0f) enemyCenter /= totalWeight;
        else enemyCenter = primary.Position;

        Vector2 away = actor.current_position - enemyCenter;
        if (away.sqrMagnitude < 0.01f) away = Vector2.right;
        away.Normalize();
        float distance = cap >= 16 ? 12f : 8f;
        float[] angles = [-40f, 0f, 40f];
        for (int i = 0; i < angles.Length && output.Count < cap; i++)
        {
            Vector2 direction = Toolbox.rotateVector(away, angles[i]);
            Vector2 position = actor.current_position + direction * distance;
            AddPositionCandidate(
                actor,
                World.world.GetTile(Mathf.RoundToInt(position.x), Mathf.RoundToInt(position.y)),
                CombatPositionRole.Safe,
                primary,
                allies,
                enemies,
                obstacles,
                seen,
                output);
        }
    }

    private static void AddPositionCandidate(
        Actor actor,
        WorldTile tile,
        CombatPositionRole role,
        CombatantSnapshot primary,
        IReadOnlyList<CombatantSnapshot> allies,
        IReadOnlyList<CombatantSnapshot> enemies,
        IReadOnlyList<CombatObstacleSnapshot> obstacles,
        ISet<int> seen,
        ICollection<CombatPositionCandidate> output,
        long relatedAllyId = 0)
    {
        if (tile == null || tile.Type == null) return;
        int key = unchecked(tile.x * 397 ^ tile.y);
        if (!seen.Add(key)) return;
        if (!actor.isFlying())
        {
            if (tile.Type.block || !tile.isSameIsland(actor.current_tile)) return;
            if (actor.isWaterCreature() != tile.is_liquid && !actor.asset.force_land_creature) return;
        }

        Vector2 position = tile.posV;
        if (!actor.isFlying() &&
            IsGroundPathBlocked(actor.current_position, position, obstacles))
            return;
        float enemyPressure = 0f;
        for (int i = 0; i < enemies.Count; i++)
        {
            float distance = Vector2.Distance(position, enemies[i].Position);
            enemyPressure += enemies[i].EstimatedPower / Mathf.Max(1f, distance);
        }
        float allySupport = 0f;
        float crowding = 0f;
        float actorSize = Mathf.Max(0f, actor.stats[S.size]);
        for (int i = 0; i < allies.Count; i++)
        {
            float distance = Vector2.Distance(position, allies[i].Position);
            allySupport += allies[i].EstimatedPower / Mathf.Max(1f, distance);
            float personalSpace = Mathf.Clamp(
                1.75f + (actorSize + allies[i].Size) * 0.25f,
                1.75f,
                4f);
            crowding += 1f - Mathf.Clamp01(distance / personalSpace);
        }
        ulong clearShotMask = 0UL;
        for (int i = 0; i < enemies.Count && i < 64; i++)
        {
            if (CombatPlanner.IsShotBlocked(position, enemies[i].Position, obstacles))
                continue;
            clearShotMask |= 1UL << i;
        }
        output.Add(new CombatPositionCandidate(
            tile,
            role,
            position,
            enemyPressure,
            allySupport,
            crowding,
            clearShotMask,
            relatedAllyId));
    }

    /// <summary>判断候选移动线段是否穿过会真实阻挡该角色的敌对墙体。</summary>
    private static bool IsGroundPathBlocked(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<CombatObstacleSnapshot> obstacles)
    {
        for (int i = 0; i < obstacles.Count; i++)
        {
            CombatObstacleSnapshot obstacle = obstacles[i];
            if (!obstacle.IsHostile ||
                obstacle.Durability <= 0f ||
                obstacle.Kind != SkillImpactKind.Wall)
                continue;
            Vector2 side = new(-obstacle.Direction.y, obstacle.Direction.x);
            Vector2 half = side * (obstacle.Length * 0.5f);
            if (CombatGeometry.SegmentDistanceSquared(
                    start,
                    end,
                    obstacle.Position - half,
                    obstacle.Position + half) <= obstacle.Width * obstacle.Width)
                return true;
        }
        return false;
    }

    private static CombatArmyRuntime ResolveArmyState(Actor actor, double now)
    {
        if (actor.army == null) return null;
        CombatArmyRuntime runtime = GetOrCreateArmyState(actor.army);
        int alive = 0;
        List<Actor> units = actor.army.units;
        for (int i = 0; i < units.Count; i++)
        {
            if (!units[i].isRekt()) alive++;
        }
        runtime.PeakMemberCount = Math.Max(runtime.PeakMemberCount, alive);
        float casualties = runtime.PeakMemberCount <= 0
            ? 0f
            : 1f - alive / (float)runtime.PeakMemberCount;
        double elapsed = runtime.LastUpdatedAt <= 0d
            ? 0d
            : Math.Max(0d, now - runtime.LastUpdatedAt);
        runtime.LastUpdatedAt = now;
        float newCasualties = Mathf.Max(0f, casualties - runtime.LastCasualtyRatio);
        runtime.LastCasualtyRatio = casualties;
        RemoveExpiredOutcomeReports(runtime, now);
        runtime.Morale = Mathf.Clamp01(
            runtime.Morale +
            (runtime.OutcomeReports.Count == 0 ? (float)elapsed * 0.005f : 0f) -
            newCasualties * 1.2f);
        CombatObservationService.RemoveExpired(runtime.SharedObservations, now);
        RemoveExpiredThreats(runtime.SharedThreats, now);
        Actor captain = actor.army.getCaptain();
        long captainId = captain.isRekt() ? 0 : captain.getID();
        if (runtime.LastCaptainId != 0 &&
            runtime.LastCaptainId != captainId &&
            runtime.RecordedLostCaptainId != runtime.LastCaptainId)
        {
            Actor previousCaptain = World.world.units.get(runtime.LastCaptainId);
            if (previousCaptain.isRekt())
            {
                runtime.RecordedLostCaptainId = runtime.LastCaptainId;
                runtime.LastCaptainLossAt = now;
                runtime.Morale = Mathf.Clamp01(runtime.Morale - 0.2f);
            }
        }
        if (captainId != 0) runtime.LastCaptainId = captainId;
        return runtime;
    }

    /// <summary>移除不再代表当前局部战况的成员报告。</summary>
    private static void RemoveExpiredOutcomeReports(
        CombatArmyRuntime runtime,
        double now)
    {
        using var stale = new ListPool<long>();
        foreach (KeyValuePair<long, CombatOutcomeReport> pair in runtime.OutcomeReports)
        {
            if (now - pair.Value.ReportedAt > TacticalCombatSettings.ArmyRoutReportLifetime)
                stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++) runtime.OutcomeReports.Remove(stale[i]);
    }

    private static CombatDirective ResolveDirective(
        Actor actor,
        CombatArmyRuntime armyRuntime,
        double now)
    {
        if (armyRuntime == null) return CombatDirective.Attack;
        if (armyRuntime.Routed) return CombatDirective.Retreat;
        if (armyRuntime.DirectiveExpiresAt > now) return armyRuntime.Directive;
        armyRuntime.Directive = actor.city != null && actor.city.hasAttackZoneOrder()
            ? CombatDirective.Attack
            : CombatDirective.Hold;
        armyRuntime.DirectiveExpiresAt = now + 1d;
        return armyRuntime.Directive;
    }

    private static bool UpdateArmyRout(
        Actor actor,
        CombatOutcomeEstimate outcome,
        bool noEnemies)
    {
        if (actor.army == null ||
            !ArmyStates.TryGetValue(actor.army.id, out CombatArmyRuntime armyRuntime))
            return false;
        bool routedBefore = armyRuntime.Routed;

        if (noEnemies)
        {
            armyRuntime.OutcomeReports.Remove(actor.getID());
        }
        else
        {
            if (!armyRuntime.OutcomeReports.TryGetValue(
                    actor.getID(),
                    out CombatOutcomeReport report))
            {
                report = new CombatOutcomeReport();
                armyRuntime.OutcomeReports.Add(actor.getID(), report);
            }
            report.StrengthRatio = outcome.StrengthRatio;
            report.Survival = outcome.Survival;
            report.ReportedAt = CurrentTime;
        }
        RemoveExpiredOutcomeReports(armyRuntime, CurrentTime);

        int alive = 0;
        int engaged = 0;
        for (int i = 0; i < actor.army.units.Count; i++)
        {
            Actor member = actor.army.units[i];
            if (member.isRekt()) continue;
            alive++;
            if (ActorStates.TryGetValue(member.getID(), out CombatActorRuntime memberRuntime) &&
                memberRuntime.IsEngaged)
                engaged++;
        }
        float casualties = armyRuntime.PeakMemberCount <= 0
            ? 0f
            : 1f - alive / (float)armyRuntime.PeakMemberCount;
        int reportCount = armyRuntime.OutcomeReports.Count;
        int requiredReports = Math.Min(
            8,
            Math.Max(2, Mathf.CeilToInt(Mathf.Max(engaged, reportCount) * 0.35f)));
        int unfavorableReports = 0;
        foreach (CombatOutcomeReport report in armyRuntime.OutcomeReports.Values)
        {
            if (report.StrengthRatio < TacticalCombatSettings.ArmyRoutLocalRatio)
                unfavorableReports++;
        }
        bool consensus = reportCount >= requiredReports &&
                         unfavorableReports >= Mathf.CeilToInt(
                             reportCount * TacticalCombatSettings.ArmyRoutConsensusRatio);
        bool routePressure = !armyRuntime.Routed &&
                             casualties >= TacticalCombatSettings.ArmyRoutMinimumCasualtyRatio &&
                             armyRuntime.Morale <= TacticalCombatSettings.ArmyRoutMorale &&
                             consensus;
        if (routePressure)
        {
            if (armyRuntime.RoutPressureSince <= 0d)
                armyRuntime.RoutPressureSince = CurrentTime;
        }
        else
        {
            armyRuntime.RoutPressureSince = 0d;
        }

        if (!armyRuntime.Routed &&
            armyRuntime.RoutPressureSince > 0d &&
            CurrentTime - armyRuntime.RoutPressureSince >=
            TacticalCombatSettings.ArmyRoutConsensusDuration)
        {
            armyRuntime.Routed = true;
            armyRuntime.RoutPressureSince = 0d;
            CombatDiagnostics.RecordArmyRout(
                actor.army.id,
                armyRuntime.Morale,
                casualties,
                unfavorableReports,
                reportCount,
                requiredReports);
            armyRuntime.Directive = CombatDirective.Retreat;
            armyRuntime.DirectiveExpiresAt = double.MaxValue;
            RequestArmyReplan(
                actor.army,
                clearPlans: true,
                stopMovement: true);
        }

        if (!armyRuntime.Routed) return routedBefore != armyRuntime.Routed;
        bool armySafe = armyRuntime.OutcomeReports.Count == 0;
        if (armySafe)
        {
            if (armyRuntime.SafeSince <= 0d)
            {
                armyRuntime.SafeSince = CurrentTime;
                armyRuntime.LastRoutRecoveryAt = CurrentTime;
            }
            double elapsed = Math.Max(0d, CurrentTime - armyRuntime.LastRoutRecoveryAt);
            armyRuntime.LastRoutRecoveryAt = CurrentTime;
            armyRuntime.Morale = Mathf.Clamp01(
                armyRuntime.Morale + (float)elapsed * 0.04f);
        }
        else
        {
            armyRuntime.SafeSince = 0d;
            armyRuntime.LastRoutRecoveryAt = CurrentTime;
        }
        if (armyRuntime.SafeSince > 0d &&
            CurrentTime - armyRuntime.SafeSince >= 5d &&
            armyRuntime.Morale >= TacticalCombatSettings.ArmyRecoverMorale)
        {
            armyRuntime.Routed = false;
            armyRuntime.RoutPressureSince = 0d;
            armyRuntime.Directive = CombatDirective.Hold;
            armyRuntime.DirectiveExpiresAt = CurrentTime + 1d;
            armyRuntime.SafeSince = 0d;
            RequestArmyReplan(
                actor.army,
                clearPlans: false,
                stopMovement: false);
        }
        return routedBefore != armyRuntime.Routed;
    }

    /// <summary>使军队所有成员的异步快照失效，并按指令需要终止旧动作计划。</summary>
    private static void RequestArmyReplan(
        Army army,
        bool clearPlans,
        bool stopMovement)
    {
        if (army == null) return;
        for (int i = 0; i < army.units.Count; i++)
        {
            Actor member = army.units[i];
            if (member.isRekt() ||
                !ActorStates.TryGetValue(member.getID(), out CombatActorRuntime runtime))
                continue;
            if (clearPlans) runtime.Plan = null;
            if (stopMovement)
                CombatMovementService.Clear(
                    member,
                    runtime,
                    stopMovement: true,
                    clearBearing: false);
            RequestMovementRefresh(runtime, clearBearing: false);
        }
    }

    private static void UpdateProgress(
        Actor actor,
        BaseSimObject target,
        CombatActorRuntime runtime)
    {
        double now = CurrentTime;
        if (runtime.LastProgressAt <= 0d ||
            runtime.LastProgressTargetId != target.getID() ||
            Vector2.Distance(runtime.LastProgressPosition, actor.current_position) >= 0.4f)
        {
            runtime.LastProgressAt = now;
            runtime.LastProgressPosition = actor.current_position;
            runtime.LastProgressTargetId = target.getID();
            runtime.TargetPathFailures = 0;
            return;
        }

        bool highFidelity = actor.is_visible ||
                            actor.isFavorite() ||
                            actor.GetExtend().GetPowerLevel() >= 3f;
        float timeout = highFidelity
            ? TacticalCombatSettings.NoProgressHighFidelitySeconds
            : TacticalCombatSettings.NoProgressLowFidelitySeconds;
        if (now - runtime.LastProgressAt < timeout) return;
        runtime.TargetPathFailures++;
        runtime.LastProgressAt = now;
        if (runtime.TargetPathFailures >= 3)
        {
            runtime.IgnoredTargetId = target.getID();
            runtime.IgnoreCurrentTargetUntil = now + 3d;
            runtime.Plan = null;
            actor.clearAttackTarget();
            CombatMovementService.Clear(
                actor,
                runtime,
                stopMovement: true,
                clearBearing: true);
            runtime.TargetPathFailures = 0;
        }
        RequestMovementRefresh(runtime, clearBearing: false);
    }

    private static void ImportExternalTarget(Actor actor, CombatActorRuntime runtime)
    {
        if (actor.attack_target.isRekt()) return;
        long targetId = actor.attack_target.getID();
        if (runtime.Plan?.HasEnemy == true &&
            runtime.Plan.PrimaryEnemy.Id == targetId)
            return;
        runtime.CurrentTargetId = targetId;
        runtime.ExternalTargetDirty = true;
        runtime.NextPlanAt = 0d;
        runtime.NextActionAttemptAt = 0d;
        CombatMovementService.Invalidate(runtime, clearBearing: true);
        if (runtime.IgnoredTargetId == targetId)
        {
            runtime.IgnoredTargetId = 0;
            runtime.IgnoreCurrentTargetUntil = 0d;
        }
    }

    private static void LeaveCombat(Actor actor, CombatActorRuntime runtime)
    {
        runtime.Plan = null;
        runtime.IsEngaged = false;
        runtime.CurrentTargetId = 0;
        runtime.NextActionAttemptAt = 0d;
        ResetDisplayedActivity(runtime);
        CombatMovementService.Clear(
            actor,
            runtime,
            stopMovement: true,
            clearBearing: true);
        if (!actor.attack_target.isRekt()) actor.clearAttackTarget();
        if (actor.isTask(TacticalCombatSettings.TacticalTaskId)) actor.cancelAllBeh();
    }

    /// <summary>分别解析移动状态和动作阶段，供任务栏组合展示。</summary>
    private static CombatActivityPresentation ResolveDisplayedActivity(
        Actor actor,
        CombatActorRuntime runtime,
        double now)
    {
        CombatActivityMovement movement = ResolveMovementActivity(actor, runtime);
        CombatActivityAction action = CombatActivityAction.None;
        if (runtime.ActionPresentationUntil > now)
        {
            action = ResolveActionActivity(runtime.ActiveActionUse, preparing: false);
        }
        else if (actor.attack_timer > 0f)
        {
            action = CombatActivityAction.Ready;
        }
        else if (runtime.Plan?.Action != null)
        {
            action = ResolveActionActivity(runtime.Plan.ActionUse, preparing: true);
        }
        else if (runtime.Plan?.HasEnemy == true &&
                 movement is not (CombatActivityMovement.Retreat or CombatActivityMovement.Observe))
        {
            action = CombatActivityAction.Ready;
        }
        return new CombatActivityPresentation(movement, action);
    }

    /// <summary>从实际路径订单和计划意图解析移动或站位状态。</summary>
    private static CombatActivityMovement ResolveMovementActivity(
        Actor actor,
        CombatActorRuntime runtime)
    {
        if (runtime.LostContactSince > 0d && runtime.Plan?.HasEnemy != true)
            return CombatActivityMovement.Observe;
        if (actor.is_moving || actor.isUsingPath())
        {
            return runtime.Movement.Kind switch
            {
                CombatMovementKind.Advance => CombatActivityMovement.Advance,
                CombatMovementKind.Reposition => CombatActivityMovement.Reposition,
                CombatMovementKind.Regroup => CombatActivityMovement.Regroup,
                CombatMovementKind.Retreat => CombatActivityMovement.Retreat,
                CombatMovementKind.Assist => CombatActivityMovement.Assist,
                CombatMovementKind.Protect => CombatActivityMovement.Protect,
                _ => CombatActivityMovement.Reposition
            };
        }

        return runtime.Plan?.Intent switch
        {
            CombatIntent.Hold => CombatActivityMovement.Hold,
            CombatIntent.Reposition => CombatActivityMovement.Reposition,
            CombatIntent.Assist => CombatActivityMovement.Assist,
            CombatIntent.Protect => CombatActivityMovement.Protect,
            CombatIntent.Regroup => CombatActivityMovement.Regroup,
            CombatIntent.Disengage => CombatActivityMovement.Retreat,
            CombatIntent.None => CombatActivityMovement.Observe,
            _ => CombatActivityMovement.None
        };
    }

    /// <summary>根据本轮选择的单值用途区分准备阶段和实际启动阶段。</summary>
    private static CombatActivityAction ResolveActionActivity(
        CombatActionUse use,
        bool preparing)
    {
        return use switch
        {
            CombatActionUse.Defense => preparing
                ? CombatActivityAction.PrepareDefense
                : CombatActivityAction.Defend,
            CombatActionUse.Support => preparing
                ? CombatActivityAction.PrepareSupport
                : CombatActivityAction.Support,
            CombatActionUse.Control => preparing
                ? CombatActivityAction.PrepareControl
                : CombatActivityAction.Control,
            CombatActionUse.Offense => preparing
                ? CombatActivityAction.PrepareAttack
                : CombatActivityAction.Attack,
            _ => CombatActivityAction.None
        };
    }

    /// <summary>切换展示活动时记录独立计时起点。</summary>
    private static void SetDisplayedActivity(
        CombatActorRuntime runtime,
        CombatActivityPresentation activity,
        double now)
    {
        if (runtime.DisplayedActivity == activity) return;
        runtime.DisplayedActivity = activity;
        runtime.DisplayedActivityStartedAt = now;
    }

    /// <summary>角色离开战术交战时清除仅供界面使用的活动状态。</summary>
    private static void ResetDisplayedActivity(CombatActorRuntime runtime)
    {
        runtime.ActiveActionUse = CombatActionUse.None;
        runtime.ActionPresentationUntil = 0d;
        runtime.LostContactSince = 0d;
        runtime.DisplayedActivity = default;
        runtime.DisplayedActivityStartedAt = 0d;
    }

    private static bool IsValidEnemy(
        Actor actor,
        BaseSimObject target,
        bool confirmedThreat)
    {
        return !target.isRekt() &&
               actor.canAttackTarget(
                   target,
                   pCheckForFactions: !confirmedThreat,
                   pAttackBuildings: actor.asset.can_attack_buildings);
    }

    private static BaseSimObject ResolveActionTarget(Actor actor, CombatPlan plan)
    {
        return ResolveActionTarget(actor, plan, plan.Action);
    }

    private static BaseSimObject ResolveActionTarget(
        Actor actor,
        CombatPlan plan,
        CombatActionCandidate candidate)
    {
        if (candidate?.Profile.TargetMode ==
            SkillLibV3.ActiveAbilities.ActiveAbilityTargetMode.Self)
            return actor;
        CombatantSnapshot plannedTarget = ReferenceEquals(candidate, plan.BackupAction)
            ? plan.BackupActionTarget
            : plan.ActionTarget;
        if (!plannedTarget.Object.isRekt()) return plannedTarget.Object;
        CombatActionUse use = ReferenceEquals(candidate, plan.BackupAction)
            ? plan.BackupActionUse
            : plan.ActionUse;
        if (candidate != null && use is CombatActionUse.Defense or CombatActionUse.Support)
            return actor;
        return plan.PrimaryEnemy.Object;
    }

    private static bool IsWithinActionRange(
        Actor actor,
        BaseSimObject target,
        CombatActionProfile profile)
    {
        if (target.isRekt()) return false;
        if (target == actor) return true;
        float distance = Vector2.Distance(actor.current_position, target.current_position);
        float maxRange = profile.MaxRange + target.stats[S.size];
        return distance >= profile.MinRange && distance <= maxRange;
    }

    private static float ResolveActualPower(Actor actor)
    {
        return CombatObservationService.ResolveKnownPower(actor);
    }

    private static float ResolveHealthRatio(BaseSimObject obj)
    {
        return obj.isRekt()
            ? 0f
            : Mathf.Clamp01(obj.getHealth() / Mathf.Max(1f, obj.getMaxHealth()));
    }

    private static bool CanRetreat(Actor actor)
    {
        return !actor.hasTrait("madness") &&
               !actor.hasStatusTantrum() &&
               !actor.hasStatus("possessed");
    }

    private static double ResolvePlanInterval(
        Actor actor,
        bool highFidelity,
        int revision)
    {
        float min = highFidelity ? 0.2f : 0.8f;
        float max = highFidelity ? 0.4f : 1.5f;
        unchecked
        {
            long hash = actor.getID() * 73856093L ^ revision * 19349663L;
            float roll = (hash & 0xFFFF) / 65535f;
            return Mathf.Lerp(min, max, roll);
        }
    }

    /// <summary>
    /// 为未能启动的动作提供与技能冷却无关的短退避，避免概率检定被模拟帧率放大。
    /// 退避期间角色仍可按当前计划移动和重新选位。
    /// </summary>
    private static double ResolveActionRetryDelay(Actor actor, int revision)
    {
        bool highFidelity = actor.is_visible ||
                            actor.isFavorite() ||
                            actor.GetExtend().GetPowerLevel() >= 3f;
        float min = highFidelity ? 0.18f : 0.55f;
        float max = highFidelity ? 0.32f : 0.9f;
        unchecked
        {
            long hash = actor.getID() * 83492791L ^ revision * 2971215073L;
            float roll = (hash & 0xFFFF) / 65535f;
            return Mathf.Lerp(min, max, roll);
        }
    }

    private static void RequestDecisionRefresh(CombatActorRuntime runtime)
    {
        runtime.NextPlanAt = 0d;
        runtime.TouchRevision();
        CombatDiagnostics.RecordReplan();
    }

    /// <summary>使移动订单失效并立即请求新决策；新计划提交前不会主动截断旧路径。</summary>
    private static void RequestMovementRefresh(
        CombatActorRuntime runtime,
        bool clearBearing)
    {
        CombatMovementService.Invalidate(runtime, clearBearing);
        RequestDecisionRefresh(runtime);
    }

    private static void RemoveExpiredRuntimeEntries(
        CombatActorRuntime runtime,
        double now)
    {
        CombatObservationService.RemoveExpired(runtime.Observations, now);
        RemoveExpiredThreats(runtime.IncomingThreats, now);
        if (runtime.IgnoreCurrentTargetUntil <= now)
        {
            runtime.IgnoredTargetId = 0;
            runtime.IgnoreCurrentTargetUntil = 0d;
        }
        using var staleAttackers = new ListPool<long>();
        foreach (KeyValuePair<long, double> pair in runtime.RecentAttackers)
        {
            if (now - pair.Value > TacticalCombatSettings.TacticalLocationLifetime)
                staleAttackers.Add(pair.Key);
        }
        for (int i = 0; i < staleAttackers.Count; i++)
            runtime.RecentAttackers.Remove(staleAttackers[i]);

        using var staleCooldowns = new ListPool<CombatActionKey>();
        foreach (KeyValuePair<CombatActionKey, double> pair in runtime.Cooldowns)
        {
            if (pair.Value <= now) staleCooldowns.Add(pair.Key);
        }
        for (int i = 0; i < staleCooldowns.Count; i++)
            runtime.Cooldowns.Remove(staleCooldowns[i]);
    }

    /// <summary>移除超过响应窗口或任一参与者已经失效的威胁。</summary>
    private static void RemoveExpiredThreats(
        IDictionary<CombatThreatKey, CombatThreatSignal> threats,
        double now)
    {
        using var stale = new ListPool<CombatThreatKey>();
        foreach (KeyValuePair<CombatThreatKey, CombatThreatSignal> pair in threats)
        {
            CombatThreatSignal signal = pair.Value;
            if (signal.Attacker.isRekt() ||
                signal.Victim.isRekt() ||
                now - signal.LastThreatAt > TacticalCombatSettings.ThreatLifetime)
                stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++) threats.Remove(stale[i]);
    }

    /// <summary>在没有持续威胁时缓慢恢复个人士气，避免一次受创永久改变后续所有战斗。</summary>
    private static void RecoverActorMorale(CombatActorRuntime runtime, double now)
    {
        double elapsed = runtime.LastMoraleUpdateAt <= 0d
            ? 0d
            : Math.Max(0d, now - runtime.LastMoraleUpdateAt);
        runtime.LastMoraleUpdateAt = now;
        if (runtime.RecentAttackers.Count == 0)
            runtime.Morale = Mathf.Clamp01(runtime.Morale + (float)elapsed * 0.02f);
    }

    /// <summary>定期回收长期没有任何成员参与规划的军队运行时。</summary>
    private static void CleanupStaleArmyStates(double now)
    {
        if (now < nextGlobalCleanupAt) return;
        nextGlobalCleanupAt = now + Math.Max(5d, TimeScales.SecPerYear);
        double lifetime = Math.Max(30d, TimeScales.SecPerYear * 20d);
        using var stale = new ListPool<long>();
        foreach (KeyValuePair<long, CombatArmyRuntime> pair in ArmyStates)
        {
            if (now - pair.Value.LastUpdatedAt > lifetime) stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++) ArmyStates.Remove(stale[i]);
    }

    /// <summary>
    /// 将一次真实敌对行为记录到受害者侧，并仅把外部威胁发布给受害者所属军队。
    /// </summary>
    private static void RecordIncomingThreat(
        Actor attacker,
        Actor victim,
        CombatActorRuntime victimRuntime,
        CombatObservation observation,
        float severity,
        double now)
    {
        victimRuntime.RecentAttackers[attacker.getID()] = now;
        CombatThreatSignal personal = UpsertThreatSignal(
            victimRuntime.IncomingThreats,
            attacker,
            victim,
            observation,
            severity,
            now,
            TacticalCombatSettings.PersonalThreatLimit);
        if (victim.army != null &&
            victim.kingdom != null &&
            attacker.kingdom != victim.kingdom)
        {
            UpsertThreatSignal(
                GetOrCreateArmyState(victim.army).SharedThreats,
                attacker,
                victim,
                observation,
                personal.Severity,
                now,
                TacticalCombatSettings.ArmyThreatLimit);
        }
        victimRuntime.LostContactSince = 0d;
        RequestDecisionRefresh(victimRuntime);
        CombatDiagnostics.RecordThreatSignal();
    }

    /// <summary>更新一条威胁并按最旧访问时间维持字典上限。</summary>
    private static CombatThreatSignal UpsertThreatSignal(
        IDictionary<CombatThreatKey, CombatThreatSignal> threats,
        Actor attacker,
        Actor victim,
        CombatObservation observation,
        float severity,
        double now,
        int limit)
    {
        var key = new CombatThreatKey(attacker.getID(), victim.getID());
        if (!threats.TryGetValue(key, out CombatThreatSignal signal))
        {
            signal = new CombatThreatSignal
            {
                Attacker = attacker,
                Victim = victim,
                AttackerId = attacker.getID(),
                VictimId = victim.getID()
            };
            threats.Add(key, signal);
        }

        signal.Attacker = attacker;
        signal.Victim = victim;
        signal.AttackerPosition = observation.LastPosition;
        signal.VictimPosition = victim.current_position;
        signal.AttackerHealthRatio = observation.LastHealthRatio;
        signal.AttackerPower = observation.EstimatedPower;
        signal.AttackerSize = observation.LastSize;
        signal.AttackerAirborne = observation.LastAirborne;
        signal.Confidence = observation.Confidence;
        signal.Severity = Mathf.Max(signal.Severity, Mathf.Clamp01(severity));
        signal.LastThreatAt = now;
        TrimThreats(threats, limit);
        return signal;
    }

    /// <summary>移除威胁字典中最久未更新的记录，保持运行时引用有界。</summary>
    private static void TrimThreats(
        IDictionary<CombatThreatKey, CombatThreatSignal> threats,
        int limit)
    {
        while (threats.Count > limit)
        {
            CombatThreatKey oldestKey = default;
            double oldestTime = double.MaxValue;
            foreach (KeyValuePair<CombatThreatKey, CombatThreatSignal> pair in threats)
            {
                if (pair.Value.LastThreatAt >= oldestTime) continue;
                oldestKey = pair.Key;
                oldestTime = pair.Value.LastThreatAt;
            }
            threats.Remove(oldestKey);
        }
    }

    private static void PublishObservationToArmy(
        Actor actor,
        CombatObservation observation,
        double now)
    {
        if (actor.army == null) return;
        CombatObservationService.PublishShared(
            GetOrCreateArmyState(actor.army),
            observation,
            now);
    }

    private static CombatActorRuntime GetOrCreateActorState(Actor actor)
    {
        long id = actor.getID();
        if (ActorStates.TryGetValue(id, out CombatActorRuntime runtime)) return runtime;
        runtime = new CombatActorRuntime
        {
            LastProgressAt = CurrentTime,
            LastProgressPosition = actor.current_position,
            LastMoraleUpdateAt = CurrentTime
        };
        ActorStates.Add(id, runtime);
        return runtime;
    }

    private static CombatArmyRuntime GetOrCreateArmyState(Army army)
    {
        if (ArmyStates.TryGetValue(army.id, out CombatArmyRuntime runtime)) return runtime;
        runtime = new CombatArmyRuntime
        {
            PeakMemberCount = army.units.Count,
            LastUpdatedAt = CurrentTime
        };
        Actor captain = army.getCaptain();
        if (!captain.isRekt()) runtime.LastCaptainId = captain.getID();
        ArmyStates.Add(army.id, runtime);
        return runtime;
    }

    private static double CurrentTime => World.world?.getCurWorldTime() ?? 0d;
}

/// <summary>
/// 协作模拟器在线程间传递的一次规划工作；Plan 只读快照，Commit 只在主线程运行。
/// </summary>
public sealed class CombatPlanningWorkItem
{
    private Actor actor;
    private CombatPlanningSnapshot snapshot;
    private CombatPlan plan;

    internal CombatPlanningWorkItem(Actor actor, CombatPlanningSnapshot snapshot)
    {
        this.actor = actor;
        this.snapshot = snapshot;
    }

    /// <summary>在工作线程执行纯数据规划。</summary>
    public void Plan()
    {
        plan = CombatPlanner.Plan(snapshot);
        CombatDiagnostics.RecordPlan(snapshot, plan);
    }

    /// <summary>回到主线程后按版本提交计划。</summary>
    public void Commit()
    {
        CombatWorldService.Commit(actor, snapshot, plan);
    }

    /// <summary>释放对实时对象和快照的引用，供协作运行器复用数组。</summary>
    public void Reset()
    {
        actor = null;
        snapshot = null;
        plan = null;
    }
}
