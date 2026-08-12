using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Impacts;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 只读取不可变快照的战斗规划器。该类型不得访问 World、Actor 或 ECS 实体的实时状态。
/// </summary>
public static class CombatPlanner
{
    private const float DominantRatio = 1.6f;
    private const float FavorableRatio = 1.15f;
    private const float EvenRatio = 0.85f;
    private const float UnfavorableRatio = 0.55f;

    /// <summary>为一个角色生成目标、动作、站位和意图相互一致的完整计划。</summary>
    public static CombatPlan Plan(CombatPlanningSnapshot snapshot)
    {
        var plan = new CombatPlan
        {
            Revision = snapshot.Revision,
            Role = ResolveRole(snapshot.Actions),
            Outcome = EstimateOutcome(snapshot)
        };
        if (!TrySelectEnemy(snapshot, plan.Outcome, out CombatantSnapshot enemy, out float targetScore))
            return plan;

        plan.HasEnemy = true;
        plan.PrimaryEnemy = enemy;
        plan.TargetScore = targetScore;
        plan.Intent = ResolveIntent(snapshot, plan.Outcome, enemy, plan.Role);
        if (plan.Intent is CombatIntent.Assist or CombatIntent.Protect)
            plan.AssistedAllyId = enemy.ThreatenedAllyId;
        SelectActions(snapshot, plan);
        plan.PositioningProfile = ResolvePositioningProfile(snapshot, plan);
        SelectActionTargets(snapshot, plan);
        SelectPosition(snapshot, plan);
        return plan;
    }

    /// <summary>估算局部战斗群的强度比、存活率和情报置信度。</summary>
    public static CombatOutcomeEstimate EstimateOutcome(CombatPlanningSnapshot snapshot)
    {
        float friendly = snapshot.SelfPower *
                         Mathf.Lerp(0.45f, 1f, snapshot.HealthRatio) *
                         Mathf.Lerp(0.55f, 1f, snapshot.Morale);
        for (int i = 0; i < snapshot.Allies.Length; i++)
        {
            CombatantSnapshot ally = snapshot.Allies[i];
            friendly += ally.EstimatedPower * Mathf.Lerp(0.35f, 1f, ally.HealthRatio);
        }

        float hostile = 0f;
        float confidence = 0f;
        for (int i = 0; i < snapshot.Enemies.Length; i++)
        {
            CombatantSnapshot enemy = snapshot.Enemies[i];
            float uncertainty = Mathf.Lerp(1.3f, 1f, enemy.Confidence);
            hostile += enemy.EstimatedPower *
                       Mathf.Lerp(0.4f, 1f, enemy.HealthRatio) *
                       uncertainty;
            confidence += enemy.Confidence;
        }

        hostile = Mathf.Max(0.01f, hostile);
        float resourceFactor = 0.7f + Mathf.Max(snapshot.StaminaRatio, snapshot.ManaRatio) * 0.3f;
        float ratio = friendly * resourceFactor / hostile;
        float survival = Mathf.Clamp01(
            ratio / (ratio + 1f) *
            Mathf.Lerp(0.45f, 1.15f, snapshot.HealthRatio) *
            Mathf.Lerp(0.6f, 1.1f, snapshot.Morale));
        confidence = snapshot.Enemies.Length == 0
            ? 0f
            : confidence / snapshot.Enemies.Length;
        return new CombatOutcomeEstimate(ratio, survival, confidence);
    }

