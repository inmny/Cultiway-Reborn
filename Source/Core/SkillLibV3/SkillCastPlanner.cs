using System.Collections.Generic;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Core.SkillLibV3;

public sealed class SkillCastPlan
{
    public static readonly SkillCastPlan Empty = new();

    public List<SkillCastStep> Steps { get; } = new();
}

public readonly struct SkillCastStep
{
    public readonly BaseSimObject Target;
    public readonly float Delay;

    public SkillCastStep(BaseSimObject target, float delay)
    {
        Target = target;
        Delay = delay;
    }
}

public static class SkillCastPlanner
{
    private const float DelayStep = 0.04f;

    public static SkillCastPlan CreatePlan(ActorExtend caster, Entity skill, BaseSimObject primaryTarget,
        int maxStepCount = int.MaxValue)
    {
        if (maxStepCount <= 0) return SkillCastPlan.Empty;
        if (caster == null || caster.Base.isRekt()) return SkillCastPlan.Empty;
        if (primaryTarget.isRekt()) return SkillCastPlan.Empty;
        if (!skill.HasComponent<SkillContainer>()) return SkillCastPlan.Empty;

        var plan = new SkillCastPlan();
        var castCount = Mathf.Min(DetermineCastCount(caster, skill, primaryTarget), maxStepCount);
        var targets = CollectCandidateTargets(caster.Base, primaryTarget, skill, castCount);
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;

        for (var i = 0; i < castCount; i++)
        {
            var target = i == 0 ? primaryTarget : SelectTarget(primaryTarget, targets, repeatBias, spreadBias);
            plan.Steps.Add(new SkillCastStep(target, i * DelayStep));
        }

        return plan;
    }

    private static int DetermineCastCount(ActorExtend caster, Entity skill, BaseSimObject primaryTarget)
    {
        if (!caster.HasCultisys<Xian>()) return 1;

        var level = caster.GetCultisys<Xian>().CurrLevel;
        var budget = GetRealmCastBudget(level);
        if (budget <= 1) return 1;

        var powerLevel = caster.GetPowerLevel();
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var threatRatio = GetThreatRatio(caster, primaryTarget);
        var powerFactor = Mathf.Clamp01(powerLevel / 10f);
        var intent = 0.35f
                     + threatRatio * 0.45f
                     + powerFactor * 0.1f
                     + Mathf.Clamp(repeatBias, 0, 8) * 0.05f;

        if (level >= XianLevels.Yuanying && (threatRatio >= 0.85f || repeatBias >= 4))
        {
            intent = 1f;
        }

        return Mathf.Clamp(Mathf.CeilToInt(budget * Mathf.Clamp01(intent)), 1, budget);
    }

    private static List<BaseSimObject> CollectCandidateTargets(BaseSimObject caster, BaseSimObject primaryTarget,
        Entity skill, int expectedCount)
    {
        var targets = new List<BaseSimObject> { primaryTarget };
        if (expectedCount <= 1) return targets;

        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        var radius = Mathf.Clamp(4f + spreadBias * 0.75f, 4f, 10f);
        var center = primaryTarget.current_position;
        var targetLimit = Mathf.Max(expectedCount * 2, targets.Count);

        AddAttackersOfCaster(targets, caster, targetLimit);

        foreach (var target in SkillUtils.IterEnemyInSphere(center, radius, caster))
        {
            AddCandidateTarget(targets, target, caster, targetLimit);
            if (targets.Count >= targetLimit) break;
        }

        return targets;
    }

    /// <summary>
    /// 把正在攻击施法者或最近攻击过施法者的单位加入候选目标，不依赖这次攻击是否实际造成伤害。
    /// </summary>
    private static void AddAttackersOfCaster(List<BaseSimObject> targets, BaseSimObject caster, int targetLimit)
    {
        if (caster.isRekt() || !caster.isActor()) return;

        var casterActor = caster.a;
        foreach (var recentAttacker in casterActor.GetExtend().GetRecentAttackersSnapshot())
        {
            AddCandidateTarget(targets, recentAttacker, caster, targetLimit);
            if (targets.Count >= targetLimit) return;
        }

        AddCandidateTarget(targets, casterActor.attackedBy, caster, targetLimit);
        if (targets.Count >= targetLimit) return;

        foreach (var actor in World.world.units.units_only_alive)
        {
            if (targets.Count >= targetLimit) break;
            if (actor.isRekt()) continue;
            if (!actor.has_attack_target || actor.attack_target != caster) continue;
            AddCandidateTarget(targets, actor, caster, targetLimit);
        }
    }

    private static void AddCandidateTarget(List<BaseSimObject> targets, BaseSimObject target, BaseSimObject caster,
        int targetLimit)
    {
        if (targets.Count >= targetLimit) return;
        if (target.isRekt()) return;
        if (target == caster) return;
        if (targets.Contains(target)) return;

        targets.Add(target);
    }

    private static BaseSimObject SelectTarget(BaseSimObject primaryTarget, List<BaseSimObject> targets, int repeatBias,
        int spreadBias)
    {
        if (targets.Count <= 1) return primaryTarget;

        var primaryWeight = 1f + Mathf.Clamp(repeatBias, 0, 32);
        var otherWeight = 1f + Mathf.Clamp(spreadBias, 0, 32);
        var totalWeight = primaryWeight + otherWeight * (targets.Count - 1);
        var roll = Randy.randomFloat(0f, totalWeight);
        if (roll < primaryWeight) return primaryTarget;

        var index = 1 + Mathf.FloorToInt((roll - primaryWeight) / otherWeight);
        return targets[Mathf.Clamp(index, 1, targets.Count - 1)];
    }

    private static int GetRealmCastBudget(int level)
    {
        return level switch
        {
            0 => 1,
            XianLevels.XianBase => 4,
            XianLevels.Jindan => 32,
            XianLevels.Yuanying => 256,
            _ => 1024
        };
    }

    private static float GetThreatRatio(ActorExtend caster, BaseSimObject target)
    {
        if (target.isRekt() || !target.isActor()) return 0f;

        var targetPowerLevel = target.a.GetExtend().GetPowerLevel();
        var delta = targetPowerLevel - caster.GetPowerLevel();
        return Mathf.Clamp01((delta + 2f) / 6f);
    }
}
