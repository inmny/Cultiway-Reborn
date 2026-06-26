using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Core.Components;
using NeoModLoader.api.attributes;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using strings;
using UnityEngine;

namespace Cultiway.Core;

public class ActorExtend : ExtendComponent<Actor>, IHasInventory, IHasStatus, IHasForce, IDisposable
{
    private static Action<ActorExtend> action_on_new_creature;

    private static   Action<ActorExtend> action_on_update_stats;
    private readonly HashSet<Entity> _learned_skills_v3 = new();
    private Entity          e;
    public HashSet<Entity> all_skills = new();
    public List<Entity> all_attack_skills = new();
    private  Dictionary<string, Entity> _skill_action_modifiers = new();
    private  Dictionary<string, Entity> _skill_entity_modifiers = new();
    internal float[]         s_armor        = new float[9];
    public Sect sect;

    private Dictionary<Type, Dictionary<IDeleteWhenUnknown, float>>  _master_items = new();
    public void Master<T>(T item, float value) where T : Asset, IDeleteWhenUnknown
    {
        if (!_master_items.TryGetValue(typeof(T), out var dict))
        {
            dict = new Dictionary<IDeleteWhenUnknown, float>();
            _master_items.Add(typeof(T), dict);
        }

        if (!dict.ContainsKey(item))
        {
            item.Current++;
        }
        dict[item] = value;
    }
    public void DeMaster<T>(T item) where T : Asset, IDeleteWhenUnknown
    {
        if (!_master_items.TryGetValue(typeof(T), out var dict))
        {
            return;
        }

        if (dict.ContainsKey(item))
        {
            item.Current--;
            dict.Remove(item);
        }
    }

