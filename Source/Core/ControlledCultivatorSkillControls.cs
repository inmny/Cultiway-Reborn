using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Core;

internal static class ControlledCultivatorSkillControls
{
    private static readonly Dictionary<long, int> SelectedAbilityIndexes = new();

    internal static bool CastSelectedSkill()
    {
        return CastSelectedSkill(SkillTargetSelectionArea.Inactive);
    }

    internal static bool CastSelectedSkill(SkillTargetSelectionArea selectionArea)
    {
        if (!TryGetControlledActor(out var actor)) return false;

        if (!TryGetSelectedAttackAbility(actor, out _))
        {
            ShowTip("没有可释放的主动能力");
            return false;
        }

        var aim = ResolveAim(actor);
        var attackKingdom = World.world.kingdoms_wild.get("possessed");
        if (TryCastSelectedSkill(actor, aim.Target, aim.TargetPos, attackKingdom, selectionArea)) return true;

        ShowTip("暂时无法释放主动能力");
        return false;
    }

    internal static bool CycleSelectedSkill(int direction = 1)
    {
        if (!TryGetControlledActor(out var actor)) return false;
        if (!TryCycleAttackAbility(actor, direction, out ActiveAbilityHandle ability))
        {
            ShowTip("没有可切换的主动能力");
            return false;
        }

        ShowTip($"当前能力：{GetAbilityName(actor.GetExtend(), ability)}");
        return true;
    }

    internal static ControlledSkillControlState GetState()
    {
        if (!TryGetControlledActor(out var actor)) return ControlledSkillControlState.Inactive;

        var hasSkill = TryGetSelectedAttackAbility(actor, out ActiveAbilityHandle selectedAbility, out int skillCount);
        return new ControlledSkillControlState(
            actor,
            hasSkill,
            skillCount,
            hasSkill ? GetAbilityName(actor.GetExtend(), selectedAbility) : string.Empty,
            skillCount > 1
        );
    }

    internal static bool TryGetControlledActor(out Actor actor)
    {
        actor = null;
        if (!ControllableUnit.isControllingUnit()) return false;

        actor = ControllableUnit.getControllableUnit();
        if (actor == null || actor.isRekt() || actor.asset.id == "crabzilla") return false;
        if (actor.is_unconscious || actor.asset.skip_fight_logic) return false;
        return true;
    }

    private static bool TryCycleAttackAbility(Actor actor, int direction, out ActiveAbilityHandle ability)
    {
        ability = default;
        using var candidates = new ListPool<ActiveAbilityHandle>();
        if (!TryCollectAvailableAttackAbilities(actor.GetExtend(), candidates)) return false;

        var count = candidates.Count;
        var next = GetSelectedAbilityIndex(actor);
        next = Mod(next + Math.Sign(direction == 0 ? 1 : direction), count);
        SelectedAbilityIndexes[actor.data.id] = next;
        ability = candidates[next];
        return true;
    }

    private static bool TryGetSelectedAttackAbility(Actor actor, out ActiveAbilityHandle ability)
    {
        return TryGetSelectedAttackAbility(actor, out ability, out _);
    }

    private static bool TryGetSelectedAttackAbility(
        Actor actor,
        out ActiveAbilityHandle ability,
        out int count)
    {
        ability = default;
        count = 0;
        using var candidates = new ListPool<ActiveAbilityHandle>();
        if (!TryCollectAvailableAttackAbilities(actor.GetExtend(), candidates)) return false;

        count = candidates.Count;
        var index = Mod(GetSelectedAbilityIndex(actor), count);
        SelectedAbilityIndexes[actor.data.id] = index;
        ability = candidates[index];
        return true;
    }

    private static bool TryCollectAvailableAttackAbilities(
        ActorExtend caster,
        IList<ActiveAbilityHandle> candidates)
    {
        candidates.Clear();
        if (!GeneralSettings.EnableSkillSystems || caster == null || caster.Base.isRekt()) return false;

        ActiveAbilityService.Collect(caster, candidates);
        for (int i = candidates.Count - 1; i >= 0; i--)
        {
            ActiveAbilityHandle candidate = candidates[i];
            if ((ActiveAbilityService.GetChannels(caster, candidate) & ActiveAbilityChannel.Combat) == 0 ||
                !ActiveAbilityService.CanPrepare(caster, candidate, null))
            {
                candidates.RemoveAt(i);
            }
        }

        return candidates.Count > 0;
    }

