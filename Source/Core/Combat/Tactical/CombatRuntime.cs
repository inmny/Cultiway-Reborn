using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 新战斗层的集中开关和稳定调参。关键补丁失效时会整体关闭，避免新旧逻辑同时接管。
/// </summary>
public static class TacticalCombatSettings
{
    public const string TacticalTaskId = "Cultiway.TacticalCombat";
    public const int PersonalObservationLimit = 16;
    public const int ArmyObservationLimit = 64;
    public const int PersonalThreatLimit = 16;
    public const int ArmyThreatLimit = 64;
    public const float TacticalLocationLifetime = 10f;
    public const float ThreatLifetime = 6f;
    public const float LocalCombatRadius = 24f;
    public const float NearbyAssistRadius = 20f;
    public const float ArmyAssistRadius = 40f;
    public const float DormantProbeMinInterval = 2f;
    public const float DormantProbeMaxInterval = 4f;
    public const float LostContactGrace = 2f;
    public const float ActionPresentationDuration = 0.65f;
    public const float NoProgressHighFidelitySeconds = 1.5f;
    public const float NoProgressLowFidelitySeconds = 3f;
    public const float TargetSwitchImprovement = 0.25f;
    public const float NearOptimalScoreWindow = 0.15f;
    public const float RegroupEnterCohesion = 0.42f;
    public const float RegroupExitCohesion = 0.65f;
    public const float RepositionScoreImprovement = 0.75f;
    public const float ArmyRoutMorale = 0.55f;
    public const float ArmyRecoverMorale = 0.55f;
    public const float ArmyRoutLocalRatio = 0.7f;
    public const float ArmyRoutMinimumCasualtyRatio = 0.12f;
    public const float ArmyRoutReportLifetime = 2f;
    public const float ArmyRoutConsensusRatio = 0.6f;
    public const float ArmyRoutConsensusDuration = 2f;

    private static bool enabled = true;
    private static bool criticalFailure;
    private static string disabledReason = string.Empty;

    /// <summary>返回新战斗层是否完整启用。</summary>
    public static bool Enabled => enabled;

    /// <summary>返回本次运行中导致战斗层关闭的首个原因。</summary>
    public static string DisabledReason => disabledReason;

    /// <summary>由设置界面或调试工具整体切换新战斗层。</summary>
    public static void SetEnabled(bool value)
    {
        if (value && criticalFailure) return;
        if (enabled == value) return;
        enabled = value;
        if (value)
        {
            disabledReason = string.Empty;
        }
        else
        {
            CombatWorldService.ReleaseTakeovers();
        }
    }

    /// <summary>关键入口无法安装时永久关闭本次运行中的新战斗层。</summary>
    public static void DisableForCriticalFailure(string reason)
    {
        if (criticalFailure) return;
        criticalFailure = true;
        enabled = false;
        disabledReason = reason ?? string.Empty;
        CombatWorldService.ReleaseTakeovers();
        ModClass.LogError($"战术战斗系统已整体关闭: {disabledReason}");
    }
}

/// <summary>唯一标识一名攻击者对一名受害者形成的近期威胁。</summary>
internal readonly struct CombatThreatKey : IEquatable<CombatThreatKey>
{
    internal readonly long AttackerId;
    internal readonly long VictimId;

    internal CombatThreatKey(long attackerId, long victimId)
    {
        AttackerId = attackerId;
        VictimId = victimId;
    }

    public bool Equals(CombatThreatKey other)
    {
        return AttackerId == other.AttackerId && VictimId == other.VictimId;
    }

    public override bool Equals(object obj)
    {
        return obj is CombatThreatKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        return unchecked((AttackerId.GetHashCode() * 397) ^ VictimId.GetHashCode());
    }
}

/// <summary>
/// 一次受袭关系的有界运行时记录。位置与数值均取自事件发生时受害者掌握的信息。
/// </summary>
internal sealed class CombatThreatSignal
{
    internal Actor Attacker;
    internal Actor Victim;
    internal long AttackerId;
    internal long VictimId;
    internal Vector2 AttackerPosition;
    internal Vector2 VictimPosition;
    internal float AttackerHealthRatio;
    internal float AttackerPower;
    internal float AttackerSize;
    internal bool AttackerAirborne;
    internal float Confidence;
    internal float Severity;
    internal double LastThreatAt;
}

