using System;
using System.Collections.Generic;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Content.KnightCombat;

/// <summary>骑士战技短动作共用的准入、恢复时间和取消判断。</summary>
internal static class KnightTechniqueRuntimeService
{
    private static readonly Dictionary<long, float> BusyUntil = new();

    public static bool IsBusy(ActorExtend caster)
    {
        if (caster == null || caster.Base.isRekt()) return false;
        if (caster.E.HasComponent<KnightMovementState>()) return true;
        long actorId = caster.Base.getID();
        if (!BusyUntil.TryGetValue(actorId, out float until)) return false;
        if (Time.time < until) return true;
        BusyUntil.Remove(actorId);
        return false;
    }

    public static void BeginAction(ActorExtend caster, float duration)
    {
        long actorId = caster.Base.getID();
        float until = Time.time + Mathf.Max(0f, duration);
        if (!BusyUntil.TryGetValue(actorId, out float current) || until > current)
            BusyUntil[actorId] = until;
    }

    public static bool CanAct(ActorExtend caster)
    {
        return caster != null && !caster.Base.isRekt() && !caster.Base.isFlying() &&
               !caster.Base.stats.hasTag("frozen_ai") && !caster.Base.stats.hasTag("immovable");
    }

    public static bool IsCurrentTechniqueWeapon(
        ActorExtend caster,
        KnightTechniqueAsset technique,
        Item weapon)
    {
        return CanAct(caster) &&
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

    public static KnightActionSequence CreateSequence(
        KnightTechniqueContext context,
        BaseSimObject target,
        float range)
    {
        return new KnightActionSequence(context.Caster, context.Technique, context.Weapon, target, range);
    }

    public static void ClearWorldState()
    {
        BusyUntil.Clear();
    }
}

/// <summary>一组延后武器段共享的取消令牌。</summary>
internal sealed class KnightActionSequence
{
    private readonly ActorExtend caster;
    private readonly KnightTechniqueAsset technique;
    private readonly Item weapon;
    private readonly BaseSimObject target;
    private readonly float range;
    private bool cancelled;

    public KnightActionSequence(
        ActorExtend caster,
        KnightTechniqueAsset technique,
        Item weapon,
        BaseSimObject target,
        float range)
    {
        this.caster = caster;
        this.technique = technique;
        this.weapon = weapon;
        this.target = target;
        this.range = range;
    }

    public bool TryContinue(out KnightTechniqueContext context)
    {
        context = default;
        if (cancelled || !KnightTechniqueRuntimeService.IsCurrentTechniqueWeapon(caster, technique, weapon) ||
            target != null && (target.isRekt() || !KnightTechniqueRuntimeService.IsWithinRange(caster.Base, target, range)))
        {
            cancelled = true;
            return false;
        }

        var activeTarget = new ActiveAbilityTarget(
            target,
            target?.GetSimPos() ?? caster.Base.GetSimPos());
        if (KnightTechniqueAccessService.TryCreateContext(
                caster,
                technique,
                weapon,
                caster.Base.getWeaponAsset(),
                target,
                activeTarget,
                out context)) return true;
        cancelled = true;
        return false;
    }

    public void Cancel()
    {
        cancelled = true;
    }
}