    private static bool CanUseSkillControlsNow(Actor actor)
    {
        if (!GeneralSettings.EnableSkillSystems) return false;
        if (actor == null || !actor.isAlive()) return false;
        if (actor.asset.skip_fight_logic || actor.is_unconscious) return false;
        if (!actor.isAttackReady()) return false;
        if (actor.isInWaterAndCantAttack()) return false;
        return actor.isAttackPossible();
    }

    private static bool TryCastSelectedSkill(Actor actor, BaseSimObject target, Vector3 targetPos,
        Kingdom attackKingdom, SkillTargetSelectionArea selectionArea)
    {
        if (!CanUseSkillControlsNow(actor)) return false;
        if (!TryGetSelectedAttackAbility(actor, out ActiveAbilityHandle ability)) return false;

        var caster = actor.GetExtend();
        var manualTargets = selectionArea.Active
            ? CollectManualTargets(actor, selectionArea, attackKingdom)
            : null;
        if (selectionArea.Active)
        {
            if ((target == null || target.isRekt() || !IsWithinAbilityRange(caster, ability, target))
                && manualTargets is { Count: > 0 })
            {
                target = manualTargets[0];
                targetPos = target.GetSimPos();
            }
            else if (target == null || target.isRekt())
            {
                targetPos = selectionArea.Center;
            }
        }

        var clampedTargetPos = ClampTargetPos(caster, ability, targetPos);
        var useTrackedTarget = target != null && !target.isRekt() && IsWithinAbilityRange(caster, ability, target);
        var abilityTarget = new ActiveAbilityTarget(
            useTrackedTarget ? target : null,
            useTrackedTarget ? target.GetSimPos() : clampedTargetPos,
            selectionArea,
            manualTargets,
            attackKingdom);
        if (!ActiveAbilityService.TryUse(caster, ability, abilityTarget, ActiveAbilityUseOrigin.Player)) return false;

        var aimPos = useTrackedTarget ? target.GetSimPos() : clampedTargetPos;
        actor.startAttackCooldown();
        actor.punchTargetAnimation(aimPos, true, actor.hasRangeAttack());
        actor.lookTowardsPosition(aimPos);
        actor.setPossessionAttackHappened();
        return true;
    }

    private static ControlledSkillAim ResolveAim(Actor actor)
    {
        var mousePos = (Vector3)World.world.getMousePos();
        mousePos.z = 0f;

        BaseSimObject target = GetActorTargetRaycast(actor, mousePos);
        target ??= GetActorTargetNearCursor(actor);
        target ??= GetBuildingTargetNearCursor();
        var targetPos = target == null ? mousePos : target.GetSimPos();
        targetPos = ClampTargetPos(actor.GetExtend(), targetPos);

        return new ControlledSkillAim(target, targetPos);
    }

    internal static Vector3 ClampSkillTargetPos(ActorExtend caster, Vector3 targetPos)
    {
        return caster == null || caster.Base.isRekt() ? targetPos : ClampTargetPos(caster, targetPos);
    }

    internal static List<BaseSimObject> CollectManualTargets(Actor actor, SkillTargetSelectionArea area,
        Kingdom attackKingdom)
    {
        var result = new List<BaseSimObject>();
        if (!area.Active || actor == null || actor.isRekt()) return result;

        var caster = actor.GetExtend();
        if (!TryGetSelectedAttackAbility(actor, out ActiveAbilityHandle ability)) return result;
        var center = ClampTargetPos(caster, area.Center);
        var radius = Mathf.Max(0.1f, area.Radius);
        foreach (var target in SkillUtils.IterEnemyInSphere(center, radius, actor, attackKingdom))
        {
            if (target == null || target.isRekt()) continue;
            if (target == actor) continue;
            if (!IsWithinAbilityRange(caster, ability, target)) continue;
            if (result.Contains(target)) continue;

            result.Add(target);
        }

        result.Sort((a, b) =>
        {
            var da = Toolbox.SquaredDistVec2Float(center, a.current_position);
            var db = Toolbox.SquaredDistVec2Float(center, b.current_position);
            return da.CompareTo(db);
        });
        return result;
    }

