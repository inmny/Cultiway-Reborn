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
    }

    public void Dispose()
    {
        if (!e.IsNull)
        {
            e.AddTag<TagRecycle>();
            foreach (var item in GetItems())
            {
                item.AddTag<TagRecycle>();
            }
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
                skill_action.AddTag<TagRecycle>();
            }

            _skill_action_modifiers = null;
        }
        if (_skill_entity_modifiers != null)
        {
            foreach (var skill_entity in _skill_entity_modifiers.Values)
            {
                skill_entity.AddTag<TagRecycle>();
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
    [Hotfixable]
    public bool TryToAttack(BaseSimObject target, Action kill_action = null, float bonus_area_effect = 0, bool do_checks = true)
    {
        var actor = Base;
        if (do_checks)
        {
            if (actor.isInWaterAndCantAttack()) return false;
            if (!actor.isAttackPossible()) return false;
        }
        if (target.isRekt()) return false;
        if (actor.kingdom == null)
        {
            ModClass.LogError($"Actor {actor.id}({(actor.isActor() ? actor.a.asset.id : actor.b.asset.id)}) has no kingdom");
        }
        CombatActionAsset basic_attack_action = null;
        
        using var attack_action_pool = new ListPool<CombatActionAsset>();
        // 加入普攻
        if (actor.hasMeleeAttack())
        {
            basic_attack_action = WorldboxGame.CombatActions.AttackMelee;
        }
        else if (actor.isInAttackRange(target))
        {
            basic_attack_action = WorldboxGame.CombatActions.AttackRange;
        }
        
        if (basic_attack_action != null) basic_attack_action.AddToPool(attack_action_pool);
        // 加入原版技能
        if (actor._spells.hasAny())
        {
            WorldboxGame.CombatActions.CastVanillaSpell.AddToPool(attack_action_pool);
        }
        // 加入自定义技能
        WorldboxGame.CombatActions.CastSkillV3.AddToPool(attack_action_pool, all_attack_skills.Count);
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
        
        if (attack_action_pool.Any())
        {
            // 随机选择一个攻击动作
            var attack_action = attack_action_pool.GetRandom();
            if (attack_action.action(attack_data))
            {
                // 结算攻击动作/饥饿/声效/攻击间隔
                attack_succeed(attack_action);
                return true;
            }
            if (!attack_action.basic)
            {
                // 如果不是普攻且失败了，那就尝试普攻
                goto BASIC_ATTACK;
            }

            // 如果是普攻且失败了，那就退出
            return false;
        }
        BASIC_ATTACK:
        if (basic_attack_action == null) return false;
        if (basic_attack_action.action(attack_data))
        {
            // 结算攻击动作/饥饿/声效/攻击间隔
            attack_succeed(basic_attack_action);
            return true;
        }
        
        return false;

        [Hotfixable]
        void attack_succeed(CombatActionAsset combat_action)
        {
            actor.startAttackCooldown();
            actor.punchTargetAnimation(target.current_position, true, actor.hasRangeAttack());
            actor.spendStamina(combat_action.cost_stamina);
            actor.spendMana(combat_action.cost_mana);
            if (combat_action.play_unit_attack_sounds && actor.asset.has_sound_attack)
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
        return e.GetRelations<StatusRelation>().Select(x => x.status).ToList();
    }

    public IEnumerable<Entity> GetItems()
    {
        return e.GetRelations<InventoryRelation>().Select(x => x.item);
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
        using var pool = new ListPool<SpecialItem>(e.GetRelations<InventoryRelation>()
            .Select(x => x.item.GetComponent<SpecialItem>()).Where(x => filter(x.self)));
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
        if (!ignore_damage_reduction)
        {
            var attacker_power_level = ((attacker?.isActor() ?? false) && !attacker.isRekt()) ? attacker.a.GetExtend().GetPowerLevel() : 0;
            var power_level = GetPowerLevel();
            if (power_level > attacker_power_level)
            {
                damage = Mathf.Log(Mathf.Max(damage, 1),
                    Mathf.Pow(DamageCalcHyperParameters.PowerBase, power_level - attacker_power_level));
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
        
        // 触发被攻击事件（在实际受到伤害之前）
        if (damage > 0 && attacker != null)
        {
            action_on_be_attacked?.Invoke(this, attacker, damage);
        }
        if (!attacker.isRekt() && attacker.isActor())
        {
            Base.checkSpecialAttackLogic(attacker.a, attack_type_for_vanilla, damage, out var final_damage);
            AchievementLibrary.clone_wars.checkBySignal(new ValueTuple<Actor, Actor>(Base, attacker.a));
            damage = Math.Min(damage, final_damage);
        }
        PatchActor.getHit_snapshot(Base, damage, pAttackType: attack_type_for_vanilla, pAttacker: attacker, pSkipIfShake: false, pCheckDamageReduction: false);
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
        return e.GetRelations<InventoryRelation>().Select(x => x.item)
            .FirstOrDefault(x => x.HasComponent<TComponent>());
    }

    public bool HasItem<TComponent>() where TComponent : struct, IComponent
    {
        return e.GetRelations<InventoryRelation>().Select(x => x.item).Any(x => x.HasComponent<TComponent>());
    }
    public static void RegisterActionOnKill(Action<ActorExtend, Actor, Kingdom> action)
    {
        action_on_kill += action;
    }
    
    public static void RegisterActionOnBeAttacked(Action<ActorExtend, BaseSimObject, float> action)
    {
        action_on_be_attacked += action;
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
        return E.GetRelations<TRelation>().Select(x => x.GetRelationKey());
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
        // TODO: 添加消耗检查，技能消耗由技能实体和所有词缀组合决定（存在SkillContainer里头）
        if (!skill.HasComponent<SkillContainer>())
        {
            ModClass.LogError($"技能实体{skill}不包含技能");
            return false;
        }
        ModClass.I.SkillV3.SpawnSkill(skill, Base, target, 100);
        return true;
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
        if (!relations.Any()) return false;
        var relation = relations.First();
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
        if (!relations.Any()) return null;
        
        var masterEntity = relations.First().Master;
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
        var relation = relations.First();

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