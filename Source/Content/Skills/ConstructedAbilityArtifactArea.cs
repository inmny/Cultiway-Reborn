using Cultiway.Content.Combat;
using Cultiway.Core.Combat;
using Cultiway.Core.EventSystem.Systems;
using Cultiway.Core.Skills;
using Cultiway.Core.Visuals;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content.Skills;

public sealed partial class ConstructedAbilityService
{
    /// <summary>地面冲击的眩晕停止行动和待机动画，同时允许同次真实外力产生位移。</summary>
    private static readonly BaseStats impactDazeStats = CreateImpactDazeStats();

    /// <summary>这一击仍使用原眩晕状态，仅采用能承受外力的属性。</summary>
    private static BaseStats CreateImpactDazeStats()
    {
        var stats = new BaseStats();
        stats.addTag("frozen_ai");
        stats.addTag("stop_idle_animation");
        return stats;
    }

    /// <summary>圆形接触沿用原法宝的敌我判断，净化也可以接触原有友方。</summary>
    /// <param name="owner">本次真实归属。</param><param name="target">候选人物。</param>
    /// <param name="contact">本击的原作用。</param>
    private static bool MatchesArtifactArea(CastAuthority owner, Actor target, ArtifactAreaContact contact) =>
        owner.IsLive && (CombatTargeting.MatchesDisposition(owner.Actor, target, CombatTargeting.TargetDisposition.Hostile) ||
            contact.Purifies && CombatTargeting.MatchesDisposition(owner.Actor, target, CombatTargeting.TargetDisposition.Friendly));

    /// <summary>圆面确认到人物后，兑现原法宝的伤害、震退或实际解除。</summary>
    /// <param name="running">本次施放。</param><param name="node">真正接触的圆面。</param>
    /// <param name="target">真实接触的人物。</param><param name="point">真实接触位置。</param>
    /// <param name="contact">出手时冻结的资料。</param>
    private static ConstructedTargetResponse ApplyArtifactAreaContact(RunningCast running, int node, Actor target,
        Vector3 point, ArtifactAreaContact contact)
    {
        CastAuthority owner = running.Owner(node);
        if (!MatchesArtifactArea(owner, target, contact)) return default;
        Actor source = owner.Actor;
        if (contact.ApplyEffect != null)
        {
            contact.ApplyEffect(source, target);
            return default;
        }

        if (contact.CleanseCount is { } count)
        {
            int changed = 0;
            if (CombatTargeting.MatchesDisposition(source, target, CombatTargeting.TargetDisposition.Friendly))
                changed += CombatStatusEffects.CleanseNegativeStatuses(target, count);
            if (CombatTargeting.MatchesDisposition(source, target, CombatTargeting.TargetDisposition.Hostile))
                changed += CombatStatusEffects.DispelPositiveStatuses(target, count);
            if (changed > 0)
                SpatialVisualEffects.Particle(point + Vector3.forward * .42f, Vector3.forward * .8f,
                    new Color(.64f, 1f, .82f), .20f, .34f);
            return default;
        }

        long generation = worldGeneration, targetId = target.id;
        DamageSettlement result = default;
        if (contact.DamageFirst && contact.Damage > 0f)
        {
            result = SettleAreaDamage(owner, targetId, contact.Damage);
            if (generation != worldGeneration || target.data?.id != targetId || !target.isAlive())
                return new ConstructedTargetResponse(targetId, Vector3.zero, false, 0f, 0f,
                    additionalDamage: true, damage: result);
        }

        Vector3 before = ConstructedActorImpulse.Velocity(target);
        Vector2 away = target.current_position - (Vector2)running.Cast.ContactOrigin(node);
        Vector2 force = (away.sqrMagnitude < .0001f ? Vector2.up : away.normalized) * contact.Force;
        bool pushed = ApplyLegacyGroundSlide(target, source, force.x, force.y);
        Vector3 change = ConstructedActorImpulse.Velocity(target) - before;
        CombatStatusEffects.ApplyContactStatus(target, StatusEffects.Daze, contact.Daze, source,
            owner.Damage.AttackerPowerLevel ?? source.GetExtend().GetPowerLevel(), ConstructionStatusApplication.Strongest,
            pushed ? impactDazeStats : null);
        float daze = CombatStatusEffects.GetStatusRemaining(target, StatusEffects.Daze);
        if (!contact.DamageFirst && contact.Damage > 0f && generation == worldGeneration &&
            target.data?.id == targetId && target.isAlive())
            result = SettleAreaDamage(owner, targetId, contact.Damage);
        return new ConstructedTargetResponse(targetId, change, pushed, daze, 0f,
            additionalDamage: contact.Damage > 0f, damage: result);
    }

    /// <summary>保持该次法宝打击已确认的实际伤害、来源和元素。</summary>
    private static DamageSettlement SettleAreaDamage(CastAuthority owner, long target, float amount)
    {
        var hit = owner.Damage;
        hit.TargetID = target;
        hit.Damage = amount;
        return GetHitEventSystem.Resolve(hit);
    }
}
