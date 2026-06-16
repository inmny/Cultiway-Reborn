using System.Collections.Generic;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
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
    private const int MaxPlannedCastCount = 32;

    public static SkillCastPlan CreatePlan(ActorExtend caster, Entity skill, BaseSimObject primaryTarget)
    {
        if (caster == null || caster.Base == null || caster.Base.isRekt()) return SkillCastPlan.Empty;
        if (primaryTarget == null || primaryTarget.isRekt()) return SkillCastPlan.Empty;
        if (!skill.HasComponent<SkillContainer>()) return SkillCastPlan.Empty;

        var plan = new SkillCastPlan();
        var castCount = DetermineCastCount(caster, skill, primaryTarget);
        var targets = CollectCandidateTargets(caster.Base, primaryTarget, skill, castCount);
        var masteryRatio = Mathf.Clamp01(caster.GetMainCultibookMastery() / 100f);
        var delayStep = Mathf.Lerp(0.22f, 0.1f, masteryRatio);
        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        var multiTargetChance = Mathf.Clamp01(0.2f + (targets.Count - 1) * 0.08f + spreadBias * 0.04f + masteryRatio * 0.2f);

        for (var i = 0; i < castCount; i++)
        {
            var target = primaryTarget;
            if (i > 0 && targets.Count > 1 && Randy.randomChance(multiTargetChance))
            {
                target = targets[i % targets.Count];
            }

            plan.Steps.Add(new SkillCastStep(target, i * delayStep));
        }

        return plan;
    }

    private static int DetermineCastCount(ActorExtend caster, Entity skill, BaseSimObject primaryTarget)
    {
        if (!caster.HasCultisys<Xian>()) return 1;

        var powerLevel = caster.GetPowerLevel();
        var masteryRatio = Mathf.Clamp01(caster.GetMainCultibookMastery() / 100f);
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var threatRatio = GetThreatRatio(caster, primaryTarget);
        var castCount = 1;
        for (var nextIndex = 2; nextIndex <= MaxPlannedCastCount; nextIndex++)
        {
            var chance = 0.12f
                         + masteryRatio * 0.25f
                         + Mathf.Clamp01(powerLevel / 10f) * 0.2f
                         + Mathf.Clamp(repeatBias, 0, 8) * 0.03f
                         + threatRatio * 0.08f;
            chance *= Mathf.Pow(0.65f, nextIndex - 2);
            if (!Randy.randomChance(Mathf.Clamp01(chance))) break;
            castCount++;
        }

        return castCount;
    }

    private static List<BaseSimObject> CollectCandidateTargets(BaseSimObject caster, BaseSimObject primaryTarget,
        Entity skill, int expectedCount)
    {
        var targets = new List<BaseSimObject> { primaryTarget };
        if (expectedCount <= 1) return targets;

        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        var radius = Mathf.Clamp(4f + spreadBias * 0.75f, 4f, 10f);
        var center = primaryTarget.current_position;

        foreach (var target in SkillUtils.IterEnemyInSphere(center, radius, caster))
        {
            if (target == null || target.isRekt()) continue;
            if (targets.Contains(target)) continue;
            targets.Add(target);
            if (targets.Count >= expectedCount * 2) break;
        }

        return targets;
    }

    private static float GetThreatRatio(ActorExtend caster, BaseSimObject target)
    {
        if (target == null || !target.isActor() || target.isRekt()) return 0f;

        var targetPowerLevel = target.a.GetExtend().GetPowerLevel();
        var delta = targetPowerLevel - caster.GetPowerLevel();
        return Mathf.Clamp01((delta + 2f) / 6f);
    }
}