/// <summary>规划者本轮可消费的威胁及其传播来源。</summary>
internal readonly struct CombatThreatContext
{
    internal readonly CombatThreatSignal Signal;
    internal readonly CombatThreatSource Source;

    internal CombatThreatContext(CombatThreatSignal signal, CombatThreatSource source)
    {
        Signal = signal;
        Source = source;
    }
}

/// <summary>军队成员最近一次局部胜负判断。</summary>
internal sealed class CombatOutcomeReport
{
    internal float StrengthRatio;
    internal float Survival;
    internal double ReportedAt;
}

/// <summary>
/// 对某个敌人的不完整认知。该数据只在当前世界运行期存在。
/// </summary>
internal sealed class CombatObservation
{
    internal BaseSimObject TargetObject;
    internal long TargetId;
    internal Vector2 LastPosition;
    internal float LastHealthRatio;
    internal float LastSize;
    internal bool LastAirborne;
    internal double LastObservedAt;
    internal double LastLocationAt;
    internal float EstimatedPower;
    internal float Confidence;
    internal int AttackAttempts;
    internal int EffectiveHits;
    internal float EffectiveDamage;
    internal bool LastHitIneffective;

    internal void Decay(double now)
    {
        double halfLife = Math.Max(1f, TimeScales.SecPerYear * 5f);
        double elapsed = Math.Max(0d, now - LastObservedAt);
        Confidence *= Mathf.Pow(0.5f, (float)(elapsed / halfLife));
        LastObservedAt = now;
    }
}

/// <summary>
/// 单个角色的战斗运行时。它不作为 ECS 组件保存，因此不会在查询循环中产生结构变更。
/// </summary>
internal sealed class CombatActorRuntime
{
    internal readonly Dictionary<long, CombatObservation> Observations = new();
    internal readonly Dictionary<long, double> RecentAttackers = new();
    internal readonly Dictionary<CombatThreatKey, CombatThreatSignal> IncomingThreats = new();
    internal readonly Dictionary<CombatActionKey, double> Cooldowns = new();
    internal int Revision;
    internal CombatPlan Plan;
    internal double NextPlanAt;
    internal double NextActionAttemptAt;
    internal double LastProgressAt;
    internal Vector2 LastProgressPosition;
    internal long LastProgressTargetId;
    internal int TargetPathFailures;
    internal long IgnoredTargetId;
    internal double IgnoreCurrentTargetUntil;
    internal float Morale = 1f;
    internal double LastMoraleUpdateAt;
    internal long CurrentTargetId;
    internal bool ExternalTargetDirty;
    internal bool IsEngaged;
    internal readonly CombatMovementOrder Movement = new();
    internal double MovementPausedUntil;
    internal CombatActionUse ActiveActionUse;
    internal double ActionPresentationUntil;
    internal double LostContactSince;
    internal CombatActivityPresentation DisplayedActivity;
    internal double DisplayedActivityStartedAt;

    internal void TouchRevision()
    {
        unchecked
        {
            Revision++;
        }
    }
}

/// <summary>
/// 将连续多轮战术规划归并为一个稳定移动订单，避免每次评分变化都重建路径。
/// </summary>
internal sealed class CombatMovementOrder
{
    internal long TargetId;
    internal CombatMovementKind Kind;
    internal long EngagementTargetId;
    internal Vector2 EngagementBearing;
    internal bool HasEngagementBearing;
    internal WorldTile GoalTile;
    internal double GoalIssuedAt;
    internal double RetargetAfter;
    internal bool ForceRefresh;
    internal bool PendingStop;

    /// <summary>清除已提交终点；可选择同时丢弃针对当前目标建立的交战方向。</summary>
    internal void Clear(bool clearBearing)
    {
        TargetId = 0;
        Kind = CombatMovementKind.None;
        GoalTile = null;
        GoalIssuedAt = 0d;
        RetargetAfter = 0d;
        ForceRefresh = false;
        PendingStop = false;
        if (!clearBearing) return;
        EngagementTargetId = 0;
        EngagementBearing = default;
        HasEngagementBearing = false;
    }
}

/// <summary>移动订单使用的稳定类别；同类计划可以沿用同一条路径。</summary>
internal enum CombatMovementKind
{
    None,
    Advance,
    Reposition,
    Regroup,
    Retreat,
    Assist,
    Protect,
}

