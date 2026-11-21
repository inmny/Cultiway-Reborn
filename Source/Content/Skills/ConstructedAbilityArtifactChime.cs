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
    /// <summary>钟鸣的眩晕停止行动和待机动画，同时允许同次真实外力产生位移。</summary>
    private static readonly BaseStats chimeDazeStats = CreateChimeDazeStats();

    /// <summary>这一击仍使用原眩晕状态，仅采用能承受外力的属性。</summary>
    private static BaseStats CreateChimeDazeStats()
    {
        var stats = new BaseStats();
        stats.addTag("frozen_ai");
        stats.addTag("stop_idle_animation");
        return stats;
    }

    /// <summary>圆形接触使用原法钟的敌我判断，清音也可以接触原有友方。</summary>
    /// <param name="owner">本次真实归属。</param><param name="target">候选人物。</param>
    /// <param name="chime">本击的原作用。</param>
    private static bool MatchesArtifactChime(CastAuthority owner, Actor target, ArtifactChimeContact chime) =>
        owner.IsLive && (CombatTargeting.MatchesDisposition(owner.Actor, target, CombatTargeting.TargetDisposition.Hostile) ||
            chime.Purifies && CombatTargeting.MatchesDisposition(owner.Actor, target, CombatTargeting.TargetDisposition.Friendly));

    /// <summary>圆面确认到人物后，兑现原钟声的伤害、震退或实际解除。</summary>
    /// <param name="running">本次施放。</param><param name="node">真正接触的圆面。</param>
    /// <param name="target">真实接触的人物。</param><param name="point">真实接触位置。</param>
    /// <param name="chime">出手时冻结的资料。</param>
    private static ConstructedTargetResponse ApplyArtifactChimeContact(RunningCast running, int node, Actor target,
        Vector3 point, ArtifactChimeContact chime)
    {
        CastAuthority owner = running.Owner(node);
        if (!MatchesArtifactChime(owner, target, chime)) return default;
        Actor source = owner.Actor;
        if (chime.CleanseCount is { } count)
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
        Vector3 before = ConstructedActorImpulse.Velocity(target);
        Vector2 away = target.current_position - (Vector2)running.Cast.ContactOrigin(node);
        Vector2 force = (away.sqrMagnitude < .0001f ? Vector2.up : away.normalized) * chime.Force;
        bool pushed = ApplyLegacyGroundSlide(target, source, force.x, force.y);
        Vector3 change = ConstructedActorImpulse.Velocity(target) - before;
        CombatStatusEffects.ApplyContactStatus(target, StatusEffects.Daze, chime.Daze, source,
            owner.Damage.AttackerPowerLevel ?? source.GetExtend().GetPowerLevel(), ConstructionStatusApplication.Strongest,
            pushed ? chimeDazeStats : null);
        float daze = CombatStatusEffects.GetStatusRemaining(target, StatusEffects.Daze);
        DamageSettlement result = default;
        if (chime.Damage > 0f && generation == worldGeneration && target.data?.id == targetId && target.isAlive())
        {
            var hit = owner.Damage;
            hit.TargetID = targetId;
            hit.Damage = chime.Damage;
            result = GetHitEventSystem.Resolve(hit);
        }
        return new ConstructedTargetResponse(targetId, change, pushed, daze, 0f,
            additionalDamage: chime.Damage > 0f, damage: result);
    }
}
