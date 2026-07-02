using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using strings;
using UnityEngine;

namespace Cultiway.Core;

internal static class ControlledCultivatorSkillControls
{
    private static readonly Dictionary<long, int> SelectedSkillIndexes = new();

    internal static bool CastSelectedSkill()
    {
        return CastSelectedSkill(SkillTargetSelectionArea.Inactive);
    }

    internal static bool CastSelectedSkill(SkillTargetSelectionArea selectionArea)
    {
        if (!TryGetControlledActor(out var actor)) return false;

        if (!TryGetSelectedAttackSkill(actor, out _))
        {
            ShowTip("没有可释放的法术");
            return false;
        }

        var aim = ResolveAim(actor);
        var attackKingdom = World.world.kingdoms_wild.get("possessed");
        if (TryCastSelectedSkill(actor, aim.Target, aim.TargetPos, attackKingdom, selectionArea)) return true;

        ShowTip("暂时无法释放法术");
        return false;
    }

    internal static bool CycleSelectedSkill(int direction = 1)
    {
        if (!TryGetControlledActor(out var actor)) return false;
        if (!TryCycleAttackSkill(actor, direction, out var skill))
        {
            ShowTip("没有可切换的法术");
            return false;
        }

        ShowTip($"当前法术：{GetSkillName(skill)}");
        return true;
    }

    internal static ControlledSkillControlState GetState()
    {
        if (!TryGetControlledActor(out var actor)) return ControlledSkillControlState.Inactive;

        var hasSkill = TryGetSelectedAttackSkill(actor, out var selectedSkill, out var skillCount);
        return new ControlledSkillControlState(
            actor,
            hasSkill,
            skillCount,
            hasSkill ? GetSkillName(selectedSkill) : string.Empty,
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

    private static bool TryCycleAttackSkill(Actor actor, int direction, out Entity skill)
    {
        skill = default;
        if (!TryCollectAvailableAttackSkills(actor.GetExtend(), out var candidates)) return false;

        var count = candidates.Count;
        var next = GetSelectedSkillIndex(actor);
        next = Mod(next + Math.Sign(direction == 0 ? 1 : direction), count);
        SelectedSkillIndexes[actor.data.id] = next;
        skill = candidates[next];
        return true;
    }

    private static bool TryGetSelectedAttackSkill(Actor actor, out Entity skill)
    {
        return TryGetSelectedAttackSkill(actor, out skill, out _);
    }

    private static bool TryGetSelectedAttackSkill(Actor actor, out Entity skill, out int count)
    {
        skill = default;
        count = 0;
        if (!TryCollectAvailableAttackSkills(actor.GetExtend(), out var candidates)) return false;

        count = candidates.Count;
        var index = Mod(GetSelectedSkillIndex(actor), count);
        SelectedSkillIndexes[actor.data.id] = index;
        skill = candidates[index];
        return true;
    }

    private static bool TryCollectAvailableAttackSkills(ActorExtend caster, out List<Entity> candidates)
    {
        candidates = null;
        if (!GeneralSettings.EnableSkillSystems) return false;
        if (caster?.all_attack_skills == null || caster.all_attack_skills.Count == 0) return false;

        candidates = new List<Entity>();
        foreach (var candidate in caster.all_attack_skills)
        {
            if (CanPreparePointCast(caster, candidate))
            {
                candidates.Add(candidate);
            }
        }

        return candidates.Count > 0;
    }

    private static bool CanPreparePointCast(ActorExtend caster, Entity skill)
    {
        if (caster == null || caster.Base.isRekt()) return false;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;

        var stepLimit = SkillCastCost.GetAffordableStepLimit(caster, skill);
        var probePlan = SkillCastPlanner.CreatePointPlan(caster, skill, caster.Base.GetSimPos() + Vector3.right,
            stepLimit);
        return SkillCastCost.CanPay(caster, skill, probePlan);
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
        if (!TryGetSelectedAttackSkill(actor, out var skill)) return false;

        var caster = actor.GetExtend();
        var manualTargets = selectionArea.Active
            ? CollectManualTargets(actor, selectionArea, attackKingdom)
            : null;
        if (selectionArea.Active)
        {
            if ((target == null || target.isRekt() || !IsWithinSkillCastRange(caster, target))
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

        var clampedTargetPos = ClampTargetPos(caster, targetPos);
        var useTrackedTarget = target != null && !target.isRekt() && IsWithinSkillCastRange(caster, target);
        var stepLimit = SkillCastCost.GetAffordableStepLimit(caster, skill);
        var plan = useTrackedTarget
            ? SkillCastPlanner.CreatePlan(caster, skill, target, stepLimit, manualTargets,
                selectionArea.Active)
            : SkillCastPlanner.CreatePointPlan(caster, skill, clampedTargetPos, stepLimit);
        if (plan.Steps.Count == 0) return false;

        if (!ModClass.I.SkillV3.StartSkillSequence(caster, skill, plan, 100, caster.GetPowerLevel(),
                SkillCastCostSource.CasterWakan, attackKingdom))
        {
            return false;
        }

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

        var target = GetActorTargetRaycast(actor, mousePos) ?? GetActorTargetNearCursor(actor);
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
        var center = ClampTargetPos(caster, area.Center);
        var radius = Mathf.Max(0.1f, area.Radius);
        foreach (var target in SkillUtils.IterEnemyInSphere(center, radius, actor, attackKingdom))
        {
            if (target == null || target.isRekt()) continue;
            if (target == actor) continue;
            if (!IsWithinSkillCastRange(caster, target)) continue;
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

    private static Vector3 ClampTargetPos(ActorExtend caster, Vector3 targetPos)
    {
        var sourcePos = caster.Base.GetSimPos();
        var delta = targetPos - sourcePos;
        var maxRange = caster.GetSkillCastRange(null);
        if (delta.sqrMagnitude <= maxRange * maxRange) return targetPos;
        if (delta.sqrMagnitude < 0.0001f) return sourcePos + Vector3.right * maxRange;
        return sourcePos + delta.normalized * maxRange;
    }

    private static bool IsWithinSkillCastRange(ActorExtend caster, BaseSimObject target)
    {
        if (target == null) return false;
        var range = caster.GetSkillCastRange(target) + target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(caster.Base.current_position, target.current_position) <= range * range;
    }

    private static string GetSkillName(Entity skill)
    {
        if (skill.IsNull) return "未知法术";
        if (skill.HasName) return skill.Name.value;
        if (!skill.HasComponent<SkillContainer>()) return "未知法术";

        var skillId = skill.GetComponent<SkillContainer>().SkillEntityAssetID;
        return string.IsNullOrEmpty(skillId) ? "未知法术" : skillId.Localize();
    }

    private static int GetSelectedSkillIndex(Actor actor)
    {
        return actor != null && SelectedSkillIndexes.TryGetValue(actor.data.id, out var index) ? index : 0;
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
        public readonly Actor Target;
        public readonly Vector3 TargetPos;

        public ControlledSkillAim(Actor target, Vector3 targetPos)
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
