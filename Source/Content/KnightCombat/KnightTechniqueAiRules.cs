using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>提供普通战斗 AI 与战术规划器共用的骑士战技情境信号。</summary>
internal static class KnightTechniqueAiRules
{
    public static bool IsCritical(ActorExtend caster)
    {
        return caster.Base.getHealthRatio() < 0.35f;
    }

    public static float ResolveVigorRatio(ActorExtend caster)
    {
        float maxVigor = caster.Base.stats[BaseStatses.MaxVigor.id];
        return maxVigor <= 0f ? 0f : caster.GetCultisys<Knight>().vigor / maxVigor;
    }

    public static float ResolveAttackDamage(ActorExtend caster)
    {
        return Mathf.Max(1f, caster.Base.stats[S.damage]);
    }

    public static int CountGroundHostiles(Actor caster, float radius)
    {
        var count = 0;
        CombatTargeting.ForEachHostile(caster, caster.current_position, radius, target =>
        {
            if (!target.isFlying()) count++;
        });
        return count;
    }

    public static int CountGroundHostilesAround(Actor caster, BaseSimObject center, float radius)
    {
        if (center == null || center.isRekt()) return 0;
        var count = 0;
        CombatTargeting.ForEachHostile(caster, center.current_position, radius, target =>
        {
            if (!target.isFlying()) count++;
        });
        return count;
    }

    public static bool IsHighValueTarget(ActorExtend caster, BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        if (target.isBuilding()) return true;
        if (!target.isActor()) return false;

        Actor actor = target.a;
        return actor.isKing() || actor.isCityLeader() || actor.isFavorite() ||
               actor.GetExtend().GetPowerLevel() >= caster.GetPowerLevel();
    }

    public static bool IsHighTierTarget(
        ActorExtend caster,
        BaseSimObject target,
        int minimumNearbyHostiles,
        float nearbyRadius)
    {
        return IsHighValueTarget(caster, target) ||
               CountGroundHostilesAround(caster.Base, target, nearbyRadius) >= minimumNearbyHostiles;
    }

    public static bool IsIsolated(ActorExtend caster, BaseSimObject target)
    {
        if (target?.isActor() != true || target.isRekt()) return false;
        var isolated = true;
        CombatTargeting.ForEachActor(
            target.a,
            target.current_position,
            1.8f,
            CombatTargeting.TargetDisposition.Any,
            candidate =>
            {
                if (candidate != target.a && SkillTargetRelationResolver.IsFriendly(target, candidate))
                    isolated = false;
            });
        return isolated;
    }
}
