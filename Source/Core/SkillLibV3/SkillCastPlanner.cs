using System;
using System.Collections.Generic;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Usage;
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
    public readonly Vector3 TargetPos;
    public readonly bool TrackTarget;
    public readonly float Delay;
    public readonly float InitialAngleOffsetDegrees;
    /// <summary>无施法者序列中本步骤技能实体的出生坐标。</summary>
    public readonly Vector3 SourcePos;
    public readonly bool HasSourcePosition;

    public SkillCastStep(BaseSimObject target, float delay, float initialAngleOffsetDegrees = 0f)
    {
        Target = target;
        TargetPos = default;
        TrackTarget = true;
        Delay = delay;
        InitialAngleOffsetDegrees = initialAngleOffsetDegrees;
        SourcePos = default;
        HasSourcePosition = false;
    }

    public SkillCastStep(Vector3 targetPos, float delay, float initialAngleOffsetDegrees = 0f)
    {
        Target = null;
        TargetPos = targetPos;
        TrackTarget = false;
        Delay = delay;
        InitialAngleOffsetDegrees = initialAngleOffsetDegrees;
        SourcePos = default;
        HasSourcePosition = false;
    }

    private SkillCastStep(Vector3 sourcePos, BaseSimObject target, Vector3 targetPos, bool trackTarget,
        float delay, float initialAngleOffsetDegrees)
    {
        Target = target;
        TargetPos = targetPos;
        TrackTarget = trackTarget;
        Delay = delay;
        InitialAngleOffsetDegrees = initialAngleOffsetDegrees;
        SourcePos = sourcePos;
        HasSourcePosition = true;
    }

    /// <summary>创建从指定世界坐标生成的无施法者序列步骤。</summary>
    public static SkillCastStep FromSource(Vector3 sourcePos, BaseSimObject target, Vector3 targetPos,
        float delay, bool trackTarget = false, float initialAngleOffsetDegrees = 0f)
    {
        return new SkillCastStep(sourcePos, target, targetPos, trackTarget, delay, initialAngleOffsetDegrees);
    }
}

public static class SkillCastPlanner
{
    /// <summary>内容系统可注册的施法许可校验器。</summary>
    private static Func<ActorExtend, Entity, bool> castValidator;

    /// <summary>内容系统可注册的技能出生位置解析器。</summary>
    private static Func<ActorExtend, Entity, Vector3?> sourcePositionResolver;

    /// <summary>注册一个在计划生成前执行的施法许可校验器。</summary>
    /// <param name="validator">返回假时禁止本次技能释放。</param>
    public static void RegisterCastValidator(Func<ActorExtend, Entity, bool> validator)
    {
        castValidator += validator;
    }

    /// <summary>执行全部已注册许可校验，任一拒绝即禁止施法。</summary>
    /// <param name="caster">技能归属人物。</param>
    /// <param name="skill">准备释放的技能。</param>
    /// <returns>没有校验器拒绝时返回真。</returns>
    public static bool CanCast(ActorExtend caster, Entity skill)
    {
        if (castValidator == null) return true;
        Delegate[] validators = castValidator.GetInvocationList();
        for (var i = 0; i < validators.Length; i++)
            if (!((Func<ActorExtend, Entity, bool>)validators[i])(caster, skill)) return false;
        return true;
    }

    /// <summary>注册一个在不改变施法者归属的前提下改写技能出生位置的解析器。</summary>
    /// <param name="resolver">返回世界坐标表示改写；返回空表示继续使用人物位置。</param>
    public static void RegisterSourcePositionResolver(Func<ActorExtend, Entity, Vector3?> resolver)
    {
        sourcePositionResolver += resolver;
    }

    /// <summary>按注册顺序取得第一个明确的技能出生位置。</summary>
    /// <param name="caster">技能归属人物。</param>
    /// <param name="skill">准备释放的技能。</param>
    /// <param name="position">返回出生位置。</param>
    /// <returns>内容系统提供了有效位置时返回真。</returns>
    public static bool TryResolveSourcePosition(ActorExtend caster, Entity skill, out Vector3 position)
    {
        position = default;
        if (caster == null) return false;
        if (SkillCasterContextService.TryGetCurrent(caster, out SkillCasterContext context) &&
            context.Carrier != caster)
        {
            return false;
        }
        if (sourcePositionResolver == null) return false;
        Delegate[] resolvers = sourcePositionResolver.GetInvocationList();
        for (var i = 0; i < resolvers.Length; i++)
        {
            Vector3? candidate = ((Func<ActorExtend, Entity, Vector3?>)resolvers[i])(caster, skill);
            if (!candidate.HasValue) continue;
            position = candidate.Value;
            return true;
        }
        return false;
    }

    private const float DelayStep = 0.04f;
    private const float MinDelayStep = 0.01f;
    private const float MinSalvoAngleOffset = 3f;
    private const float SalvoAngleOffsetStep = 2f;
    private const float MaxSalvoAngleOffset = 30f;
    private const float SalvoAngleOffsetJitter = 1.25f;