/// <summary>
/// 军队共享的指令、士气和降置信度情报。
/// </summary>
internal sealed class CombatArmyRuntime
{
    internal readonly Dictionary<long, CombatObservation> SharedObservations = new();
    internal readonly Dictionary<CombatThreatKey, CombatThreatSignal> SharedThreats = new();
    internal readonly Dictionary<long, CombatOutcomeReport> OutcomeReports = new();
    internal CombatDirective Directive = CombatDirective.Hold;
    internal double DirectiveExpiresAt;
    internal float Morale = 1f;
    internal int PeakMemberCount;
    internal float LastCasualtyRatio;
    internal long LastCaptainId;
    internal long RecordedLostCaptainId;
    internal double LastCaptainLossAt;
    internal bool Routed;
    internal double RoutPressureSince;
    internal double SafeSince;
    internal double LastRoutRecoveryAt;
    internal double LastUpdatedAt;
    internal double AggregateUpdatedAt = double.MinValue;
    internal double RoutEvaluatedAt = double.MinValue;
    internal double LatestSharedAwarenessAt = double.MinValue;
    internal int AliveMemberCount;
    internal int EngagedMemberCount;
}

/// <summary>
/// 负责战斗观察的创建、衰减和有界回收。
/// </summary>
internal static class CombatObservationService
{
    private const float SharedConfidenceMultiplier = 0.6f;

    internal static CombatObservation ObserveVisible(
        CombatActorRuntime runtime,
        Actor observer,
        BaseSimObject target,
        double now)
    {
        long targetId = target.getID();
        if (!runtime.Observations.TryGetValue(targetId, out CombatObservation observation))
        {
            observation = new CombatObservation
            {
                TargetObject = target,
                TargetId = targetId,
                EstimatedPower = ResolveObservedPower(observer, target),
                Confidence = ResolveInitialConfidence(observer),
                LastObservedAt = now,
                LastLocationAt = now,
                LastPosition = target.current_position,
                LastHealthRatio = ResolveHealthRatio(target),
                LastSize = target.stats[S.size],
                LastAirborne = target.isFlying()
            };
            runtime.Observations.Add(targetId, observation);
            Trim(runtime.Observations, TacticalCombatSettings.PersonalObservationLimit, targetId);
            return observation;
        }

        observation.Decay(now);
        float visiblePower = ResolveObservedPower(observer, target);
        observation.EstimatedPower = Mathf.Lerp(
            observation.EstimatedPower,
            visiblePower,
            0.2f + ResolveInitialConfidence(observer) * 0.25f);
        observation.Confidence = Mathf.Clamp01(
            Mathf.Max(observation.Confidence, ResolveInitialConfidence(observer)) + 0.03f);
        observation.LastObservedAt = now;
        observation.LastLocationAt = now;
        observation.LastPosition = target.current_position;
        observation.LastHealthRatio = ResolveHealthRatio(target);
        observation.LastSize = target.stats[S.size];
        observation.LastAirborne = target.isFlying();
        observation.TargetObject = target;
        return observation;
    }

    /// <summary>
    /// 解析角色当前可用的目标认知；可见目标会刷新实况，不可见目标只衰减并合并已有军团情报。
    /// </summary>
    internal static CombatObservation ResolveKnown(
        CombatActorRuntime personal,
        CombatArmyRuntime army,
        Actor observer,
        BaseSimObject target,
        double now,
        bool visible)
    {
        long targetId = target.getID();
        if (visible)
        {
            CombatObservation observed = ObserveVisible(personal, observer, target, now);
            PublishShared(army, observed, now);
            return observed;
        }

        personal.Observations.TryGetValue(targetId, out CombatObservation own);
        CombatObservation shared = null;
        if (army != null) army.SharedObservations.TryGetValue(targetId, out shared);
        if (own == null && shared == null)
        {
            // 外部刚指定的目标至少代表角色在这一刻获知了它，之后仍按记忆衰减。
            return ObserveVisible(personal, observer, target, now);
        }

        own?.Decay(now);
        shared?.Decay(now);
        if (own == null)
        {
            own = CopySharedObservation(shared);
            personal.Observations[targetId] = own;
            Trim(personal.Observations, TacticalCombatSettings.PersonalObservationLimit, targetId);
            return own;
        }

        if (shared != null && shared.Confidence > own.Confidence)
        {
            float sharedWeight = Mathf.Clamp01(shared.Confidence);
            own.EstimatedPower = Mathf.Lerp(
                own.EstimatedPower,
                shared.EstimatedPower,
                sharedWeight);
            own.LastHealthRatio = Mathf.Lerp(
                own.LastHealthRatio,
                shared.LastHealthRatio,
                sharedWeight);
            own.LastSize = Mathf.Lerp(own.LastSize, shared.LastSize, sharedWeight);
            own.LastAirborne = shared.LastAirborne;
            own.Confidence = shared.Confidence;
            if (shared.LastLocationAt > own.LastLocationAt)
            {
                own.LastPosition = shared.LastPosition;
                own.LastLocationAt = shared.LastLocationAt;
            }
            own.TargetObject = shared.TargetObject ?? own.TargetObject;
        }
        return own;
    }

