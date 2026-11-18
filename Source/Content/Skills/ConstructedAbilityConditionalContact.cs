using System.Collections.Generic;
using Cultiway.Content.Combat;
using Cultiway.Core.Combat;
using Cultiway.Core.Skills;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>顺序核对实际生命与状态，立即结算，再按本条真实伤害结果追加有限后续作用。</summary>
    /// <param name="running">本次真实施放。</param><param name="node">接触部位。</param>
    /// <param name="target">实际接触人物。</param><param name="targetId">已确认的人物身份。</param>
    /// <param name="rules">已经编译的有限分支。</param><param name="origin">实际传播中心。</param>
    /// <param name="strength">接触余力或传播衰减倍率。</param>
    private static ConstructedTargetResponse ApplyConditionalContacts(RunningCast running, int node, Actor target, long targetId,
        IReadOnlyList<CompiledConstructionConditionalContact> rules, Vector3? origin, float strength)
    {
        long generation = worldGeneration;
        Vector3 velocity = Vector3.zero;
        bool force = false, damage = false;
        DamageSettlement settled = default;
        foreach (CompiledConstructionConditionalContact rule in rules)
        {
            if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth()) break;
            bool matches = (!rule.MaximumHealthRatio.HasValue || target.getHealthRatio() <= rule.MaximumHealthRatio.Value) &&
                           (rule.RequiredStatus == null || CombatStatusEffects.TryGetActiveStatus(target, rule.RequiredStatus, out _));
            CompiledConstructionContactOutcome outcome = matches ? rule.Outcome : rule.Otherwise;
            ConstructedTargetResponse response = ApplyContactOutcome(running, node, target, targetId, outcome, origin, strength, out bool contactDamage);
            velocity += response.VelocityChange; force |= response.ForceApplied; damage |= response.AdditionalDamageAttempted;
            settled += response.Damage;
            if (generation != worldGeneration || target.data?.id != targetId) break;
            if (contactDamage)
            {
                CompiledConstructionContactOutcome follow = target.isAlive() && target.hasHealth() ? rule.OnSurvive : rule.OnKill;
                response = ApplyContactOutcome(running, node, target, targetId, follow, origin, strength, out _);
                velocity += response.VelocityChange; force |= response.ForceApplied; damage |= response.AdditionalDamageAttempted;
                settled += response.Damage;
            }
        }
        return new ConstructedTargetResponse(targetId, velocity, force, 0f, 0f, additionalDamage: damage, damage: settled);
    }

    /// <summary>先提交明确伤害，再恢复仍有效的人物，最后只对活着的接触者施加状态和运动。</summary>
    /// <param name="running">本次真实施放。</param><param name="node">实际部位。</param>
    /// <param name="target">实际接触人物。</param><param name="targetId">接触时身份。</param>
    /// <param name="outcome">明确的普通结果；空值表示无作用。</param><param name="origin">实际传播中心。</param>
    /// <param name="strength">接触余力或传播衰减倍率。</param>
    /// <param name="contactDamage">本结果是否实际提交了接触人物伤害。</param>
    private static ConstructedTargetResponse ApplyContactOutcome(RunningCast running, int node, Actor target, long targetId,
        CompiledConstructionContactOutcome outcome, Vector3? origin, float strength, out bool contactDamage)
    {
        contactDamage = false;
        if (outcome == null) return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f);
        long generation = worldGeneration;
        // 伤害入口会跳过已死亡接触者，但允许仍有效控制者承受明确作用。
        ApplyAdditionalDamage(running, node, target, targetId, outcome.Damage, strength, out bool attempted, out contactDamage, out var settled);
        if (generation != worldGeneration || target.data?.id != targetId)
            return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, additionalDamage: attempted, damage: settled);
        bool alive = target.isAlive() && target.hasHealth();
        if (outcome.Restoration?.Target == ConstructionRestorationTarget.Controller || alive)
            ApplyRestoration(running, node, target, outcome.Restoration, strength);
        if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive() || !target.hasHealth())
            return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f, additionalDamage: attempted, damage: settled);
        ApplyStatuses(running, node, target, outcome.Statuses, strength);
        Vector3 velocity = ApplyContactKnockback(running, node, target, outcome.Knockback, ApplyAttackState.Hit, out bool force, origin, strength);
        if (force) InterruptContactBody(target);
        return new ConstructedTargetResponse(targetId, velocity, force, 0f, 0f, additionalDamage: attempted, damage: settled);
    }
}
