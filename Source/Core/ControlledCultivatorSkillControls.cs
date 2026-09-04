using System;
using System.Collections.Generic;
using Cultiway.Const;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.ActiveAbilities;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Effects;
using Cultiway.Core.SkillLibV3.Usage;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using strings;
using UnityEngine;

namespace Cultiway.Core;

internal static class ControlledCultivatorSkillControls
{
    private static readonly Dictionary<long, ActiveAbilityHandle> SelectedAbilities = new();

    internal static bool CastSelectedSkill()
    {
        return CastSelectedSkill(SkillTargetSelectionArea.Inactive);
    }

    internal static bool CastSelectedSkill(SkillTargetSelectionArea selectionArea)
    {
        if (!TryGetControlledActor(out var actor)) return false;

        if (!TryGetSelectedAttackAbility(actor, out ActiveAbilityHandle ability))
        {
            ShowTip("没有可释放的主动能力");
            return false;
        }

        ActiveAbilityControlState controlState =
            ActiveAbilityService.ResolveControlState(actor.GetExtend(), ability);
        if (!controlState.CanUse)
        {
            ShowBlockedState(controlState);
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

    /// <summary>让本体与临时魂体共享同一项玩家能力选择。</summary>
    private static long ResolveSelectionOwnerId(Actor actor)
    {
        if (actor == null) return 0L;
        SkillCasterContext context = SkillCasterContextService.Resolve(actor.GetExtend());
        return context.IsValid ? context.Owner.Base.data.id : actor.data.id;
    }

    private static bool TryCycleAttackAbility(Actor actor, int direction, out ActiveAbilityHandle ability)
    {
        ability = default;
        using var candidates = new ListPool<ActiveAbilityHandle>();
        int selectedIndex = CollectSelectableAbilities(actor, candidates);
        if (selectedIndex < 0) return false;

        int next = Mod(selectedIndex + Math.Sign(direction == 0 ? 1 : direction), candidates.Count);
        SelectedAbilities[ResolveSelectionOwnerId(actor)] = candidates[next];
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
        int selectedIndex = CollectSelectableAbilities(actor, candidates);
        if (selectedIndex < 0) return false;

        count = candidates.Count;
        ability = candidates[selectedIndex];
        return true;
    }

    /// <summary>按玩家控制使用的固定顺序收集全部当前可选能力，并返回当前选中下标。</summary>
    internal static int CollectSelectableAbilities(Actor actor, IList<ActiveAbilityHandle> candidates)
    {
        candidates.Clear();
        if (actor == null || actor.isRekt() ||
            !TryCollectAvailableAttackAbilities(actor.GetExtend(), candidates)) return -1;

        long selectionOwnerId = ResolveSelectionOwnerId(actor);
        if (SelectedAbilities.TryGetValue(selectionOwnerId, out ActiveAbilityHandle selected))
        {
            for (int i = 0; i < candidates.Count; i++)
            {
                if (candidates[i] == selected) return i;
            }
        }
        else
        {
            SelectedAbilities[selectionOwnerId] = candidates[0];
        }
        return 0;
    }

    /// <summary>从当前可选能力中直接选择指定句柄，供附体能力带点击使用。</summary>
    internal static bool SelectAbility(Actor actor, ActiveAbilityHandle ability)
    {
        if (!TryGetControlledActor(out Actor controlledActor) || controlledActor != actor) return false;

        using var candidates = new ListPool<ActiveAbilityHandle>();
        if (CollectSelectableAbilities(actor, candidates) < 0) return false;
        for (int i = 0; i < candidates.Count; i++)
        {
            if (candidates[i] != ability) continue;
            SelectedAbilities[ResolveSelectionOwnerId(actor)] = ability;
            return true;
        }
        return false;
    }

    /// <summary>按能力栏显示顺序选择前十个主动能力。</summary>
    internal static bool SelectAbilityAtIndex(int index)
    {
        if (index < 0 || !TryGetControlledActor(out Actor actor)) return false;

        using var candidates = new ListPool<ActiveAbilityHandle>();
        if (CollectSelectableAbilities(actor, candidates) < 0 || index >= candidates.Count) return false;
        ActiveAbilityHandle ability = candidates[index];
        SelectedAbilities[ResolveSelectionOwnerId(actor)] = ability;
        ShowTip($"当前能力：{GetAbilityName(actor.GetExtend(), ability)}");
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
            if ((ActiveAbilityService.GetChannels(caster, candidate) &
                 (ActiveAbilityChannel.Combat | ActiveAbilityChannel.World)) == 0)
            {
                candidates.RemoveAt(i);
            }
        }

        StableSortAbilities(caster, candidates);

        return candidates.Count > 0;
    }

    /// <summary>按攻击、防御、辅助、功能的固定顺序整理玩家能够轮换的主动能力。</summary>
    private static void StableSortAbilities(ActorExtend caster, IList<ActiveAbilityHandle> abilities)
    {
        for (int i = 1; i < abilities.Count; i++)
        {
            ActiveAbilityHandle value = abilities[i];
            int group = ResolveAbilityGroup(caster, value);
            int insert = i;
            while (insert > 0 && ResolveAbilityGroup(caster, abilities[insert - 1]) > group)
            {
                abilities[insert] = abilities[insert - 1];
                insert--;
            }
            abilities[insert] = value;
        }
    }

    /// <summary>优先使用技能本体类型，其他能力则依据目标关系和战术画像推断轮换分组。</summary>
    private static int ResolveAbilityGroup(ActorExtend caster, ActiveAbilityHandle ability)
    {
        if (!ability.Source.IsNull && ability.Source.HasComponent<SkillContainer>())
        {
            return (int)ability.Source.GetComponent<SkillContainer>().Asset.Type;
        }
        ActiveAbilityDescriptor descriptor = ActiveAbilityService.Describe(caster, ability);
        if (descriptor.TargetRelation == SkillUseTargetRelation.WorldTile) return (int)SkillEntityType.Utility;
        if (descriptor.TargetRelation == SkillUseTargetRelation.Friendly) return (int)SkillEntityType.Support;
        ActiveAbilityTacticalProfile tactical = ActiveAbilityService.ResolveTacticalProfile(caster, ability, null);
        if (tactical.Defensive > tactical.Offensive) return (int)SkillEntityType.Defense;
        if (tactical.Support > 0f) return (int)SkillEntityType.Support;
        return (int)SkillEntityType.Attack;
    }

    private static bool CanUseAbilityControlsNow(Actor actor, ActiveAbilityChannel channels)
    {
        if (!GeneralSettings.EnableSkillSystems) return false;
        if (actor == null || !actor.isAlive()) return false;
        if (actor.asset.skip_fight_logic || actor.is_unconscious) return false;
        if ((channels & ActiveAbilityChannel.World) != 0) return true;
        if (!actor.isAttackReady()) return false;
        if (actor.isInWaterAndCantAttack()) return false;
        return actor.isAttackPossible();
    }

    private static bool TryCastSelectedSkill(Actor actor, BaseSimObject target, Vector3 targetPos,
        Kingdom attackKingdom, SkillTargetSelectionArea selectionArea)
    {
        if (!TryGetSelectedAttackAbility(actor, out ActiveAbilityHandle ability)) return false;
        var caster = actor.GetExtend();
        ActiveAbilityChannel channels = ActiveAbilityService.GetChannels(caster, ability);
        if (!CanUseAbilityControlsNow(actor, channels)) return false;

        ActiveAbilityDescriptor descriptor = ActiveAbilityService.Describe(caster, ability);
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

        var clampedTargetPos = descriptor.TargetMode == ActiveAbilityTargetMode.Self
            ? actor.GetSimPos()
            : ClampTargetPos(caster, ability, targetPos);
        var useTrackedTarget = descriptor.TargetRelation != SkillUseTargetRelation.WorldTile &&
                               target != null && !target.isRekt() &&
                               MatchesTargetRelation(actor, target, descriptor.TargetRelation) &&
                               IsWithinAbilityRange(caster, ability, target);
        var abilityTarget = new ActiveAbilityTarget(
            useTrackedTarget ? target : null,
            useTrackedTarget ? target.GetSimPos() : clampedTargetPos,
            selectionArea,
            manualTargets,
            attackKingdom);
        if (!ActiveAbilityService.TryUse(caster, ability, abilityTarget, ActiveAbilityUseOrigin.Player)) return false;

        var aimPos = useTrackedTarget ? target.GetSimPos() : clampedTargetPos;
        if ((channels & ActiveAbilityChannel.Combat) == 0) return true;
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

        if (!TryGetSelectedAttackAbility(actor, out ActiveAbilityHandle ability))
            return new ControlledSkillAim(null, mousePos);
        ActiveAbilityDescriptor descriptor = ActiveAbilityService.Describe(actor.GetExtend(), ability);
        if (descriptor.TargetMode == ActiveAbilityTargetMode.Self)
            return new ControlledSkillAim(actor, actor.GetSimPos());
        if (descriptor.TargetRelation == SkillUseTargetRelation.WorldTile)
            return new ControlledSkillAim(null, ClampTargetPos(actor.GetExtend(), ability, mousePos));

        BaseSimObject target = GetActorTargetRaycast(actor, mousePos, descriptor.TargetRelation);
        target ??= GetActorTargetNearCursor(actor, descriptor.TargetRelation);
        if (descriptor.TargetRelation == SkillUseTargetRelation.Hostile)
        {
            BaseSimObject building = GetBuildingTargetNearCursor();
            if (MatchesTargetRelation(actor, building, descriptor.TargetRelation)) target ??= building;
        }
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
        ActiveAbilityDescriptor descriptor = ActiveAbilityService.Describe(caster, ability);
        var center = ClampTargetPos(caster, area.Center);
        var radius = Mathf.Max(0.1f, area.Radius);
        if (descriptor.TargetRelation == SkillUseTargetRelation.WorldTile) return result;
        if (descriptor.TargetRelation == SkillUseTargetRelation.Self)
        {
            result.Add(actor);
            return result;
        }

        if (descriptor.TargetRelation == SkillUseTargetRelation.Friendly)
        {
            CollectFriendlyTargets(actor, caster, ability, center, radius, result);
        }
        else
        {
            foreach (var target in SkillUtils.IterEnemyInSphere(center, radius, actor, attackKingdom))
            {
                if (target == null || target.isRekt() || target == actor) continue;
                if (!IsWithinAbilityRange(caster, ability, target)) continue;
                if (!result.Contains(target)) result.Add(target);
            }
        }

        result.Sort((a, b) =>
        {
            var da = Toolbox.SquaredDistVec2Float(center, a.current_position);
            var db = Toolbox.SquaredDistVec2Float(center, b.current_position);
            return da.CompareTo(db);
        });
        return result;
    }

    /// <summary>收集选区内与施法者同国、同联盟或为施法者本人的全部单位。</summary>
    private static void CollectFriendlyTargets(
        Actor actor,
        ActorExtend caster,
        ActiveAbilityHandle ability,
        Vector3 center,
        float radius,
        ICollection<BaseSimObject> output)
    {
        float radiusSquared = radius * radius;
        WorldTile centerTile = World.world.GetTile(Mathf.FloorToInt(center.x), Mathf.FloorToInt(center.y));
        if (centerTile == null) return;
        int chunkRadius = Mathf.CeilToInt(radius / 16f) + 1;
        foreach (Actor candidate in Finder.getUnitsFromChunk(centerTile, chunkRadius))
        {
            if (candidate == null || candidate.isRekt() ||
                !SkillTargetRelationResolver.IsFriendly(actor, candidate) ||
                Toolbox.SquaredDistVec2Float(center, candidate.current_position) > radiusSquared ||
                !IsWithinAbilityRange(caster, ability, candidate) || output.Contains(candidate)) continue;
            output.Add(candidate);
        }
    }

    private static Actor GetActorTargetRaycast(
        Actor actor,
        Vector2 targetPos,
        SkillUseTargetRelation relation)
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
                if (candidate.isRekt() || !MatchesTargetRelation(actor, candidate, relation)) return;

                var distance = Toolbox.SquaredDistVec2Float(actorPos, candidate.current_position);
                if (distance >= bestDistance) return;

                bestDistance = distance;
                target = candidate;
            });
            if (target != null) break;
        }

        return target;
    }

    private static Actor GetActorTargetNearCursor(Actor actor, SkillUseTargetRelation relation)
    {
        var target = World.world.getActorNearCursor();
        return target == null || target.isRekt() || !MatchesTargetRelation(actor, target, relation)
            ? null
            : target;
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

    internal static float ResolveSelectedAbilityEffectRadius(ActorExtend caster)
    {
        return caster != null && !caster.Base.isRekt() &&
               TryGetSelectedAttackAbility(caster.Base, out ActiveAbilityHandle ability)
            ? ActiveAbilityService.ResolveEffectRadius(caster, ability)
            : 0f;
    }

    /// <summary>返回玩家当前选中能力的稳定目标描述。</summary>
    internal static bool TryDescribeSelectedAbility(
        ActorExtend caster,
        out ActiveAbilityHandle ability,
        out ActiveAbilityDescriptor descriptor)
    {
        ability = default;
        descriptor = default;
        if (caster == null || caster.Base.isRekt() ||
            !TryGetSelectedAttackAbility(caster.Base, out ability)) return false;
        descriptor = ActiveAbilityService.Describe(caster, ability);
        return true;
    }

    /// <summary>使用技能真实的结构化地块预检收集当前世界能力的逐格预览。</summary>
    internal static void CollectSelectedTilePreview(
        ActorExtend caster,
        Vector3 center,
        float radius,
        ICollection<SkillTilePreviewEntry> output)
    {
        output.Clear();
        if (!TryDescribeSelectedAbility(caster, out ActiveAbilityHandle ability, out ActiveAbilityDescriptor descriptor) ||
            descriptor.TargetRelation != SkillUseTargetRelation.WorldTile || ability.Source.IsNull ||
            !ability.Source.HasComponent<SkillContainer>()) return;
        SkillEffectResolver.CollectTilePreview(caster, ability.Source, center, radius, output);
    }

    /// <summary>按主动能力声明的关系判断玩家光标下对象是否是合法目标。</summary>
    private static bool MatchesTargetRelation(
        Actor source,
        BaseSimObject target,
        SkillUseTargetRelation relation)
    {
        return SkillTargetRelationResolver.Matches(relation, source, target);
    }

    private static string GetAbilityName(ActorExtend caster, ActiveAbilityHandle ability)
    {
        string name = ActiveAbilityService.Describe(caster, ability).Name;
        return string.IsNullOrEmpty(name) ? "未知能力" : name;
    }

    private static int Mod(int value, int divisor)
    {
        if (divisor <= 0) return 0;
        var result = value % divisor;
        return result < 0 ? result + divisor : result;
    }

    private static void ShowBlockedState(ActiveAbilityControlState state)
    {
        if (state.IsActive)
        {
            ShowTip("cultiway_control_ability_state_active".Localize());
            return;
        }

        string text = state.BlockReason switch
        {
            ActiveAbilityControlBlockReason.Cooldown => string.Format(
                "cultiway_control_ability_state_cooldown".Localize(),
                Mathf.CeilToInt(state.CooldownRemaining)),
            ActiveAbilityControlBlockReason.InsufficientResource =>
                "cultiway_control_ability_state_resource".Localize(),
            _ => "cultiway_control_ability_state_unavailable".Localize(),
        };
        ShowTip(text);
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