    private static bool TrySelectEnemy(
        CombatPlanningSnapshot snapshot,
        CombatOutcomeEstimate outcome,
        out CombatantSnapshot selected,
        out float selectedScore)
    {
        selected = default;
        selectedScore = float.MinValue;
        CombatantSnapshot current = default;
        float currentScore = float.MinValue;
        bool hasCurrent = false;
        for (int i = 0; i < snapshot.Enemies.Length; i++)
        {
            CombatantSnapshot enemy = snapshot.Enemies[i];
            float distance = Vector2.Distance(snapshot.Position, enemy.Position);
            float vulnerability = 1f - enemy.HealthRatio;
            float relativePower = snapshot.SelfPower /
                                  Mathf.Max(0.01f, enemy.EstimatedPower);
            float score =
                Mathf.Clamp(relativePower, 0.1f, 4f) * 0.75f +
                vulnerability * 1.1f -
                distance * 0.025f +
                enemy.Confidence * 0.15f;
            if (enemy.IsRecentAttacker) score += 1.35f;
            if (enemy.IsAttackingPlanner) score += 0.85f;
            if (enemy.ThreatenedAllyId != 0 &&
                enemy.ThreatenedAllyId != snapshot.ActorId)
            {
                float sourcePriority = enemy.ThreatSource switch
                {
                    CombatThreatSource.Group => 0.9f,
                    CombatThreatSource.NearbyAlly => 0.75f,
                    _ => 1f
                };
                score += 1.15f + enemy.ThreatSeverity * sourcePriority * 1.4f;
            }
            if (enemy.IsAirborne && !CanReachAirTarget(snapshot.Actions)) score -= 2f;
            if (outcome.StrengthRatio < EvenRatio)
            {
                score += vulnerability * 0.7f;
                score -= Mathf.Max(0f, 1f - relativePower) * 0.8f;
            }

            if (enemy.Id == snapshot.CurrentTargetId)
            {
                current = enemy;
                currentScore = score;
                hasCurrent = true;
            }
            if (score <= selectedScore) continue;
            selected = enemy;
            selectedScore = score;
        }

        if (selectedScore == float.MinValue) return false;
        if (hasCurrent &&
            selected.Id != current.Id &&
            selectedScore - currentScore <
            Mathf.Max(0.25f, Mathf.Abs(currentScore) * TacticalCombatSettings.TargetSwitchImprovement))
        {
            selected = current;
            selectedScore = currentScore;
        }
        return true;
    }