    public static SkillCastPlan CreatePlan(ActorExtend caster, Entity skill, BaseSimObject primaryTarget,
        int maxStepCount = int.MaxValue, IReadOnlyList<BaseSimObject> explicitTargets = null,
        bool explicitTargetsOnly = false)
    {
        if (maxStepCount <= 0 || caster == null || caster.Base.isRekt() ||
            !CanCast(caster, skill)) return SkillCastPlan.Empty;
        if (primaryTarget == null || primaryTarget.isRekt()) return SkillCastPlan.Empty;
        if (!skill.HasComponent<SkillContainer>()) return SkillCastPlan.Empty;

        var plan = new SkillCastPlan();
        var castCount = Mathf.Min(DetermineCastCount(caster, skill, primaryTarget), maxStepCount);
        var targets = CollectCandidateTargets(caster.Base, primaryTarget, skill, castCount, explicitTargets,
            explicitTargetsOnly);
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var spreadBias = skill.TryGetComponent(out BurstCount burst) ? Mathf.Max(0, burst.Value - 1) : 0;
        var delayStep = GetDelayStep(skill);

        for (var i = 0; i < castCount; i++)
        {
            var target = i == 0 ? primaryTarget : SelectTarget(primaryTarget, targets, repeatBias, spreadBias);
            var angleOffset = GetSalvoAngleOffset(i, target == primaryTarget);
            plan.Steps.Add(TryResolveSourcePosition(caster, skill, out Vector3 sourcePosition)
                ? SkillCastStep.FromSource(sourcePosition, target, target.GetSimPos(), i * delayStep, true, angleOffset)
                : new SkillCastStep(target, i * delayStep, angleOffset));
        }

        return plan;
    }

    public static SkillCastPlan CreatePointPlan(ActorExtend caster, Entity skill, Vector3 targetPos,
        int maxStepCount = int.MaxValue)
    {
        if (maxStepCount <= 0 || caster == null || caster.Base.isRekt() ||
            !CanCast(caster, skill)) return SkillCastPlan.Empty;
        if (!skill.HasComponent<SkillContainer>()) return SkillCastPlan.Empty;

        var plan = new SkillCastPlan();
        var castCount = Mathf.Min(DetermineCastCount(caster, skill, null), maxStepCount);
        var delayStep = GetDelayStep(skill);

        for (var i = 0; i < castCount; i++)
        {
            var angleOffset = GetSalvoAngleOffset(i, true);
            plan.Steps.Add(TryResolveSourcePosition(caster, skill, out Vector3 sourcePosition)
                ? SkillCastStep.FromSource(sourcePosition, null, targetPos, i * delayStep, false, angleOffset)
                : new SkillCastStep(targetPos, i * delayStep, angleOffset));
        }

        return plan;
    }

    private static float GetSalvoAngleOffset(int stepIndex, bool repeatedPrimaryTarget)
    {
        if (stepIndex <= 0 || !repeatedPrimaryTarget) return 0f;

        var pairIndex = (stepIndex - 1) / 2;
        var sign = stepIndex % 2 == 0 ? 1f : -1f;
        var magnitude = Mathf.Min(MinSalvoAngleOffset + pairIndex * SalvoAngleOffsetStep, MaxSalvoAngleOffset);
        magnitude = Mathf.Clamp(magnitude + Randy.randomFloat(-SalvoAngleOffsetJitter, SalvoAngleOffsetJitter),
            MinSalvoAngleOffset, MaxSalvoAngleOffset);
        return sign * magnitude;
    }

    private static float GetDelayStep(Entity skill)
    {
        var parameters = skill.GetComponent<SkillCastParameters>();
        return Mathf.Max(MinDelayStep, DelayStep * parameters.SalvoIntervalMultiplier);
    }

    private static int DetermineCastCount(ActorExtend caster, Entity skill, BaseSimObject primaryTarget)
    {
        if (skill.GetComponent<SkillContainer>().Asset.UseProfile.Multiplicity == SkillUseMultiplicity.Single)
            return 1;
        var budgetResolution = SkillCastBudgetResolver.Resolve(caster, skill, primaryTarget);
        var budget = budgetResolution.MaxSteps;
        if (budget <= 1) return 1;

        var powerLevel = caster.GetPowerLevel();
        var repeatBias = skill.TryGetComponent(out SalvoCount salvo) ? Mathf.Max(0, salvo.Value - 1) : 0;
        var threatRatio = primaryTarget.isRekt() ? 0f : GetThreatRatio(caster, primaryTarget);
        var powerFactor = Mathf.Clamp01(powerLevel / 10f);
        var intent = 0.35f
                     + threatRatio * 0.45f
                     + powerFactor * 0.1f
                     + Mathf.Clamp(repeatBias, 0, 8) * 0.05f;

        if (budgetResolution.ForceFullBudgetAgainstMajorThreat && (threatRatio >= 0.85f || repeatBias >= 4))
        {
            intent = 1f;
        }

        return Mathf.Clamp(Mathf.CeilToInt(budget * Mathf.Clamp01(intent)), 1, budget);
    }

    private static List<BaseSimObject> CollectCandidateTargets(BaseSimObject caster, BaseSimObject primaryTarget,
        Entity skill, int expectedCount, IReadOnlyList<BaseSimObject> explicitTargets = null,
        bool explicitTargetsOnly = false)
    {
        var targets = new List<BaseSimObject>();
        AddCandidateTarget(targets, primaryTarget, caster, int.MaxValue);
        if (explicitTargets != null)
        {
            foreach (var explicitTarget in explicitTargets)
            {
                AddCandidateTarget(targets, explicitTarget, caster, int.MaxValue);
            }
        }

        if (expectedCount <= 1) return targets;
        if (explicitTargetsOnly) return targets;

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
        if (target == null) return;
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

    private static float GetThreatRatio(ActorExtend caster, BaseSimObject target)
    {
        if (target.isRekt() || !target.isActor()) return 0f;

        var targetPowerLevel = target.a.GetExtend().GetPowerLevel();
        var delta = targetPowerLevel - caster.GetPowerLevel();
        return Mathf.Clamp01((delta + 2f) / 6f);
    }
}