    internal static CombatObservation RecordAttempt(
        CombatActorRuntime observerRuntime,
        Actor observer,
        BaseSimObject target,
        double now)
    {
        CombatObservation observation = ObserveVisible(observerRuntime, observer, target, now);
        observation.AttackAttempts++;
        observation.Confidence = Mathf.Clamp01(observation.Confidence + 0.02f);
        return observation;
    }

    internal static CombatObservation RecordOutcome(
        CombatActorRuntime attackerRuntime,
        Actor attacker,
        BaseSimObject target,
        float damage,
        bool ineffective,
        double now)
    {
        CombatObservation observation = ObserveVisible(attackerRuntime, attacker, target, now);
        observation.LastHitIneffective = ineffective;
        if (damage > 0f)
        {
            observation.EffectiveHits++;
            observation.EffectiveDamage += damage;
            observation.EstimatedPower *= 0.98f;
        }
        else
        {
            observation.EstimatedPower *= ineffective ? 1.12f : 1.04f;
        }
        observation.Confidence = Mathf.Clamp01(observation.Confidence + (ineffective ? 0.08f : 0.05f));
        return observation;
    }

    internal static void PublishShared(CombatArmyRuntime army, CombatObservation source, double now)
    {
        if (army == null || source == null) return;
        if (!army.SharedObservations.TryGetValue(source.TargetId, out CombatObservation shared))
        {
            shared = new CombatObservation
            {
                TargetId = source.TargetId,
                TargetObject = source.TargetObject
            };
            army.SharedObservations.Add(source.TargetId, shared);
        }

        shared.TargetObject = source.TargetObject;
        shared.LastObservedAt = now;
        shared.LastLocationAt = source.LastLocationAt;
        shared.LastPosition = source.LastPosition;
        shared.LastHealthRatio = source.LastHealthRatio;
        shared.LastSize = source.LastSize;
        shared.LastAirborne = source.LastAirborne;
        shared.EstimatedPower = source.EstimatedPower;
        shared.Confidence = Mathf.Max(shared.Confidence, source.Confidence * SharedConfidenceMultiplier);
        shared.AttackAttempts = Math.Max(shared.AttackAttempts, source.AttackAttempts);
        shared.EffectiveHits = Math.Max(shared.EffectiveHits, source.EffectiveHits);
        shared.EffectiveDamage = Mathf.Max(shared.EffectiveDamage, source.EffectiveDamage);
        shared.LastHitIneffective = source.LastHitIneffective;
        Trim(army.SharedObservations, TacticalCombatSettings.ArmyObservationLimit, source.TargetId);
    }

    /// <summary>将军团情报复制为个人记忆，避免后续个人衰减直接修改共享对象。</summary>
    private static CombatObservation CopySharedObservation(CombatObservation shared)
    {
        return new CombatObservation
        {
            TargetObject = shared.TargetObject,
            TargetId = shared.TargetId,
            LastPosition = shared.LastPosition,
            LastHealthRatio = shared.LastHealthRatio,
            LastSize = shared.LastSize,
            LastAirborne = shared.LastAirborne,
            LastObservedAt = shared.LastObservedAt,
            LastLocationAt = shared.LastLocationAt,
            EstimatedPower = shared.EstimatedPower,
            Confidence = shared.Confidence,
            AttackAttempts = shared.AttackAttempts,
            EffectiveHits = shared.EffectiveHits,
            EffectiveDamage = shared.EffectiveDamage,
            LastHitIneffective = shared.LastHitIneffective
        };
    }

