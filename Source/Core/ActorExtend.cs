using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Extensions;
using Cultiway.Content.Skills;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Examples;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.services;
using UnityEngine;

namespace Cultiway.Core;

public class ActorExtend : ExtendComponent<Actor>, IHasInventory, IHasStatus, IHasForce
{
    private static Action<ActorExtend> action_on_new_creature;

    private static   Action<ActorExtend> action_on_update_stats;
    private readonly HashSet<string> _learned_skills = new();
    private Entity          e;

    private  Dictionary<string, Entity> _skill_action_modifiers = new();
    private  Dictionary<string, Entity> _skill_entity_modifiers = new();
    internal float[]         s_armor        = new float[9];
    
    public   HashSet<string> tmp_all_skills = new();
    public List<string> tmp_all_attack_skills = new();

    public ActorExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<ActorBinder>()._ae = this;
    }

    public Entity E => e;
    public override Actor Base => e.HasComponent<ActorBinder>() ? e.GetComponent<ActorBinder>().Actor : null;
    public bool TryGetComponent<TComponent>(out TComponent component) where TComponent : struct, IComponent
    {
        return e.TryGetComponent(out component);
    }
    public static void RegisterCombatActionOnAttack(Action<ActorExtend, BaseSimObject, ListPool<CombatActionAsset>> action)
    {
        action_on_attack += action;
    }
    private static Action<ActorExtend, BaseSimObject, ListPool<CombatActionAsset>> action_on_attack;
    [Hotfixable]
    public bool TryToAttack(BaseSimObject target)
    {
        var actor = Base;
        if (actor.isInLiquid() && !actor.asset.oceanCreature) return false;
        if (!actor.isAttackReady()) return false;

        CombatActionAsset basic_attack_action = null;
        
        using var attack_action_pool = new ListPool<CombatActionAsset>();
        // 加入普攻
        if (actor.s_attackType == WeaponType.Melee)
        {
            basic_attack_action = WorldboxGame.CombatActions.AttackMelee;
        }
        else if (actor.isInAttackRange(target))
        {
            basic_attack_action = WorldboxGame.CombatActions.AttackRange;
        }
        
        if (basic_attack_action != null) basic_attack_action.AddToPool(attack_action_pool);
        // 加入原版技能
        if (actor.asset.attack_spells?.Count > 0)
        {
            WorldboxGame.CombatActions.CastVanillaSpell.AddToPool(attack_action_pool);
        }
        // 加入自定义技能
        WorldboxGame.CombatActions.CastSkill.AddToPool(attack_action_pool, tmp_all_attack_skills.Count);
        action_on_attack?.Invoke(this, target, attack_action_pool);
        

        float target_size = target.stats[S.size];
        Vector3 target_pos = new Vector3(target.currentPosition.x, target.currentPosition.y);
        if (target.isActor() && target.a.is_moving && target.isFlying())
        {
            target_pos = Vector3.MoveTowards(target_pos, target.a.nextStepPosition, target_size * 3f);
        }
        float dist = Vector2.Distance(actor.currentPosition, target.currentPosition) + target.getZ();
        Vector3 new_point = Toolbox.getNewPoint(actor.currentPosition.x, actor.currentPosition.y, target_pos.x, target_pos.y, dist - target_size, true);

        AttackData attack_data = new(actor, target.currentTile, new_point, target, AttackType.Weapon,
            actor.haveMetallicWeapon());
        
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
            actor.timer_action = actor.s_attackSpeed_seconds;
            actor.attackTimer = actor.s_attackSpeed_seconds;
            actor.punchTargetAnimation(target.currentPosition, true, actor.s_attackType == WeaponType.Range);
            if (combat_action.play_unit_attack_sounds && !string.IsNullOrEmpty(actor.asset.fmod_attack))
            {
                MusicBox.playSound(actor.asset.fmod_attack, actor.currentTile.x, actor.currentTile.y);
            }
            if (actor.asset.needFood && Toolbox.randomBool())
            {
                actor.decreaseHunger();
            }
        }
    }

    public void GetForce(BaseSimObject source, float x, float y, float z)
    {
        var actor = Base;
        if (!actor.asset.canBeMovedByPowers)
        {
            return;
        }
        if (actor.zPosition.y > 0f)
        {
            return;
        }
        var power_level = GetPowerLevel();
        var source_power_level = (source?.isActor()??false) ? source.a.GetExtend().GetPowerLevel() : 0;
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
            actor.forceVector.x = x * 0.6f;
            actor.forceVector.y = y * 0.6f;
            actor.forceVector.z = z * 2f;
            actor.under_force = true;
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

    public void AddSharedStatus(Entity item)
    {
        e.AddRelation(new StatusRelation { status = item });
        Base.setStatsDirty();
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
    public bool HasSkillModifier<TModifier, TValue>(string action_id)
        where TModifier : struct, IModifier<TValue>
    {
        return _skill_action_modifiers.TryGetValue(action_id, out Entity action_modifiers) && action_modifiers.HasComponent<TModifier>();
    }
    public bool HasSkillEntityModifier<TModifier, TValue>(string entity_id)
        where TModifier : struct, IModifier<TValue>
    {
        return _skill_entity_modifiers.TryGetValue(entity_id, out Entity entity_modifiers) && entity_modifiers.HasComponent<TModifier>();
    }
    public ref TModifier GetSkillModifier<TModifier, TValue>(string action_id)
        where TModifier : struct, IModifier<TValue>
    {
        return ref _skill_action_modifiers[action_id].GetComponent<TModifier>();
    }
    public ref TModifier GetSkillEntityModifier<TModifier, TValue>(string entity_id)
        where TModifier : struct, IModifier<TValue>
    {
        return ref _skill_entity_modifiers[entity_id].GetComponent<TModifier>();
    }
    public Entity GetOrNewSkillActionModifiers(string action_id)
    {
        if (_skill_action_modifiers.TryGetValue(action_id, out var container))
        {
            return container;
        }
        container = TriggerActionBaseMeta.AllDict[action_id].NewModifierContainer();
        _skill_action_modifiers[action_id] = container;
        return container;
    }
    public Entity GetOrNewSkillEntityModifiers(string entity_id)
    {
        if (_skill_entity_modifiers.TryGetValue(entity_id, out var container))
        {
            return container;
        }
        container = SkillEntityMeta.AllDict[entity_id].NewModifierContainer();
        _skill_entity_modifiers[entity_id] = container;
        return container;
    }
    public void AddSkillModifier<TModifier, TValue>(string action_id, TModifier modifier)
        where TModifier : struct, IModifier<TValue>
    {
        if (!_skill_action_modifiers.TryGetValue(action_id, out Entity modifiers))
        {
            modifiers = TriggerActionBaseMeta.AllDict[action_id].NewModifierContainer();
            _skill_action_modifiers[action_id] = modifiers;
        }

        modifiers.AddComponent(modifier);
    }
    public void AddSkillEntityModifier<TModifier, TValue>(string entity_id, TModifier modifier)
        where TModifier : struct, IModifier<TValue>
    {
        if (!_skill_entity_modifiers.TryGetValue(entity_id, out Entity modifiers))
        {
            modifiers = SkillEntityMeta.AllDict[entity_id].NewModifierContainer();
            _skill_entity_modifiers[entity_id] = modifiers;
        }

        modifiers.AddComponent(modifier);
    }

    public void LearnSkill(string id)
    {
        _learned_skills.Add(id);
    }

    public bool CastSkillV2(string id, BaseSimObject target_obj, bool ignore_cost = false, float addition_strength = 0)
    {
        var wrapped_asset = ModClass.L.WrappedSkillLibrary.get(id);
        if (wrapped_asset == null)
        {
            ModClass.I.SkillV2.NewSkillStarter(id, this, target_obj, 100 + addition_strength);
            return true;
        }

        if (wrapped_asset.cost_check == null || ignore_cost)
        {
            ModClass.I.SkillV2.NewSkillStarter(id, this, target_obj, wrapped_asset.default_strength + addition_strength);
            return true;
        }
        if (wrapped_asset.cost_check(this, out var strength))
        {
            ModClass.I.SkillV2.NewSkillStarter(id, this, target_obj, strength + addition_strength);
            return true;
        }

        return false;
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
    [Hotfixable]
    private void TestCastFireball()
    {
        Actor target = FindTestTarget();

        CastSkillV2(ExampleTriggerActions.StartSkillFireball.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast fireball to {target.data.id}");
    }

    private void TestCastCommonWeapon()
    {
        Actor target = FindTestTarget();

        CastSkillV2(WrappedSkills.StartWeaponSkill.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast weapon to {target.data.id}");
    }
    private void TestCastSelfSurroundFireBlade()
    {
        Actor target = FindTestTarget();

        CastSkillV2(WrappedSkills.StartSelfSurroundFireBlade.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast self surround fire blade to {target.data.id}");
    }
    private void TestCastAllFireBlade()
    {
        Actor target = FindTestTarget();

        CastSkillV2(WrappedSkills.StartAllFireBlade.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast all fire blade to {target.data.id}");
    }
    private void TestCastAllGoldSword()
    {
        Actor target = FindTestTarget();

        CastSkillV2(WrappedSkills.StartAllGoldSword.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast all gold sword to {target.data.id}");
    }

    private void TestCastAllGroundThorn()
    {
        Actor target = FindTestTarget();

        CastSkillV2(WrappedSkills.StartAllGroundThorn.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast all ground thorn to {target.data.id}");
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

    public float GetStat(string stat_id)
    {
        action_on_get_stats?.Invoke(this, stat_id);
        return Base.stats[stat_id];
    }
    internal void ExtendNewCreature()
    {
        // 灵根
        if (GeneralSettings.SpawnNaturally)
        {
            var has_element_root = Base.asset.GetExtend<ActorAssetExtend>().must_have_element_root ||
                                   Toolbox.randomChance(ModClass.L.ElementRootLibrary.base_prob);
            if (has_element_root)
            {
                e.AddComponent(ElementRoot.Roll());
            }
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
                Base.stats.mergeStats(status_entity.GetComponent<StatusComponent>().Type.stats);
            }
        }

        tmp_all_skills.Clear();
        tmp_all_skills.UnionWith(_learned_skills);

        action_on_update_stats?.Invoke(this);
        
        tmp_all_attack_skills.Clear();
        var library = ModClass.L.WrappedSkillLibrary;
        foreach (var skill in tmp_all_skills)
        {
            if (library.get(skill)?.HasSkillType(WrappedSkillType.Attack) ?? false)
            {
                tmp_all_attack_skills.Add(skill);
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
    public void GetHit(float damage, ref ElementComposition damage_composition, BaseSimObject attacker)
    {

        if (!Base.isAlive() || Base.hasStatus("invincible") || Base.data.health <= 0)
        {
            return;
        }

        if (Base == attacker) return;
        var old_damage = damage;
        var old_health = Base.data.health;

        var attacker_power_level = (attacker?.isActor() ?? false) ? attacker.a.GetExtend().GetPowerLevel() : 0;
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
            Base.data.health -= (int)damage;
        }
        // 补齐原版的一些效果
        int health_before = Base.data.health;
        
        if (Base.data.health <= 0) Base.data.health = 1;
        PatchActor.getHit_snapshot(Base, 0, pAttacker: attacker, pSkipIfShake: false);
        
        Base.data.health = health_before; // 防止强制扣血
        if (Base.data.favorite && Base.data.health != old_health)
            LogService.LogInfoConcurrent($"{Base.data.id}({power_level}) 被攻击，伤害{old_damage}({attacker_power_level})，最终伤害{damage}. 血量{old_health}->{Base.data.health}");
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

    public bool HasComponent<T>() where T : struct, IComponent
    {
        return e.HasComponent<T>();
    }

    public ref T GetComponent<T>() where T : struct, IComponent
    {
        return ref e.GetComponent<T>();
    }

    public void AddComponent<T>(T component = default) where T : struct, IComponent
    {
        e.AddComponent(component);
    }
    internal void PrepareDestroy()
    {
        e.AddTag<TagRecycle>();
        foreach (var skill_action in _skill_action_modifiers.Values)
        {
            skill_action.DeleteEntity();
        }

        foreach (var item in GetItems())
        {
            item.AddTag<TagRecycle>();
        }
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
    
    public void NewKillAction(Actor dead_unit, Kingdom dead_kingdom)
    {
        using var pool = new ListPool<Entity>(dead_unit.GetExtend().GetItems());
        foreach (var item in pool)
        {
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
    }
    /// <summary>
    /// 复制修仙里头所有的数据
    /// </summary>
    /// <param name="clone_source"></param>
    public void CloneAllFrom(ActorExtend clone_source)
    {
        var self = Base;
        var source = clone_source.Base;

        #region 原版复制
        
        self.currentPosition = source.currentPosition;
        self.transform.position = source.transform.position;
        self.curAngle = source.transform.localEulerAngles;
        self.transform.localEulerAngles = self.curAngle;
        self.data.setName(source.data.name);
        self.data.created_time = source.data.created_time;
        self.data.age_overgrowth = source.data.age_overgrowth;
        self.data.kills = source.data.kills;
        self.data.children = source.data.children;
        self.data.favorite = source.data.favorite;
        self.takeItems(source, self.asset.take_items_ignore_range_weapons);
        for (int i = 0; i < source.data.traits.Count; i++)
        {
            string text = source.data.traits[i];
            self.addTrait(text, false);
        }

        #endregion

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

        #region 原版势力相关复制

        self.data.culture = source.data.culture;
        if (!string.IsNullOrEmpty(source.data.clan))
        {
            var clan = World.world.clans.get(source.data.clan);
            clan.addUnit(self);
        }

        if (source.city != null)
        {
            self.joinCity(source.city);
        }
        else
        {
            self.setKingdom(source.kingdom);
        }

        #endregion

        #region 实体复制

        var cloned_entity = E.Store.CloneEntity(clone_source.E);
        ref var binder = ref cloned_entity.GetComponent<ActorBinder>();
        binder._ae = this;
        binder.ID = self.data.id;
        binder.Update();
        
        E.DeleteEntity();
        e = cloned_entity;
        #endregion

        // TODO: 各种Relation复制(比如背包/自制势力)

        #region 技能相关复制

        _learned_skills.Clear();
        _learned_skills.UnionWith(clone_source._learned_skills);
        
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
        
        
        self.setStatsDirty();
        self.setPosDirty();
    }
}