    private static Actor GetActorTargetRaycast(Actor actor, Vector2 targetPos)
    {
        var actorPos = actor.current_position;
        if (Toolbox.SquaredDistVec2Float(actorPos, targetPos) < 0.01f) return null;

        var tiles = PathfinderTools.raycast(actorPos, targetPos);
        var bestDistance = float.MaxValue;
        Actor target = null;

        foreach (var tile in tiles)
        {
            if (!tile.hasUnits()) continue;

            tile.doUnits(candidate =>
            {
                if (candidate == actor || candidate.isRekt()) return;

                var distance = Toolbox.SquaredDistVec2Float(actorPos, candidate.current_position);
                if (distance >= bestDistance) return;

                bestDistance = distance;
                target = candidate;
            });
            if (target != null) break;
        }

        return target;
    }

    private static Actor GetActorTargetNearCursor(Actor actor)
    {
        var target = World.world.getActorNearCursor();
        if (target == null || target == actor || target.isRekt()) return null;
        return target;
    }

    private static Building GetBuildingTargetNearCursor()
    {
        WorldTile tile = World.world.getMouseTilePosCachedFrame();
        Building target = tile?.building;
        return target == null || target.isRekt() ? null : target;
    }

    private static Vector3 ClampTargetPos(ActorExtend caster, Vector3 targetPos)
    {
        return TryGetSelectedAttackAbility(caster.Base, out ActiveAbilityHandle ability)
            ? ClampTargetPos(caster, ability, targetPos)
            : targetPos;
    }

    private static Vector3 ClampTargetPos(
        ActorExtend caster,
        ActiveAbilityHandle ability,
        Vector3 targetPos)
    {
        var sourcePos = caster.Base.GetSimPos();
        var delta = targetPos - sourcePos;
        var maxRange = ActiveAbilityService.ResolveRange(caster, ability);
        if (delta.sqrMagnitude <= maxRange * maxRange) return targetPos;
        if (delta.sqrMagnitude < 0.0001f) return sourcePos + Vector3.right * maxRange;
        return sourcePos + delta.normalized * maxRange;
    }

    private static bool IsWithinAbilityRange(
        ActorExtend caster,
        ActiveAbilityHandle ability,
        BaseSimObject target)
    {
        if (target == null) return false;
        var range = ActiveAbilityService.ResolveRange(caster, ability, target) + target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(caster.Base.current_position, target.current_position) <= range * range;
    }

    internal static float ResolveSelectedAbilityRange(ActorExtend caster)
    {
        return caster != null && !caster.Base.isRekt() &&
               TryGetSelectedAttackAbility(caster.Base, out ActiveAbilityHandle ability)
            ? ActiveAbilityService.ResolveRange(caster, ability)
            : 0f;
    }

    private static string GetAbilityName(ActorExtend caster, ActiveAbilityHandle ability)
    {
        string name = ActiveAbilityService.Describe(caster, ability).Name;
        return string.IsNullOrEmpty(name) ? "未知能力" : name;
    }

    private static int GetSelectedAbilityIndex(Actor actor)
    {
        return actor != null && SelectedAbilityIndexes.TryGetValue(actor.data.id, out var index) ? index : 0;
    }

    private static int Mod(int value, int divisor)
    {
        if (divisor <= 0) return 0;
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static void ShowTip(string text)
    {
        if (string.IsNullOrEmpty(text)) return;
        WorldTip.showNow(text, false, "top", 2.5f);
    }

    private readonly struct ControlledSkillAim
    {
        public readonly BaseSimObject Target;
        public readonly Vector3 TargetPos;

        public ControlledSkillAim(BaseSimObject target, Vector3 targetPos)
        {
            Target = target;
            TargetPos = targetPos;
        }
    }
}

internal readonly struct ControlledSkillControlState
{
    public static readonly ControlledSkillControlState Inactive = new();

    public readonly Actor Actor;
    public readonly bool HasSkill;
    public readonly int SkillCount;
    public readonly string SkillName;
    public readonly bool CanCycleSkill;

    public bool Active => Actor != null;

    public ControlledSkillControlState(Actor actor, bool hasSkill, int skillCount, string skillName,
        bool canCycleSkill)
    {
        Actor = actor;
        HasSkill = hasSkill;
        SkillCount = skillCount;
        SkillName = skillName;
        CanCycleSkill = canCycleSkill;
    }
}