    internal static void RemoveExpired(
        Dictionary<long, CombatObservation> observations,
        double now)
    {
        if (observations.Count == 0) return;
        double lifetime = TimeScales.SecPerYear * 20d;
        using var stale = new ListPool<long>();
        foreach (KeyValuePair<long, CombatObservation> pair in observations)
        {
            if (now - pair.Value.LastObservedAt > lifetime) stale.Add(pair.Key);
        }
        for (int i = 0; i < stale.Count; i++) observations.Remove(stale[i]);
    }

    /// <summary>
    /// 返回角色对自身或明确友军可直接掌握的外显战力基线。
    /// </summary>
    internal static float ResolveKnownPower(Actor actor)
    {
        return ResolveVisiblePower(actor);
    }

    /// <summary>
    /// 根据观察者智力给外显战力加入稳定误差；同一观察关系不会在每次规划时随机跳变。
    /// </summary>
    private static float ResolveObservedPower(Actor observer, BaseSimObject target)
    {
        float visiblePower = ResolveVisiblePower(target);
        float intelligence = Mathf.Clamp01(observer.intelligence / 100f);
        float error = Mathf.Lerp(0.45f, 0.08f, intelligence);
        float noise = (StableRoll(observer.getID(), target.getID()) * 2f - 1f) * error;
        return Mathf.Max(0.1f, visiblePower * (1f + noise));
    }

    /// <summary>
    /// 从境界与基础战斗表现估算战力。敌方读取该基线后仍会叠加观察者相关的稳定误差。
    /// </summary>
    private static float ResolveVisiblePower(BaseSimObject target)
    {
        float healthFactor = Mathf.Pow(
            Mathf.Max(1f, target.getMaxHealth()) / 100f,
            0.25f);
        float damageFactor = Mathf.Sqrt(
            Mathf.Max(1f, target.stats[S.damage]) / 10f);
        if (!target.isActor())
            return Mathf.Max(0.1f, healthFactor * damageFactor);

        Actor actor = target.a;
        float realm = actor.GetExtend().GetPowerLevel();
        float body = 1f + Mathf.Clamp(actor.stats[S.size], 0f, 12f) * 0.05f;
        float equipment = actor.hasWeapon() ? 1.1f : 1f;
        float tempo = Mathf.Sqrt(Mathf.Clamp(actor.stats[S.attack_speed], 0.25f, 4f));
        float reach = 1f + Mathf.Clamp(actor.getAttackRange(), 0f, 20f) * 0.015f;
        return Mathf.Max(
            0.1f,
            Mathf.Pow(2f, realm) *
            healthFactor *
            damageFactor *
            body *
            equipment *
            tempo *
            reach);
    }

    /// <summary>为观察关系生成可复现的均匀随机数。</summary>
    private static float StableRoll(long observerId, long targetId)
    {
        unchecked
        {
            ulong value = (ulong)observerId;
            value ^= (ulong)targetId * 0x9E3779B185EBCA87UL;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (value & 0xFFFFFFUL) / (float)0x1000000UL;
        }
    }

    private static float ResolveInitialConfidence(Actor observer)
    {
        float intelligence = Mathf.Max(0f, observer.intelligence);
        return Mathf.Clamp(0.2f + intelligence / (intelligence + 80f) * 0.55f, 0.2f, 0.75f);
    }

    private static float ResolveHealthRatio(BaseSimObject target)
    {
        return target.isRekt()
            ? 0f
            : Mathf.Clamp01(target.getHealth() / Mathf.Max(1f, target.getMaxHealth()));
    }

    /// <summary>
    /// 回收最旧的观察记录，并保证本次刚刷新记录不会因同一模拟时刻的并列而被立即移除。
    /// </summary>
    private static void Trim(
        Dictionary<long, CombatObservation> observations,
        int limit,
        long retainedKey)
    {
        while (observations.Count > limit)
        {
            long oldestKey = 0;
            double oldestTime = double.MaxValue;
            bool found = false;
            foreach (KeyValuePair<long, CombatObservation> pair in observations)
            {
                if (pair.Key == retainedKey) continue;
                if (found &&
                    (pair.Value.LastObservedAt > oldestTime ||
                     pair.Value.LastObservedAt == oldestTime && pair.Key >= oldestKey))
                    continue;
                found = true;
                oldestTime = pair.Value.LastObservedAt;
                oldestKey = pair.Key;
            }
            if (!found) return;
            observations.Remove(oldestKey);
        }
    }
}
