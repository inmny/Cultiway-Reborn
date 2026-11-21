using System;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>骑士战技共用的身体、兵器和实际通路判断。</summary>
internal static class KnightTechniqueRuntimeService
{
    public static bool CanAct(ActorExtend caster, bool allowOwnedStance = false)
    {
        return caster != null && !caster.Base.isRekt() && !caster.Base.isFlying() &&
               (allowOwnedStance || !Core.Skills.BodyControlState.IsBusy(caster.Base.id)) &&
               !caster.Base.stats.hasTag("frozen_ai") && !caster.Base.stats.hasTag("immovable");
    }

    public static bool IsCurrentTechniqueWeapon(
        ActorExtend caster,
        KnightTechniqueAsset technique,
        Item weapon,
        bool allowOwnedStance = false)
    {
        return CanAct(caster, allowOwnedStance) &&
               KnightTechniqueAccessService.TryResolveCurrentWeapon(
                   caster,
                   technique,
                   out Item current,
                   out _) &&
               ReferenceEquals(current, weapon);
    }

    public static bool IsValidGroundTarget(
        ActorExtend caster,
        BaseSimObject target,
        float minimumRange,
        float maximumRange)
    {
        if (target == null || target.isRekt() || target == caster.Base ||
            !caster.Base.canAttackTarget(target) || target.current_tile == null ||
            target.isActor() && target.a.isFlying()) return false;
        float distance = Vector2.Distance(caster.Base.current_position, target.current_position);
        float targetSize = Mathf.Max(0f, target.stats[S.size]);
        return distance >= minimumRange && distance <= maximumRange + targetSize;
    }

    public static bool IsWithinRange(Actor source, BaseSimObject target, float range)
    {
        if (source == null || source.isRekt() || target == null || target.isRekt()) return false;
        float allowed = range + Mathf.Max(0f, target.stats[S.size]);
        return Toolbox.SquaredDistVec2Float(source.current_position, target.current_position) <= allowed * allowed;
    }

    public static float ResolveMeleeRange(Actor actor)
    {
        return Mathf.Max(1.35f, actor.getAttackRange());
    }

    public static Vector3 ResolveDirection(Actor caster, BaseSimObject target = null)
    {
        Vector3 direction = target == null || target.isRekt()
            ? caster.has_attack_target && !caster.attack_target.isRekt()
                ? caster.attack_target.current_position - caster.current_position
                : caster.is_looking_left ? Vector3.left : Vector3.right
            : target.current_position - caster.current_position;
        direction.z = 0f;
        return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.right;
    }

    public static bool IsStraightPathClear(Vector2 start, Vector2 end)
    {
        float distance = Vector2.Distance(start, end);
        int samples = Mathf.Max(1, Mathf.CeilToInt(distance * 2f));
        for (var i = 1; i <= samples; i++)
        {
            Vector2 point = Vector2.Lerp(start, end, i / (float)samples);
            WorldTile tile = World.world.GetTile(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
            if (tile == null || tile.Type.block || tile.Type.liquid || tile.Type.lava || tile.Type.damage_units)
                return false;
        }
        return true;
    }
}