    public bool HasMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) && dict.Count > 0;
    }

    public float GetMaster<T>(T item) where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) ? (dict.TryGetValue(item, out var value) ? value : 0) : 0;
    }

    public IEnumerable<(T, float)> GetAllMaster<T>() where T : Asset, IDeleteWhenUnknown
    {
        return _master_items.TryGetValue(typeof(T), out var dict) ? dict.Select(x => ((T)x.Key, x.Value)) : Array.Empty<(T, float)>();
    }
    public ActorExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<ActorBinder>()._ae = this;
        _ = e.GetComponent<ActorBinder>().Actor;
    }

    public void Dispose()
    {
        if (!e.IsNull)
        {
            ModClass.I.CommandBuffer.AddTag<TagRecycle>(e.Id);
            //ModClass.LogInfo($"Disposing ActorExtend for Actor {Base.data.id} ({e})");
            foreach (var item in GetItems())
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(item.Id);
            }
            ModClass.I.ActorExtendManager.Remove(Base);
        }

        if (_master_items != null)
        {
            foreach (var items in _master_items.Values)
            {
                if (items != null)
                {
                    foreach (var item in items.Keys)
                    {
                        item.Current--;
                    }
                }
            }
            _master_items = null;
        }

        if (_learned_skills_v3 != null && _learned_skills_v3.Count > 0)
        {
            _learned_skills_v3.Clear();
        }
        all_skills = null;
        all_attack_skills = null;

        if (_skill_action_modifiers != null)
        {
            foreach (var skill_action in _skill_action_modifiers.Values)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skill_action.Id);
            }

            _skill_action_modifiers = null;
        }
        if (_skill_entity_modifiers != null)
        {
            foreach (var skill_entity in _skill_entity_modifiers.Values)
            {
                ModClass.I.CommandBuffer.AddTag<TagRecycle>(skill_entity.Id);
            }

            _skill_entity_modifiers = null;
        }
    }

    public override Entity E => e;
    public override Actor Base => e.HasComponent<ActorBinder>() ? e.GetComponent<ActorBinder>().Actor : null;
    public bool TryGetComponent<TComponent>(out TComponent component) where TComponent : struct, IComponent
    {
        return e.TryGetComponent(out component);
    }
    public ref TComponent GetOrAddComponent<TComponent>() where TComponent : struct, IComponent
    {
        if (!e.HasComponent<TComponent>())
        {
            e.AddComponent(new TComponent());
        }
        return ref e.GetComponent<TComponent>();
    }
    public static void RegisterCombatActionOnAttack(Action<ActorExtend, BaseSimObject, ListPool<CombatActionAsset>> action)
    {
        action_on_attack += action;
    }
    private static Action<ActorExtend, BaseSimObject, ListPool<CombatActionAsset>> action_on_attack;
    private const float BaseSkillCastRange = 11f;
    private const float CloseCombatCasterChance = 0.25f;
    private const float CasterPreferredRangeRatio = 0.65f;

    [Hotfixable]
    public bool TryToAttack(BaseSimObject target, Action kill_action = null, float bonus_area_effect = 0, bool do_checks = true)
    {
        var actor = Base;
        if (do_checks)
        {
            if (actor.isInWaterAndCantAttack()) return false;
            if (!actor.isAttackPossible()) return false;
            if (target != null && !CanUseCombatActionAtCurrentDistance(target)) return false;
        }
        if (target.isRekt()) return false;
        if (actor.kingdom == null)
        {
            ModClass.LogError($"Actor {actor.id}({(actor.isActor() ? actor.a.asset.id : actor.b.asset.id)}) has no kingdom");
        }
        CombatActionAsset basic_attack_action = null;
        
        using var attack_action_pool = new ListPool<CombatActionAsset>();
        // 加入普攻
        if (CanUseMeleeAttackAtCurrentDistance(target))
        {
            basic_attack_action = WorldboxGame.CombatActions.AttackMelee;
        }
        else if (CanUseRangeAttackAtCurrentDistance(target))
        {
            basic_attack_action = WorldboxGame.CombatActions.AttackRange;
        }
        
        if (basic_attack_action != null) basic_attack_action.AddToPool(attack_action_pool);
        // 加入原版技能
        if (CanUseVanillaSpellAtCurrentDistance(target))
        {
            WorldboxGame.CombatActions.CastVanillaSpell.AddToPool(attack_action_pool);
        }
        // 加入自定义技能
        var castable_skill_count = CountCastableAttackSkills(target);
        if (castable_skill_count > 0)
        {
            WorldboxGame.CombatActions.CastSkillV3.AddToPool(attack_action_pool, castable_skill_count);
        }
        action_on_attack?.Invoke(this, target, attack_action_pool);
        

        float target_size = target.stats[S.size];
        Vector3 target_pos = new Vector3(target.current_position.x, target.current_position.y);
        if (target.isActor() && target.a.is_moving && target.isFlying())
        {
            target_pos = Vector3.MoveTowards(target_pos, target.a.next_step_position, target_size * 3f);
        }
        float dist = Vector2.Distance(actor.current_position, target.current_position) + target.getHeight();
        Vector3 new_point = Toolbox.getNewPoint(actor.current_position.x, actor.current_position.y, target_pos.x, target_pos.y, dist - target_size, true);

        AttackData attack_data = new(actor, target.current_tile, new_point, actor.current_position, target, actor.kingdom, AttackType.Weapon,
            actor.haveMetallicWeapon(), true, actor.hasRangeAttack(), actor.getWeaponAsset().projectile, kill_action, bonus_area_effect);
        
        if (!attack_action_pool.Any() && basic_attack_action == null) return false;

        actor.startAttackCooldown();
        actor.punchTargetAnimation(target.current_position, true, actor.hasRangeAttack());

        CombatActionAsset combatAction = null;
        bool combatActionDone = false;
        if (attack_action_pool.Any())
        {
            // 随机选择一个攻击动作
            combatAction = attack_action_pool.GetRandom();
            combatActionDone = combatAction.action(attack_data);
            if (!combatActionDone && !combatAction.basic && basic_attack_action != null)
            {
                // 如果不是普攻且失败了，那就尝试普攻
                combatAction = basic_attack_action;
                combatActionDone = combatAction.action(attack_data);
            }
        }
        else
        {
            combatAction = basic_attack_action;
            combatActionDone = combatAction.action(attack_data);
        }
        
        finishAttackAttempt(combatAction, combatActionDone);
        return true;

        [Hotfixable]
        void finishAttackAttempt(CombatActionAsset combatActionAsset, bool combatActionSucceeded)
        {
            if (combatActionSucceeded)
            {
                actor.spendStamina(combatActionAsset.cost_stamina);
                actor.spendMana(combatActionAsset.cost_mana);
            }
            if (combatActionAsset.play_unit_attack_sounds && actor.asset.has_sound_attack)
            {
                MusicBox.playSound(actor.asset.sound_attack, actor.current_tile.x, actor.current_tile.y);
            }
            if (actor.needsFood() && Randy.randomBool())
            {
                actor.decreaseNutrition();
            }
            // TODO: 后坐力
        }
    }

    /// <summary>
    /// 判断当前距离下是否能立即执行任意战斗动作。
    /// </summary>
    /// <param name="target">当前攻击目标。</param>
    /// <returns>近战、远程武器、法术、技能或符箓中任一项可用时返回 true。</returns>
    public bool CanUseCombatActionAtCurrentDistance(BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        return CanUsePhysicalAttackAtCurrentDistance(target) || CanUseMagicActionAtCurrentDistance(target);
    }

    /// <summary>
    /// 判断是否应该继续保留当前战斗目标。
    /// </summary>
    /// <param name="target">当前攻击目标。</param>
    /// <returns>已经能物理攻击，或可通过施法范围/可接近路径处理目标时返回 true。</returns>
    public bool CanKeepCombatTarget(BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        return CanUsePhysicalAttackAtCurrentDistance(target) || CanKeepMagicCombatTarget(target);
    }

    /// <summary>
    /// 判断当前距离下是否能立即执行修士侧的法术类动作。
    /// </summary>
    /// <param name="target">当前攻击目标。</param>
    /// <returns>原版法术、自定义攻击技能或符箓可用时返回 true。</returns>
    public bool CanUseMagicActionAtCurrentDistance(BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        if (!IsWithinSkillCastRange(target)) return false;
        if (!IsAtPreferredSkillCombatDistance(target)) return false;

        return CanUseVanillaSpellAtCurrentDistance(target)
               || CountCastableAttackSkills(target) > 0
               || HasCastableTalisman(target);
    }

    /// <summary>
    /// 判断指定技能容器在当前距离下是否可以释放。
    /// </summary>
    /// <param name="skill">技能容器实体。</param>
    /// <param name="target">当前攻击目标。</param>
    /// <returns>目标、距离和灵气消耗都满足时返回 true。</returns>
    public bool CanUseSkillContainerAtCurrentDistance(Entity skill, BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        if (!IsWithinSkillCastRange(target)) return false;
        if (!IsAtPreferredSkillCombatDistance(target)) return false;
        return CanCastSkillContainer(skill, target);
    }

    /// <summary>
    /// 从当前距离下可释放的攻击技能中随机选取一个。
    /// </summary>
    /// <param name="target">当前攻击目标。</param>
    /// <param name="skill">选中的技能实体。</param>
    /// <returns>存在可释放攻击技能时返回 true。</returns>
    public bool TryGetCastableAttackSkill(BaseSimObject target, out Entity skill)
    {
        using var pool = new ListPool<Entity>();
        foreach (var candidate in all_attack_skills)
        {
            if (!CanUseSkillContainerAtCurrentDistance(candidate, target)) continue;
            pool.Add(candidate);
        }

        skill = pool.Any() ? pool.GetRandom() : default;
        return !skill.IsNull;
    }

    /// <summary>
    /// 判断当前距离下是否能执行原版物理攻击。
    /// </summary>
    private bool CanUsePhysicalAttackAtCurrentDistance(BaseSimObject target)
    {
        return CanUseMeleeAttackAtCurrentDistance(target) || CanUseRangeAttackAtCurrentDistance(target);
    }

    /// <summary>
    /// 判断当前距离下是否能执行原版近战攻击。
    /// </summary>
    private bool CanUseMeleeAttackAtCurrentDistance(BaseSimObject target)
    {
        if (target == null || !Base.hasMeleeAttack()) return false;
        if (target.position_height > 0f) return false;
        return Base.isInAttackRange(target);
    }

    /// <summary>
    /// 判断当前距离下是否能执行原版远程武器攻击。
    /// </summary>
    private bool CanUseRangeAttackAtCurrentDistance(BaseSimObject target)
    {
        if (target == null || !Base.hasRangeAttack()) return false;
        return Base.isInAttackRange(target);
    }

    /// <summary>
    /// 判断当前距离下是否能执行原版法术动作。
    /// </summary>
    private bool CanUseVanillaSpellAtCurrentDistance(BaseSimObject target)
    {
        if (target == null) return false;
        return Base.hasSpells() && Base.canUseSpells() && IsWithinSkillCastRange(target) &&
               IsAtPreferredSkillCombatDistance(target);
    }

    /// <summary>
    /// 判断法术类动作是否足以让行为树继续保留当前目标。
    /// </summary>
    private bool CanKeepMagicCombatTarget(BaseSimObject target)
    {
        if (!HasAnyMagicAction(target)) return false;
        if (IsWithinSkillCastRange(target)) return true;
        return CanApproachTargetForMagic(target);
    }

    /// <summary>
    /// 统计当前距离下可释放的自定义攻击技能数量。
    /// </summary>
    private int CountCastableAttackSkills(BaseSimObject target)
    {
        if (!GeneralSettings.EnableSkillSystems) return 0;

        var count = 0;
        foreach (var skill in all_attack_skills)
        {
            if (CanCastSkillContainer(skill, target)) count++;
        }

        return count;
    }

    /// <summary>
    /// 统计可准备的自定义攻击技能数量，不要求目标已经进入施法距离。
    /// </summary>
    private int CountAvailableAttackSkills(BaseSimObject target)
    {
        if (!GeneralSettings.EnableSkillSystems) return 0;

        var count = 0;
        foreach (var skill in all_attack_skills)
        {
            if (CanPrepareSkillContainer(skill, target)) count++;
        }

        return count;
    }

    /// <summary>
    /// 判断技能容器是否满足释放前置条件并且目标在最大施法距离内。
    /// </summary>
    private bool CanCastSkillContainer(Entity skill, BaseSimObject target)
    {
        if (!CanPrepareSkillContainer(skill, target)) return false;
        return IsWithinSkillCastRange(target);
    }

    /// <summary>
    /// 判断技能容器是否具备准备释放的基础条件。
    /// </summary>
    private bool CanPrepareSkillContainer(Entity skill, BaseSimObject target)
    {
        if (!GeneralSettings.EnableSkillSystems) return false;
        if (skill.IsNull || !skill.HasComponent<SkillContainer>()) return false;
        if (target == null || target.isRekt()) return false;
        return SkillCastCost.CanAffordStepWakan(this, skill);
    }

    /// <summary>
    /// 判断是否存在当前距离下可释放的符箓。
    /// </summary>
    private bool HasCastableTalisman(BaseSimObject target)
    {
        foreach (var item in GetItems())
        {
            if (!item.HasComponent<Talisman>()) continue;
            ref var talisman = ref item.GetComponent<Talisman>();
            if (CanCastSkillContainer(talisman.SkillContainer, target)) return true;
        }

        return false;
    }

    /// <summary>
    /// 判断是否存在可准备释放的符箓，不要求目标已经进入施法距离。
    /// </summary>
    private bool HasAvailableTalisman(BaseSimObject target)
    {
        foreach (var item in GetItems())
        {
            if (!item.HasComponent<Talisman>()) continue;
            ref var talisman = ref item.GetComponent<Talisman>();
            if (CanPrepareSkillContainer(talisman.SkillContainer, target)) return true;
        }

        return false;
    }

    /// <summary>
    /// 判断目标是否在技能最大施法距离内。
    /// </summary>
    private bool IsWithinSkillCastRange(BaseSimObject target)
    {
        if (target == null) return false;
        var range = GetSkillCastRange(target) + target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(Base.current_position, target.current_position) <= range * range;
    }

    /// <summary>
    /// 判断目标是否进入当前战斗风格期望的出手距离。
    /// </summary>
    private bool IsAtPreferredSkillCombatDistance(BaseSimObject target)
    {
        if (target == null) return false;
        var range = GetDesiredCombatDistance(target) + target.stats[S.size];
        return Toolbox.SquaredDistVec2Float(Base.current_position, target.current_position) <= range * range;
    }

    /// <summary>
    /// 计算独立于原版物理攻击范围的技能最大施法距离。
    /// </summary>
    private float GetSkillCastRange(BaseSimObject target)
    {
        var power_bonus = Mathf.Clamp(GetPowerLevel(), 0f, 10f) * 0.5f;
        return BaseSkillCastRange + power_bonus;
    }

    /// <summary>
    /// 计算单位对当前目标实际想要靠近到的出手距离。
    /// </summary>
    private float GetDesiredCombatDistance(BaseSimObject target)
    {
        var physical_range = Base.getAttackRange();
        var skill_range = GetSkillCastRange(target);
        if (!Base.hasMeleeAttack()) return skill_range;
        if (!HasAnyMagicAction(target)) return physical_range;
        if (!CanPreferPhysicalCombatDistance(target)) return skill_range;

        var caster_chance = GetCasterCombatStyleChance(target);
        if (StableCombatRoll(Base, target) > caster_chance) return physical_range;

        return Mathf.Lerp(physical_range, skill_range, CasterPreferredRangeRatio);
    }

    /// <summary>
    /// 判断当前目标是否适合让单位选择近战距离作为期望距离。
    /// </summary>
    private bool CanPreferPhysicalCombatDistance(BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        if (!Base.hasMeleeAttack()) return false;
        if (target.position_height > 0f) return false;
        if (Base.isWaterCreature())
        {
            if (!target.isInLiquid() && !Base.asset.force_land_creature) return false;
        }
        else if (target.isInLiquid())
        {
            return false;
        }

        return IsTargetOnSameReachableIsland(target);
    }

    /// <summary>
    /// 判断单位是否能为了施法继续向目标所在位置靠近。
    /// </summary>
    private bool CanApproachTargetForMagic(BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        return IsTargetOnSameReachableIsland(target);
    }

    /// <summary>
    /// 判断目标当前位置是否处在单位可正常寻路接近的同一岛屿上。
    /// </summary>
    private bool IsTargetOnSameReachableIsland(BaseSimObject target)
    {
        if (target == null || target.current_tile == null || Base.current_tile == null) return false;
        if (target.isActor() && target.a.isInsideSomething()) return false;
        return target.current_tile.isSameIsland(Base.current_tile);
    }

    /// <summary>
    /// 判断单位是否拥有可准备的法术类动作。
    /// </summary>
    private bool HasAnyMagicAction(BaseSimObject target)
    {
        if (target == null || target.isRekt()) return false;
        if (Base.hasSpells() && Base.canUseSpells()) return true;
        return CountAvailableAttackSkills(target) > 0 || HasAvailableTalisman(target);
    }

    /// <summary>
    /// 计算当前单位对目标采用偏施法距离作战的概率。
    /// </summary>
    private float GetCasterCombatStyleChance(BaseSimObject target)
    {
        var chance = CloseCombatCasterChance;
        if (!Base.hasWeapon()) chance += 0.25f;
        chance += Mathf.Clamp(all_attack_skills.Count, 0, 8) * 0.03f;
        chance += Mathf.Clamp01(GetPowerLevel() / 10f) * 0.1f;

        if (target?.isActor() ?? false)
        {
            var threat = target.a.GetExtend().GetPowerLevel() - GetPowerLevel();
            chance += Mathf.Clamp(threat, -2f, 4f) * 0.08f;
        }

        return Mathf.Clamp01(chance);
    }

    /// <summary>
    /// 为 actor-target 对生成稳定随机值，避免同一对目标每帧切换战斗风格。
    /// </summary>
    private static float StableCombatRoll(Actor actor, BaseSimObject target)
    {
        unchecked
        {
            var hash = actor.data.id * 73856093L ^ target.getID() * 19349663L;
            hash = (hash << 13) ^ hash;
            var value = (hash * (hash * hash * 15731L + 789221L) + 1376312589L) & 0x7fffffff;
            return value / (float)0x7fffffff;
        }
    }

    public void GetForce(BaseSimObject source, float x, float y, float z)
    {
        var actor = Base;
        if (!actor.asset.can_be_moved_by_powers)
        {
            return;
        }
        if (actor.position_height > 0f)
        {
            return;
        }
        var power_level = GetPowerLevel();
        var source_power_level = (source?.isActor()??false) ? (source.isRekt() ? 0 : source.a.GetExtend().GetPowerLevel()) : 0;
        if (power_level > source_power_level)
        {
            x /= Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level - source_power_level);
            y /= Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level - source_power_level);
            z /= Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level - source_power_level);
        }
        var reduction = Base.stats[S.knockback_reduction];
        if (reduction >= 0)
        {
            var ratio = 1 / (1 + reduction);
            x *= ratio;
            y *= ratio;
            z *= ratio;
        }
        else
        {
            var ratio = 1 - reduction;
            x *= ratio;
            y *= ratio;
            z *= ratio;
        }

        if (x * x + y * y + z * z > 1)
        {
            actor.velocity.x = x;
            actor.velocity.y = y;
            actor.velocity.z = z;
            actor.velocity_speed = z;
            actor.under_forces = true;
        }
    }
    public float GetCultisysLevelForSort<T>() where T : struct, ICultisysComponent
    {
        return e.TryGetComponent(out T cultisys) ? cultisys.Asset.GetLevelForSort(this, cultisys.CurrLevel) : -1;
    }
    public float GetPowerLevel()
    {
        return E.TryGetComponent(out PowerLevel power_level) ? power_level.value : 0;
    }

    public void UpgradePowerLevel(float min_level)
    {
        if (E.HasComponent<PowerLevel>())
        {
            ref var power_level = ref E.GetComponent<PowerLevel>();
            power_level.value = Mathf.Max(power_level.value, min_level);
        }
        else
        {
            E.AddComponent(new PowerLevel { value = min_level });
        }
    }
    public void AddSpecialItem(Entity item)
    {
        item.GetIncomingLinks<InventoryRelation>().Entities
            .Do(owner => owner.RemoveRelation<InventoryRelation>(item));
        e.AddRelation(new InventoryRelation { item = item });
    }

    public void ExtractSpecialItem(Entity item)
    {
        e.RemoveRelation<InventoryRelation>(item);
    }

    public bool AddSharedStatus(Entity item)
    {
        // 检查是否是负面状态
        if (item.TryGetComponent(out StatusComponent statusComponent))
        {
            var statusAsset = statusComponent.Type;
            if (statusAsset != null && statusAsset.GetExtend<StatusAssetExtend>().negative)
            {
                // 从StatusComponent获取施加方信息
                BaseSimObject source = null;
                if (item.TryGetComponent(out StatusComponent statusComp))
                {
                    source = statusComp.Source;
                }

                // 如果有施加方信息，计算powerlevel差距并调整状态时长
                if (source != null && source.isActor())
                {
                    var sourcePowerLevel = source.a.GetExtend().GetPowerLevel();
                    var targetPowerLevel = GetPowerLevel();
                    var powerLevelDiff = sourcePowerLevel - targetPowerLevel;

                    // 如果目标powerlevel更高，减少状态时长
                    // 差距越大，减少越多（使用对数衰减）
                    if (powerLevelDiff < 0)
                    {
                        var reductionFactor = Mathf.Exp(powerLevelDiff * 0.1f); // 每差1级，效果减少约10%
                        if (item.TryGetComponent(out AliveTimeLimit timeLimit))
                        {
                            var originalDuration = timeLimit.value;
                            var adjustedDuration = originalDuration * reductionFactor;
                            
                            // 如果调整后的时长过短（小于0.1秒），则不添加状态
                            if (adjustedDuration < 0.1f)
                            {
                                return false;
                            }
                            
                            timeLimit.value = adjustedDuration;
                        }
                    }
                }
            }
        }

        e.AddRelation(new StatusRelation { status = item });
        Base.setStatsDirty();
        return true;
    }

    public void RemoveSharedStatus(Entity item)
    {
        e.RemoveRelation<StatusRelation>(item);
        Base.setStatsDirty();
    }

    public List<Entity> GetStatuses()
    {
        var status_rels = e.GetRelations<StatusRelation>();
        var list = new List<Entity>(status_rels.Length);
        for (int i = 0; i < status_rels.Length; i++)
        {
            list.Add(status_rels[i].status);
        }
        return list;
    }

    public IEnumerable<Entity> GetItems()
    {
        var rels = e.GetRelations<InventoryRelation>();
        var rel_count = rels.Length;
        for (int i = 0; i < rel_count; i++)
        {
            yield return rels[i].item;
        }
        yield break;
    }

    public static void RegisterActionOnNewCreature(Action<ActorExtend> action)
    {
        action_on_new_creature += action;
    }

    public override string ToString()
    {
        return $"[{e.GetComponent<ActorBinder>().ID}] {Base.getName()}: {e}";
    }
    public void LearnSkillV3(Entity skill_container, bool clone = false)
    {
        if (!GeneralSettings.EnableSkillSystems) return;
        if (clone)
        {
            skill_container = skill_container.Store.CloneEntity(skill_container);
        }

        E.AddRelation(new SkillMasterRelation()
        {
            SkillContainer = skill_container
        });
        _learned_skills_v3.Add(skill_container);
    }

    public SpecialItem GetRandomSpecialItem(Func<Entity, bool> filter)
    {
        using var pool = new ListPool<SpecialItem>(GetItems()
            .Select(x => x.GetComponent<SpecialItem>()).Where(x => filter(x.self)));
        if (pool.Any())
            return pool.GetRandom();
        return default;
    }

    public Entity GetSkillActionModifiers(string action_id, Entity default_modifiers)
    {
        return _skill_action_modifiers.TryGetValue(action_id, out var res) ? res : default_modifiers;
    }
    public Entity GetSkillEntityModifiers(string entity_id, Entity default_modifiers)
    {
        return _skill_entity_modifiers.TryGetValue(entity_id, out var res) ? res : default_modifiers;
    }

    private Actor FindTestTarget()
    {
        Actor target = null;
        int count = 0;
        do
        {
            target = World.world.units.GetRandom();
            if (++count > 100)
            {
                ModClass.LogInfo($"{Base.data.id} failed to find enemy");
                throw new Exception();
            }
        } while (!Base.kingdom.isEnemy(target.kingdom));

        return target;
    }

    private void TestCastFireballV3()
    {
        var target = FindTestTarget();

        var skill_container = ModClass.I.W.CreateEntity(new SkillContainer()
        {
            SkillEntityAssetID = SkillEntities.Fireball.id
        });
        ModClass.I.SkillV3.SpawnSkill(skill_container, Base, target, 1);
    }
    private void TestConsumeEnlightenElixir()
    {
        var elixir = SpecialItemUtils.StartBuild(ItemShapes.Ball.id, World.world.getCurWorldTime(), Base.getName())
            .AddComponent(new Elixir()
            {
                elixir_id = Elixirs.EnlightenElixir.id
            })
            .Build();
        this.TryConsumeElixir(elixir);
    }
    public static void RegisterActionOnGetStats(Action<ActorExtend, string> action)
    {
        action_on_get_stats += action;
    }

    private static Action<ActorExtend, string> action_on_get_stats;
    private static Action<ActorExtend, Actor, Kingdom> action_on_kill;
    private static Action<ActorExtend, BaseSimObject, float> action_on_be_attacked;
    public delegate void ActionBeforeBeAttacked(ActorExtend self, BaseSimObject attacker, ref ElementComposition damage_composition, ref AttackType attack_type, ref float damage, ref bool ignore_damage_reduction);
    private static ActionBeforeBeAttacked action_before_be_attacked;
    public float GetStat(string stat_id)
    {
        action_on_get_stats?.Invoke(this, stat_id);
        return Base.stats[stat_id];
    }
    internal void ExtendNewCreature()
    {
        // 灵根
        var has_element_root = Base.asset.GetExtend<ActorAssetExtend>().must_have_element_root ||
                                Randy.randomChance(GeneralSettings.SpawnNaturally);
        if (has_element_root)
        {
            e.AddComponent(ElementRoot.Roll());
        }

        action_on_new_creature?.Invoke(this);
    }

    public static void RegisterActionOnUpdateStats(Action<ActorExtend> action)
    {
        action_on_update_stats += action;
    }

    [Hotfixable]
    internal void ExtendUpdateStats()
    {
        if (HasElementRoot())
        {
            Base.stats.mergeStats(GetElementRoot().Stats);
        }

        if (E.TryGetComponent(out Qiyun qiyun))
        {
            Base.stats[nameof(WorldboxGame.BaseStats.MaxQiyun)] += qiyun.MaxValue;
        }
        foreach (var status_entity in GetStatuses())
        {
            if (status_entity.HasComponent<StatusOverwriteStats>())
            {
                ref var status_stats = ref status_entity.GetComponent<StatusOverwriteStats>().stats;
                if (status_stats != null)
                    Base.stats.mergeStats(status_entity.GetComponent<StatusOverwriteStats>().stats);
            }
            else
            {
                if (status_entity.TryGetComponent(out StatusStatsMultiplier mul))
                {
                    Base.stats.MergeStats(status_entity.GetComponent<StatusComponent>().Type.stats, mul.Value);
                }
                else
                {
                    Base.stats.mergeStats(status_entity.GetComponent<StatusComponent>().Type.stats);
                }
            }
        }
        if (E.TryGetComponent(out PermanentStats permanent_stats))
        {
            Base.stats.mergeStats(permanent_stats.Stats);
        }
        all_skills.Clear();
        all_skills.UnionWith(_learned_skills_v3);

        action_on_update_stats?.Invoke(this);
        
        all_attack_skills.Clear();

        foreach (var skill in all_skills)
        {
            if (skill.GetComponent<SkillContainer>().Asset.Type == SkillEntityType.Attack)
            {
                all_attack_skills.Add(skill);
            }
        }
    }

    internal void PostUpdateStats()
    {
        var stats = Base.stats;

        var armor = Mathf.Max(stats[S.armor], 0);
        s_armor[ElementIndex.Entropy + 1] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        float master = 0;
        for (var i = 0; i < 8; i++)
        {
            armor = stats[WorldboxGame.BaseStats.ArmorStats[i]];
            master = stats[WorldboxGame.BaseStats.MasterStats[i]];
            s_armor[i] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay) * master /
                         (master + DamageCalcHyperParameters.MasterEffectDecay);
        }
    }

    public void NewCultisys<T>(CultisysAsset<T> cultisys) where T : struct, ICultisysComponent
    {
        e.AddComponent(cultisys.DefaultComponent);
        cultisys.OnGetAction?.Invoke(this, cultisys, ref e.GetComponent<T>());
    }

    [Hotfixable]
    public void GetHit(float damage, ref ElementComposition damage_composition, BaseSimObject attacker, AttackType attack_type_for_vanilla = AttackType.Other, bool ignore_damage_reduction = false)
    {

        if (!Base.isAlive() || Base.hasStatus("invincible") || Base.data.health <= 0)
        {
            return;
        }

        if (Base == attacker) return;

        action_before_be_attacked?.Invoke(this, attacker, ref damage_composition, ref attack_type_for_vanilla, ref damage, ref ignore_damage_reduction);

        var attacker_power_level = ((attacker?.isActor() ?? false) && !attacker.isRekt()) ? attacker.a.GetExtend().GetPowerLevel() : 0;
        var power_level = GetPowerLevel();
        var power_level_gap = power_level - attacker_power_level;
        if (!ignore_damage_reduction)
        {
            if (power_level_gap > 0)
            {
                damage = Mathf.Log(Mathf.Max(damage, 1),
                    Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level_gap));
            }

            if (damage >= 1)
            {
                var damage_ratio = 1 - s_armor[ElementIndex.Entropy + 1];
                var total_ratio = 0f;
                var sum = 0f;
                for (var i = 0; i < 5; i++)
                {
                    total_ratio += damage_composition[i] * (1 - s_armor[i]);
                    sum += damage_composition[i];
                }
                if (sum > 0)
                    damage_ratio *= total_ratio / sum;
                total_ratio = 0f;
                sum = 0f;
                for (var i = 5; i < 7; i++)
                {
                    total_ratio += damage_composition[i] * (1 - s_armor[i]);
                    sum += damage_composition[i];
                }
            
                if (sum > 0)
                    damage_ratio *= total_ratio / sum * (1 - s_armor[ElementIndex.Entropy]);
            
                damage_ratio *= (1 - s_armor[ElementIndex.Entropy]);
                damage = Mathf.Clamp(damage * damage_ratio, 0, int.MaxValue >> 2);
            }
        }

        if (ShouldResolveIneffectiveHit(attacker, damage, power_level_gap))
        {
            ResolveIneffectiveHit(attacker);
            return;
        }
        
        // 触发被攻击事件（在实际受到伤害之前）
        if (damage > 0 && attacker != null)
        {
            action_on_be_attacked?.Invoke(this, attacker, damage);
        }
        if (attacker != null && !attacker.isRekt() && attacker.isActor())
        {
            Base.checkSpecialAttackLogic(attacker.a, attack_type_for_vanilla, damage, out var final_damage);
            AchievementLibrary.clone_wars.checkBySignal(new ValueTuple<Actor, Actor>(Base, attacker.a));
            damage = Math.Min(damage, final_damage);
        }
        PatchActor.getHit_snapshot(Base, damage, pAttackType: attack_type_for_vanilla, pAttacker: attacker, pSkipIfShake: false, pCheckDamageReduction: false);
    }

    private static bool ShouldResolveIneffectiveHit(BaseSimObject attacker, float damage, float power_level_gap)
    {
        if (damage <= 0 || power_level_gap < 0) return false;
        if (attacker.isRekt() || !attacker.isActor()) return false;

        var chance = power_level_gap == 0 ? 0.25f : 1f - Mathf.Pow(0.5f, power_level_gap);
        return Randy.randomChance(Mathf.Clamp01(chance));
    }

    private void ResolveIneffectiveHit(BaseSimObject attacker)
    {
        if (attacker.isRekt() || !attacker.isActor()) return;

        var actor = attacker.a;
        if (actor.has_attack_target && actor.attack_target == Base)
        {
            actor.clearAttackTarget();
        }
        attacker.ignoreTarget(Base);
        actor.makeWait(0.3f);
    }

    public bool HasElementRoot()
    {
        return e.HasComponent<ElementRoot>();
    }

    public ref ElementRoot GetElementRoot()
    {
        return ref e.GetComponent<ElementRoot>();
    }

    public bool HasCultisys<T>() where T : struct, ICultisysComponent
    {
        return e.HasComponent<T>();
    }

    public ref T GetCultisys<T>() where T : struct, ICultisysComponent
    {
        return ref e.GetComponent<T>();
    }

    public Entity GetFirstItemWithComponent<TComponent>() where TComponent : struct, IComponent
    {
        return GetItems().FirstOrDefault(x => x.HasComponent<TComponent>());
    }

    public bool HasItem<TComponent>() where TComponent : struct, IComponent
    {
        return GetItems().Any(x => x.HasComponent<TComponent>());
    }
    public static void RegisterActionOnKill(Action<ActorExtend, Actor, Kingdom> action)
    {
        action_on_kill += action;
    }
    
    public static void RegisterActionOnBeAttacked(Action<ActorExtend, BaseSimObject, float> action)
    {
        action_on_be_attacked += action;
    }
    public static void RegisterActionBeforeBeAttacked(ActionBeforeBeAttacked action)
    {
        action_before_be_attacked += action;
    }
    
    public void NewKillAction(Actor dead_unit, Kingdom dead_kingdom)
    {
        using var pool = new ListPool<Entity>(dead_unit.GetExtend().GetItems());
        foreach (var item in pool)
        {
            if (item.Tags.HasAny(Tags.Get<TagOccupied, TagConsumed, TagUncompleted>()))
            {
                item.AddTag<TagRecycle>();
                continue;
            }
            AddSpecialItem(item);
        }
        action_on_kill?.Invoke(this, dead_unit, dead_kingdom);
    }

    public bool HasRelatedForce<TRelation>() where TRelation : struct, IForceRelation
    {
        return E.GetRelations<TRelation>().Length > 0;
    }
    public IEnumerable<Entity> GetForces<TRelation>() where TRelation : struct, IForceRelation
    {
        var rels = E.GetRelations<TRelation>();
        var rel_count = rels.Length;
        for (int i = 0; i < rel_count; i++)
        {
            yield return rels[i].GetRelationKey();  
        }
        yield break;
    }

    public void JoinForce<TRelation>(Entity force) where TRelation : struct, IForceRelation
    {
        E.AddRelation(new TRelation { ForceEntity = force });
    }
    public void ExitForce<TRelation>(Entity force) where TRelation : struct, IForceRelation
    {
        E.RemoveRelation<TRelation>(force);
    }

    private static Action<ActorExtend> action_on_death;

    public static void RegisterActionOnDeath(Action<ActorExtend> action)
    {
        action_on_death += action;
    }
    public void OnDeath()
    {
        action_on_death?.Invoke(this);
        using var pool = new ListPool<Entity>(GetItems());
        if (Base.hasCity())
        {
            var ce = Base.GetExtend();
            foreach (var item in pool)
            {
                if (item.Tags.HasAny(Tags.Get<TagOccupied, TagConsumed, TagUncompleted>()))
                {
                    item.AddTag<TagRecycle>();
                    continue;
                }
                ce.AddSpecialItem(item);
            }
        }
    }
    /// <summary>
    /// 复制修仙里头所有的数据
    /// </summary>
    /// <param name="clone_source"></param>
    [Hotfixable]
    public void CloneLeftFrom(ActorExtend clone_source)
    {
        var self = Base;
        var source = clone_source.Base;

        #region 一般数据复制

        source.data.save();
        if (source.data.custom_data_bool != null)
        {
            self.data.custom_data_bool = new()
            {
                dict = new(source.data.custom_data_bool.dict)
            };
        }

        if (source.data.custom_data_float != null)
        {
            self.data.custom_data_float = new()
            {
                dict = new(source.data.custom_data_float.dict)
            };
        }

        if (source.data.custom_data_int != null)
        {
            self.data.custom_data_int = new()
            {
                dict = new(source.data.custom_data_int.dict)
            };
        }

        if (source.data.custom_data_string != null)
        {
            self.data.custom_data_string = new()
            {
                dict = new(source.data.custom_data_string.dict)
            };
        }

        if (source.data.custom_data_flags != null)
        {
            self.data.custom_data_flags = new(source.data.custom_data_flags);
        }

        #endregion

        #region 实体复制

        var old_binder = E.GetComponent<ActorBinder>();
        var cloned_entity = E.Store.CloneEntity(clone_source.E);
        cloned_entity.AddComponent(old_binder);

        E.AddTag<TagRecycle>();
        e = cloned_entity;
        #endregion

        // TODO: 各种Relation复制(比如背包/自制势力)

        #region 技能相关复制

        _skill_action_modifiers.Clear();
        _skill_entity_modifiers.Clear();

        foreach (var item in clone_source._skill_action_modifiers)
        {
            _skill_action_modifiers[item.Key] = item.Value.Store.CloneEntity(item.Value);
        }

        foreach (var item in clone_source._skill_entity_modifiers)
        {
            _skill_entity_modifiers[item.Key] = item.Value.Store.CloneEntity(item.Value);
        }

        #endregion
        
    }

    public void SetSect(Sect new_sect)
    {
        WorldboxGame.I.Sects.setDirtyUnits(sect);
        sect = new_sect;
        WorldboxGame.I.Sects.unitAdded(new_sect);
        Base.setStatsDirty();
    }

    public bool CastSkillV3(Entity skill, BaseSimObject target)
    {
        if (!GeneralSettings.EnableSkillSystems) return false;
        // TODO: 添加消耗检查，技能消耗由技能实体和所有词缀组合决定（存在SkillContainer里头）
        if (!skill.HasComponent<SkillContainer>())
        {
            ModClass.LogError($"技能实体{skill}不包含技能");
            return false;
        }

        var plan = SkillCastPlanner.CreatePlan(this, skill, target);
        if (plan.Steps.Count == 0) return false;

        return ModClass.I.SkillV3.StartSkillSequence(this, skill, plan, 100);
    }

    // ======== 师徒系统核心方法（不依赖Content） ========
    
    /// <summary>
    /// 获取所有弟子
    /// </summary>
    public List<ActorExtend> GetApprentices()
    {
        var apprentices = new List<ActorExtend>();
        if (E.IsNull) return apprentices;
        
        // 查找所有指向此Entity的MasterApprenticeRelation
        var allActors = World.world.units.units_only_alive;
        foreach (var actor in allActors)
        {
            var apprenticeAe = actor.GetExtend();
            if (apprenticeAe.HasMaster() && apprenticeAe.GetMaster() == Base)
            {
                apprentices.Add(apprenticeAe);
            }
        }
        
        return apprentices;
    }
    
    /// <summary>
    /// 检查是否有师傅
    /// </summary>
    [Hotfixable]
    public bool HasMaster()
    {
        if (E.IsNull) return false;
        var relations = E.GetRelations<MasterApprenticeRelation>();
        if (relations.Length == 0) return false;
        var relation = relations[0];
        // 检查师傅是否还存在
        if (relation.Master.IsNull) return false;
        var masterActor = relation.Master.GetComponent<ActorBinder>().Actor;
        if (masterActor.isRekt()) return false;
        return true;
    }
    
    /// <summary>
    /// 获取师傅
    /// </summary>
    [Hotfixable]
    public Actor GetMaster()
    {
        if (!HasMaster()) return null;
        
        var relations = E.GetRelations<MasterApprenticeRelation>();
        if (relations.Length == 0) return null;
        
        var masterEntity = relations[0].Master;
        if (masterEntity.IsNull) return null;
        
        var masterBinder = masterEntity.GetComponent<ActorBinder>();
        
        return masterBinder.Actor;
    }
    
    /// <summary>
    /// 获取师徒关系
    /// </summary>
    public ref MasterApprenticeRelation GetMasterRelation()
    {
        var relations = E.GetRelations<MasterApprenticeRelation>();
        var relation = relations[0];

        return ref E.GetRelation<MasterApprenticeRelation, Entity>(relation.Master);
    }
    
    /// <summary>
    /// 增加亲密度
    /// </summary>
    [Hotfixable]
    public void AddIntimacy(float amount)
    {
        if (!HasMaster()) return;
        
        ref var relation = ref GetMasterRelation();
        relation.Intimacy = Mathf.Clamp(relation.Intimacy + amount, 0, 100);
        
        // 检查是否升级关系类型
        UpdateRelationType(ref relation);
    }
    
    /// <summary>
    /// 获取亲密度
    /// </summary>
    public float GetIntimacy()
    {
        if (!HasMaster()) return 0;
        ref var relation = ref GetMasterRelation();
        return relation.Intimacy;
    }
    
    /// <summary>
    /// 获取关系类型
    /// </summary>
    public MasterApprenticeType GetRelationType()
    {
        if (!HasMaster()) return MasterApprenticeType.Nominal;
        ref var relation = ref GetMasterRelation();
        return relation.RelationType;
    }
    
    /// <summary>
    /// 更新关系类型（根据亲密度）
    /// </summary>
    private static void UpdateRelationType(ref MasterApprenticeRelation relation)
    {
        if (relation.IsSuccessor && relation.Intimacy >= 90)
        {
            relation.RelationType = MasterApprenticeType.Successor;
        }
        else if (relation.Intimacy >= 60)
        {
            relation.RelationType = MasterApprenticeType.Direct;
        }
        else if (relation.Intimacy >= 30)
        {
            relation.RelationType = MasterApprenticeType.Formal;
        }
        else
        {
            relation.RelationType = MasterApprenticeType.Nominal;
        }
    }

    private static Action<Actor> action_on_addchildren;

    public static void RegisterPossibleChildren<TActorComponent>() where TActorComponent : BaseActorComponent
    {
        action_on_addchildren += (Actor a) => {
            if (a.avatar?.HasComponent<TActorComponent>() ?? false)
            {
                a.addChild(a.avatar.GetComponent<TActorComponent>());
            }
        };
    }
    internal void OnAddChildren()
    {
        action_on_addchildren?.Invoke(Base);
        
		if (Base.children_pre_behaviour != null || Base.children_special != null)
		{
			Base.batch.c_update_children.Add(Base);
		}
    }
}