    private static bool CanReachAirTarget(IReadOnlyList<CombatActionCandidate> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            CombatActionProfile profile = actions[i].Profile;
            if (profile.MaxRange > 2f &&
                (profile.HasPurpose(CombatActionPurpose.Offense) ||
                 profile.HasPurpose(CombatActionPurpose.Control)))
                return true;
        }
        return false;
    }

    private static CombatIntent ResolveIntent(
        CombatPlanningSnapshot snapshot,
        CombatOutcomeEstimate outcome,
        CombatantSnapshot enemy,
        CombatRole role)
    {
        if (snapshot.Directive == CombatDirective.Retreat || snapshot.GroupRouted)
            return snapshot.CanRetreat ? CombatIntent.Disengage : CombatIntent.Reposition;

        if (enemy.ThreatenedAllyId != 0 &&
            enemy.ThreatenedAllyId != snapshot.ActorId)
        {
            return HasProtectiveAction(snapshot.Actions)
                ? CombatIntent.Protect
                : CombatIntent.Assist;
        }

        bool woundedAlly = false;
        for (int i = 0; i < snapshot.Allies.Length; i++)
        {
            if (snapshot.Allies[i].HealthRatio >= 0.5f) continue;
            woundedAlly = true;
            break;
        }
        if (snapshot.Directive == CombatDirective.Protect && woundedAlly)
            return CombatIntent.Protect;
        bool needsRegroup = snapshot.Allies.Length > 0 &&
                            (snapshot.CurrentIntent == CombatIntent.Regroup
                                ? snapshot.FormationCohesion < TacticalCombatSettings.RegroupExitCohesion
                                : snapshot.FormationCohesion < TacticalCombatSettings.RegroupEnterCohesion) &&
                            !enemy.IsRecentAttacker &&
                            !enemy.IsAttackingPlanner;
        if (!HasHostileAction(snapshot.Actions))
            return needsRegroup ? CombatIntent.Regroup : CombatIntent.Reposition;

        float aggression = Mathf.Clamp01(snapshot.Aggression);
        float effectiveRatio = outcome.StrengthRatio * Mathf.Lerp(0.9f, 1.12f, aggression);
        float retreatSurvival = Mathf.Clamp(
            0.22f + snapshot.Rationality * 0.08f - aggression * 0.08f,
            0.08f,
            0.34f);
        if (effectiveRatio < UnfavorableRatio)
        {
            if (snapshot.CanRetreat &&
                (outcome.Survival < retreatSurvival || effectiveRatio < 0.45f))
                return CombatIntent.Disengage;
            if (needsRegroup) return CombatIntent.Regroup;
            return IsBacklineRole(role)
                ? CombatIntent.Reposition
                : CombatIntent.Hold;
        }
        if (effectiveRatio < EvenRatio)
        {
            if (needsRegroup) return CombatIntent.Regroup;
            if (IsBacklineRole(role)) return CombatIntent.Reposition;
            return CombatIntent.Hold;
        }
        if (needsRegroup && effectiveRatio < DominantRatio)
            return CombatIntent.Regroup;
        if (snapshot.HealthRatio < 0.35f && outcome.StrengthRatio < FavorableRatio)
            return CombatIntent.Reposition;
        if (snapshot.Directive == CombatDirective.Hold)
            return CombatIntent.Hold;
        return CombatIntent.Engage;
    }

    /// <summary>判断角色是否拥有能直接围绕友军执行护卫的动作。</summary>
    private static bool HasProtectiveAction(IReadOnlyList<CombatActionCandidate> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            CombatActionProfile profile = actions[i].Profile;
            if (profile.HasPurpose(CombatActionPurpose.Defense) ||
                profile.HasPurpose(CombatActionPurpose.Barrier) ||
                profile.HasPurpose(CombatActionPurpose.Support))
                return true;
        }
        return false;
    }

    /// <summary>判断职责是否应在局部劣势时主动维持距离，而不是继续顶向敌方。</summary>
    private static bool IsBacklineRole(CombatRole role)
    {
        return role is CombatRole.Ranged
            or CombatRole.Skirmisher
            or CombatRole.Controller
            or CombatRole.Support;
    }

    /// <summary>判断本轮是否至少有一个能直接压制敌人的动作。</summary>
    private static bool HasHostileAction(IReadOnlyList<CombatActionCandidate> actions)
    {
        for (int i = 0; i < actions.Count; i++)
        {
            CombatActionProfile profile = actions[i].Profile;
            if (profile.HasPurpose(CombatActionPurpose.Offense) ||
                profile.HasPurpose(CombatActionPurpose.Control))
                return true;
        }
        return false;
    }

    private static CombatRole ResolveRole(IReadOnlyList<CombatActionCandidate> actions)
    {
        float offense = 0f;
        float defense = 0f;
        float support = 0f;
        float control = 0f;
        float range = 0f;
        bool mobility = false;
        for (int i = 0; i < actions.Count; i++)
        {
            CombatActionProfile profile = actions[i].Profile;
            if (profile.HasPurpose(CombatActionPurpose.Offense))
                offense = Mathf.Max(offense, profile.Power);
            if (profile.HasPurpose(CombatActionPurpose.Defense))
                defense = Mathf.Max(defense, profile.Utility + profile.Power);
            if (profile.HasPurpose(CombatActionPurpose.Support))
                support = Mathf.Max(support, profile.Utility);
            if (profile.HasPurpose(CombatActionPurpose.Control))
                control = Mathf.Max(control, profile.Control);
            if (profile.HasPurpose(CombatActionPurpose.Mobility)) mobility = true;
            if (profile.HasPurpose(CombatActionPurpose.Offense) ||
                profile.HasPurpose(CombatActionPurpose.Control))
                range = Mathf.Max(range, profile.MaxRange);
        }

        if (support + defense > offense * 1.1f) return CombatRole.Support;
        if (control > offense * 0.65f) return CombatRole.Controller;
        if (range > 6f && mobility) return CombatRole.Skirmisher;
        return range > 6f ? CombatRole.Ranged : CombatRole.Melee;
    }

    private static void SelectActions(CombatPlanningSnapshot snapshot, CombatPlan plan)
    {
        if (snapshot.Actions.Length == 0) return;
        var scored = new List<ScoredAction>(snapshot.Actions.Length * 2);
        float enemyDistance = Vector2.Distance(snapshot.Position, plan.PrimaryEnemy.Position);
        int reserveActions = ResolveResourceReserve(plan.Outcome.StrengthRatio);
        for (int i = 0; i < snapshot.Actions.Length; i++)
        {
            CombatActionCandidate candidate = snapshot.Actions[i];
            if (!candidate.IsReady) continue;
            AddScoredUse(scored, snapshot, plan, candidate, CombatActionUse.Offense, enemyDistance, reserveActions);
            AddScoredUse(scored, snapshot, plan, candidate, CombatActionUse.Control, enemyDistance, reserveActions);
            AddScoredUse(scored, snapshot, plan, candidate, CombatActionUse.Defense, enemyDistance, reserveActions);
            AddScoredUse(scored, snapshot, plan, candidate, CombatActionUse.Support, enemyDistance, reserveActions);
            AddScoredUse(scored, snapshot, plan, candidate, CombatActionUse.Mobility, enemyDistance, reserveActions);
        }
        if (scored.Count == 0) return;

        scored.Sort((left, right) => right.Score.CompareTo(left.Score));
        float best = scored[0].Score;
        float threshold = best * (1f - TacticalCombatSettings.NearOptimalScoreWindow);
        float total = 0f;
        int nearOptimalCount = 0;
        while (nearOptimalCount < scored.Count && scored[nearOptimalCount].Score >= threshold)
        {
            total += scored[nearOptimalCount].Score;
            nearOptimalCount++;
        }

        int selectedIndex = -1;
        if (snapshot.CurrentActionKey.HasValue)
        {
            CombatActionKey currentKey = snapshot.CurrentActionKey.Value;
            for (int i = 0; i < nearOptimalCount; i++)
            {
                if (scored[i].Candidate.Key != currentKey ||
                    scored[i].Use != snapshot.CurrentActionUse)
                    continue;
                selectedIndex = i;
                break;
            }
        }
        if (selectedIndex < 0)
        {
            float roll = StableRoll(snapshot.ActorId, snapshot.Revision) * total;
            selectedIndex = 0;
            for (int i = 0; i < nearOptimalCount; i++)
            {
                roll -= scored[i].Score;
                if (roll > 0f) continue;
                selectedIndex = i;
                break;
            }
        }

        plan.Action = scored[selectedIndex].Candidate;
        plan.ActionUse = scored[selectedIndex].Use;
        plan.ActionScore = scored[selectedIndex].Score;
        for (int i = 0; i < scored.Count; i++)
        {
            if (i == selectedIndex ||
                scored[i].Candidate.Key == plan.Action.Key)
                continue;
            plan.BackupAction = scored[i].Candidate;
            plan.BackupActionUse = scored[i].Use;
            break;
        }
    }

    /// <summary>仅在动作确实具备指定用途时，将该用途的情境评分加入候选池。</summary>
    private static void AddScoredUse(
        ICollection<ScoredAction> output,
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        CombatActionCandidate candidate,
        CombatActionUse use,
        float enemyDistance,
        int reserveActions)
    {
        if (!SupportsUse(candidate.Profile, use)) return;
        float distance = ResolveActionDistance(
            snapshot,
            plan,
            candidate,
            candidate.Profile,
            use,
            enemyDistance,
            out Vector2 targetPosition,
            out float targetSize);
        float score = ScoreAction(
            snapshot,
            plan,
            candidate,
            use,
            distance,
            targetPosition,
            targetSize,
            reserveActions);
        if (score > 0f) output.Add(new ScoredAction(candidate, use, score));
    }

    /// <summary>将单值用途映射回动作公开的能力标记。</summary>
    private static bool SupportsUse(CombatActionProfile profile, CombatActionUse use)
    {
        return use switch
        {
            CombatActionUse.Offense => profile.HasPurpose(CombatActionPurpose.Offense),
            CombatActionUse.Defense => profile.HasPurpose(CombatActionPurpose.Defense) ||
                                       profile.HasPurpose(CombatActionPurpose.Barrier),
            CombatActionUse.Support => profile.HasPurpose(CombatActionPurpose.Support),
            CombatActionUse.Control => profile.HasPurpose(CombatActionPurpose.Control),
            CombatActionUse.Mobility => profile.HasPurpose(CombatActionPurpose.Mobility),
            _ => false
        };
    }

    /// <summary>防御和支援按友军距离评分，其余用途按敌方距离评分。</summary>
    private static float ResolveActionDistance(
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        CombatActionCandidate candidate,
        CombatActionProfile profile,
        CombatActionUse use,
        float enemyDistance,
        out Vector2 targetPosition,
        out float targetSize)
    {
        if (profile.TargetMode == ActiveAbilityTargetMode.Self)
        {
            targetPosition = snapshot.Position;
            targetSize = 0f;
            return 0f;
        }
        if (candidate.PreferredTargetId == snapshot.ActorId)
        {
            targetPosition = snapshot.Position;
            targetSize = 0f;
            return 0f;
        }
        if (use is not (CombatActionUse.Defense or CombatActionUse.Support))
        {
            targetPosition = plan.PrimaryEnemy.Position;
            targetSize = plan.PrimaryEnemy.Size;
            return enemyDistance;
        }

        if (TryResolveFriendlyActionTarget(snapshot, plan, candidate, out CombatantSnapshot ally))
        {
            targetPosition = ally.Position;
            targetSize = ally.Size;
            return Vector2.Distance(snapshot.Position, targetPosition);
        }

        targetPosition = snapshot.Position;
        targetSize = 0f;
        return 0f;
    }

    private static float ScoreAction(
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        CombatActionCandidate candidate,
        CombatActionUse use,
        float distance,
        Vector2 targetPosition,
        float targetSize,
        int reserveActions)
    {
        CombatActionProfile profile = candidate.Profile;
        bool inRange = distance >= profile.MinRange &&
                       distance <= profile.MaxRange + targetSize;
        float score = profile.BaseWeight * 0.08f +
                      profile.Reliability * 0.5f;

        switch (use)
        {
            case CombatActionUse.Offense:
                score += profile.Power *
                         Mathf.Sqrt(profile.ExpectedTargets) *
                         Mathf.Lerp(0.65f, 1.25f, 1f - plan.PrimaryEnemy.HealthRatio);
                break;
            case CombatActionUse.Control:
                float controlNeed = plan.Outcome.StrengthRatio < FavorableRatio ? 1.35f : 0.85f;
                if (plan.Intent is CombatIntent.Assist or CombatIntent.Protect) controlNeed += 0.9f;
                if (plan.PrimaryEnemy.ThreatSeverity > 0f)
                    controlNeed += plan.PrimaryEnemy.ThreatSeverity * 0.5f;
                score += profile.Control * controlNeed;
                break;
            case CombatActionUse.Defense:
                float defenseNeed = Mathf.Lerp(0.2f, 1.5f, 1f - snapshot.HealthRatio);
                if (plan.Intent == CombatIntent.Protect) defenseNeed += 1.4f;
                else if (plan.PrimaryEnemy.IsRecentAttacker) defenseNeed += 0.55f;
                score += (profile.Utility + profile.Power) * defenseNeed;
                break;
            case CombatActionUse.Support:
                float supportNeed = ResolveSupportNeed(snapshot);
                if (plan.Intent is CombatIntent.Assist or CombatIntent.Protect) supportNeed += 1.25f;
                score += profile.Utility * supportNeed;
                break;
            case CombatActionUse.Mobility:
                if (profile.HasPurpose(CombatActionPurpose.Advance))
                {
                    if (plan.Intent is CombatIntent.Reposition or CombatIntent.Regroup or CombatIntent.Disengage)
                        return 0f;
                    score += distance > profile.PreferredRange ? 2.5f : 0.25f;
                }
                else if (profile.HasPurpose(CombatActionPurpose.Escape))
                {
                    score += plan.Intent is CombatIntent.Reposition or CombatIntent.Regroup or CombatIntent.Disengage
                        ? 2.8f
                        : 0.75f;
                }
                else
                {
                    score += 0.25f;
                }
                break;
        }

        if (profile.HasPurpose(CombatActionPurpose.Barrier) ||
            profile.HasPurpose(CombatActionPurpose.Field))
        {
            score *= HasEquivalentPersistentEffect(snapshot, profile, targetPosition)
                ? 0.2f
                : 1.15f;
        }
        if (RequiresClearShot(profile) &&
            !plan.PrimaryEnemy.HasLineOfFire)
            score *= 0.12f;

        if (!inRange)
        {
            float rangeError = distance < profile.MinRange
                ? profile.MinRange - distance
                : distance - profile.MaxRange;
            score /= 1f + rangeError * 0.08f;
        }

        float availableResource = profile.AvailableResourceRatio ??
                                   Mathf.Max(snapshot.StaminaRatio, snapshot.ManaRatio);
        float reserve = reserveActions * Mathf.Min(0.18f, Mathf.Max(0.08f, profile.ResourceCost));
        if (profile.ResourceCost > availableResource) return 0f;
        bool criticalReserveBypass = profile.IgnoreResourceReserveWhenCritical &&
                                     snapshot.HealthRatio < 0.35f;
        if (!criticalReserveBypass && availableResource - profile.ResourceCost < reserve)
            score *= plan.Outcome.StrengthRatio >= FavorableRatio ? 0.2f : 0.7f;

        return Mathf.Max(0f, score);
    }

    private static int ResolveResourceReserve(float strengthRatio)
    {
        if (strengthRatio >= DominantRatio) return 3;
        if (strengthRatio >= FavorableRatio) return 2;
        if (strengthRatio >= EvenRatio) return 1;
        return 0;
    }

    private static float ResolveSupportNeed(CombatPlanningSnapshot snapshot)
    {
        float need = 1f - snapshot.HealthRatio;
        for (int i = 0; i < snapshot.Allies.Length; i++)
        {
            need = Mathf.Max(need, 1f - snapshot.Allies[i].HealthRatio);
        }
        return 0.25f + need * 1.5f;
    }

    private static void SelectActionTargets(CombatPlanningSnapshot snapshot, CombatPlan plan)
    {
        plan.ActionTarget = ResolveActionTarget(snapshot, plan, plan.Action, plan.ActionUse);
        plan.BackupActionTarget = ResolveActionTarget(
            snapshot,
            plan,
            plan.BackupAction,
            plan.BackupActionUse);
    }

    /// <summary>
    /// 选择长期站位所依据的动作画像。当前动作进入冷却时仍保留它的距离，避免远程角色突然按近战距离移动。
    /// </summary>
    private static CombatActionProfile? ResolvePositioningProfile(
        CombatPlanningSnapshot snapshot,
        CombatPlan plan)
    {
        if (plan.Action != null) return plan.Action.Profile;
        if (snapshot.CurrentActionKey.HasValue)
        {
            CombatActionKey currentKey = snapshot.CurrentActionKey.Value;
            for (int i = 0; i < snapshot.Actions.Length; i++)
            {
                CombatActionCandidate candidate = snapshot.Actions[i];
                if (candidate.Key == currentKey) return candidate.Profile;
            }
        }

        CombatActionProfile? best = null;
        float bestScore = float.MinValue;
        for (int i = 0; i < snapshot.Actions.Length; i++)
        {
            CombatActionProfile profile = snapshot.Actions[i].Profile;
            if (!profile.HasPurpose(CombatActionPurpose.Offense) &&
                !profile.HasPurpose(CombatActionPurpose.Control))
                continue;
            float score = profile.Power + profile.Control + profile.BaseWeight * 0.1f;
            if (score <= bestScore) continue;
            best = profile;
            bestScore = score;
        }
        return best;
    }

    private static CombatantSnapshot ResolveActionTarget(
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        CombatActionCandidate action,
        CombatActionUse use)
    {
        if (action == null) return plan.PrimaryEnemy;
        CombatActionProfile profile = action.Profile;
        if (profile.TargetMode == ActiveAbilityTargetMode.Self)
            return default;
        if (action.PreferredTargetId == snapshot.ActorId)
            return default;
        if (use is CombatActionUse.Defense or CombatActionUse.Support)
            return TryResolveFriendlyActionTarget(snapshot, plan, action, out CombatantSnapshot ally)
                ? ally
                : default;
        return plan.PrimaryEnemy;
    }

    /// <summary>优先选择本轮受援者，否则选择比施法者伤势更重的最近友军。</summary>
    private static bool TryResolveFriendlyActionTarget(
        CombatPlanningSnapshot snapshot,
        CombatPlan plan,
        CombatActionCandidate action,
        out CombatantSnapshot target)
    {
        float lowestHealth = snapshot.HealthRatio;
        target = default;
        bool found = false;
        for (int i = 0; i < snapshot.Allies.Length; i++)
        {
            CombatantSnapshot ally = snapshot.Allies[i];
            if (action?.PreferredTargetId != 0 && ally.Id == action.PreferredTargetId)
            {
                target = ally;
                return true;
            }
            if (plan.AssistedAllyId != 0 && ally.Id == plan.AssistedAllyId)
            {
                target = ally;
                return true;
            }
            if (plan.AssistedAllyId != 0 || ally.HealthRatio >= lowestHealth) continue;
            lowestHealth = ally.HealthRatio;
            target = ally;
            found = true;
        }
        return found;
    }

    private static void SelectPosition(CombatPlanningSnapshot snapshot, CombatPlan plan)
    {
        if (snapshot.Positions.Length == 0) return;
        CombatActionProfile? positioningProfile = plan.PositioningProfile;
        float preferredRange = positioningProfile?.PreferredRange ?? 1f;
        float currentDistance = Vector2.Distance(snapshot.Position, plan.PrimaryEnemy.Position);
        bool currentUsable = positioningProfile.HasValue &&
                             currentDistance >= positioningProfile.Value.MinRange &&
                              currentDistance <= positioningProfile.Value.MaxRange + plan.PrimaryEnemy.Size &&
                              (!RequiresClearShot(positioningProfile.Value) ||
                               plan.PrimaryEnemy.HasLineOfFire);

        float bestScore = float.MinValue;
        float currentScore = float.MinValue;
        CombatPositionCandidate best = default;
        for (int i = 0; i < snapshot.Positions.Length; i++)
        {
            CombatPositionCandidate position = snapshot.Positions[i];
            float distanceToPrimary = Vector2.Distance(
                position.Position,
                plan.PrimaryEnemy.Position);
            float rangeError = Mathf.Abs(distanceToPrimary - preferredRange);
            float powerScale = Mathf.Max(0.1f, snapshot.SelfPower);
            float enemyPressure = position.EnemyPressure / powerScale;
            float allySupport = position.AllySupport / powerScale;
            float pressureWeight = plan.Intent is CombatIntent.Engage or CombatIntent.Hold
                ? 0.2f
                : 0.8f;
            float score = -rangeError * 0.3f -
                          enemyPressure * pressureWeight +
                          allySupport * 0.2f -
                          position.Crowding * 1.25f;
            if (RequiresClearShot(positioningProfile) &&
                !position.HasLineOfFire(plan.PrimaryEnemy.SnapshotIndex))
                score -= 3f;

            switch (plan.Intent)
            {
                case CombatIntent.Disengage:
                    score += distanceToPrimary * 0.75f;
                    score += allySupport * 0.8f;
                    score += position.Role switch
                    {
                        CombatPositionRole.Safe => 4f,
                        CombatPositionRole.AllyRally => 3.5f,
                        CombatPositionRole.CaptainRally => 3f,
                        CombatPositionRole.CityRetreat => snapshot.GroupRouted ? 5f : 2f,
                        _ => 0f
                    };
                    break;
                case CombatIntent.Regroup:
                    score += allySupport * 1.25f;
                    score -= enemyPressure * 0.6f;
                    if (position.Role == CombatPositionRole.AllyRally) score += 3f;
                    if (position.Role == CombatPositionRole.CaptainRally) score += 2.5f;
                    break;
                case CombatIntent.Reposition:
                    score -= enemyPressure * 0.9f;
                    break;
                case CombatIntent.Assist:
                    score += allySupport * 0.55f;
                    if (position.RelatedAllyId == plan.AssistedAllyId)
                    {
                        if (position.Role == CombatPositionRole.AssistRally) score += 4f;
                        if (position.Role == CombatPositionRole.Interpose) score += 1.5f;
                    }
                    break;
                case CombatIntent.Protect:
                    score += allySupport * 0.7f;
                    if (position.RelatedAllyId == plan.AssistedAllyId)
                    {
                        if (position.Role == CombatPositionRole.Interpose) score += 5f;
                        if (position.Role == CombatPositionRole.AssistRally) score += 3f;
                    }
                    break;
                case CombatIntent.Hold:
                    float holdMovePenalty =
                        plan.PrimaryEnemy.IsRecentAttacker || plan.PrimaryEnemy.IsAttackingPlanner
                            ? 0.08f
                            : 0.18f;
                    score -= Vector2.Distance(snapshot.Position, position.Position) * holdMovePenalty;
                    break;
            }
            if (plan.Intent is CombatIntent.Engage or CombatIntent.Hold &&
                position.Role == CombatPositionRole.CityRetreat)
                score -= 5f;
            if (Vector2.Distance(snapshot.Position, position.Position) < 0.25f)
                currentScore = score;
            if (score <= bestScore) continue;
            bestScore = score;
            best = position;
        }

        if (bestScore == float.MinValue ||
            Vector2.Distance(snapshot.Position, best.Position) < 0.75f)
            return;
        bool tacticalRelocation = currentUsable &&
                                  plan.Intent is CombatIntent.Engage or CombatIntent.Hold;
        if (tacticalRelocation)
        {
            if (currentScore != float.MinValue &&
                bestScore - currentScore < TacticalCombatSettings.RepositionScoreImprovement)
                return;
            plan.Intent = CombatIntent.Reposition;
        }
        plan.Position = best;
        plan.HasPosition = true;
    }

    private static bool RequiresClearShot(CombatActionProfile? nullableProfile)
    {
        return nullableProfile.HasValue && RequiresClearShot(nullableProfile.Value);
    }

    private static bool RequiresClearShot(CombatActionProfile profile)
    {
        return profile.ImpactKind is SkillImpactKind.Projectile
            or SkillImpactKind.Piercing
            or SkillImpactKind.Wave
            or SkillImpactKind.PulseBeam
            or SkillImpactKind.ChannelBeam;
    }

    private static bool HasEquivalentPersistentEffect(
        CombatPlanningSnapshot snapshot,
        CombatActionProfile profile,
        Vector2 target)
    {
        if (!profile.ImpactKind.HasValue) return false;
        float radius = Mathf.Max(2f, profile.EffectRadius * 1.5f);
        float radiusSquared = radius * radius;
        for (int i = 0; i < snapshot.Obstacles.Length; i++)
        {
            CombatObstacleSnapshot obstacle = snapshot.Obstacles[i];
            if (obstacle.OwnerId != snapshot.ActorId ||
                obstacle.Kind != profile.ImpactKind.Value)
                continue;
            if ((obstacle.Position - target).sqrMagnitude <= radiusSquared) return true;
        }
        return false;
    }

    internal static bool IsShotBlocked(
        Vector2 start,
        Vector2 end,
        IReadOnlyList<CombatObstacleSnapshot> obstacles)
    {
        for (int i = 0; i < obstacles.Count; i++)
        {
            CombatObstacleSnapshot obstacle = obstacles[i];
            if (!obstacle.IsHostile || obstacle.Durability <= 0f) continue;
            if (obstacle.Kind == SkillImpactKind.Shield)
            {
                float radius = obstacle.Length * 0.5f + obstacle.Width;
                if (CombatGeometry.SegmentIntersectsCircle(
                        start,
                        end,
                        obstacle.Position,
                        radius))
                    return true;
                continue;
            }
            if (obstacle.Kind != SkillImpactKind.Wall) continue;
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

    private static float StableRoll(long actorId, int revision)
    {
        unchecked
        {
            ulong value = (ulong)actorId;
            value ^= (ulong)(uint)revision * 0x9E3779B185EBCA87UL;
            value ^= value >> 30;
            value *= 0xBF58476D1CE4E5B9UL;
            value ^= value >> 27;
            value *= 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (value & 0xFFFFFFUL) / (float)0x1000000UL;
        }
    }

    private readonly struct ScoredAction
    {
        internal readonly CombatActionCandidate Candidate;
        internal readonly CombatActionUse Use;
        internal readonly float Score;

        internal ScoredAction(
            CombatActionCandidate candidate,
            CombatActionUse use,
            float score)
        {
            Candidate = candidate;
            Use = use;
            Score = score;
        }
    }
}
