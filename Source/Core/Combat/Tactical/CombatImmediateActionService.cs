using System.Collections.Generic;
using strings;
using UnityEngine;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>
/// 统一选择原版即时格挡、偏转、闪避与外部直接调用的高级战斗动作。
/// 这些动作由原版调用点立即执行，因此本服务只负责可用性、选择、资源和独立冷却。
/// </summary>
internal static class CombatImmediateActionService
{
    /// <summary>
    /// 从同一触发池中选出一项近优动作。返回 true 后，原版调用点会立即调用该动作的委托。
    /// </summary>
    internal static bool TrySelect(
        Actor actor,
        IList<CombatActionAsset> source,
        BaseSimObject attackTarget,
        out CombatActionAsset selected)
    {
        selected = null;
        if (actor.isRekt() || source == null || source.Count == 0 || actor.hasTrait("slow"))
            return false;

        using var candidates = new ListPool<WeightedAction>(source.Count);
        float bestScore = 0f;
        for (int i = 0; i < source.Count; i++)
        {
            CombatActionAsset action = source[i];
            if (!CanUse(actor, action, attackTarget)) continue;

            float score = Score(actor, action);
            if (score <= 0f) continue;
            candidates.Add(new WeightedAction(action, score));
            if (score > bestScore) bestScore = score;
        }
        if (candidates.Count == 0) return false;

        float minimumScore = bestScore * (1f - TacticalCombatSettings.NearOptimalScoreWindow);
        float totalWeight = 0f;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i].Score >= minimumScore) totalWeight += candidates[i].Score;
        }

        float roll = Randy.randomFloat(0f, totalWeight);
        for (int i = 0; i < candidates.Count; i++)
        {
            WeightedAction candidate = candidates[i];
            if (candidate.Score < minimumScore) continue;
            roll -= candidate.Score;
            if (roll > 0f) continue;
            selected = candidate.Action;
            break;
        }
        selected ??= candidates[0].Action;

        float chance = Mathf.Clamp01(
            selected.chance + selected.chance * actor.stats[S.skill_combat]);
        if (!Randy.randomChance(chance))
        {
            selected = null;
            return false;
        }

        actor.spendStamina(selected.cost_stamina);
        actor.spendMana(selected.cost_mana);
        CombatWorldService.StartActionCooldown(actor, CreateKey(selected), selected.cooldown);
        return true;
    }

    /// <summary>检查具体动作的委托、资源、目标条件与独立冷却。</summary>
    private static bool CanUse(
        Actor actor,
        CombatActionAsset action,
        BaseSimObject attackTarget)
    {
        if (action == null ||
            !actor.hasEnoughStamina(action.cost_stamina) ||
            !actor.hasEnoughMana(action.cost_mana) ||
            !CombatWorldService.IsActionReady(actor, CreateKey(action)))
            return false;

        if (attackTarget.isRekt())
            return action.action_actor != null;
        if (action.action_actor_target_position == null) return false;
        return action.can_do_action == null || action.can_do_action(actor, attackTarget);
    }

    /// <summary>以触发概率和动作权重为主，资源压力为次计算即时动作价值。</summary>
    private static float Score(Actor actor, CombatActionAsset action)
    {
        float reliability = Mathf.Clamp01(
            action.chance + action.chance * actor.stats[S.skill_combat]);
        float resourcePressure = CombatActionService.ResolveResourceRatio(
            actor,
            action.cost_mana,
            action.cost_stamina);
        return reliability * Mathf.Max(1f, action.rate) /
               (1f + resourcePressure * 0.75f);
    }

    /// <summary>为原版动作建立不依赖 SkillEntityAsset 的具体冷却身份。</summary>
    private static CombatActionKey CreateKey(CombatActionAsset action)
    {
        return CombatActionService.CreateOriginalActionKey(action);
    }

    private readonly struct WeightedAction
    {
        internal readonly CombatActionAsset Action;
        internal readonly float Score;

        internal WeightedAction(CombatActionAsset action, float score)
        {
            Action = action;
            Score = score;
        }
    }
}
