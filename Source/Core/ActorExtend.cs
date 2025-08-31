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
using Cultiway.Content.Skills;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Examples;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Patch;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;
using NeoModLoader.api.attributes;
using NeoModLoader.services;
using strings;
using UnityEngine;

namespace Cultiway.Core;

public class ActorExtend : ExtendComponent<Actor>, IHasInventory, IHasStatus, IHasForce, IDisposable
{
    private static Action<ActorExtend> action_on_new_creature;

    private static   Action<ActorExtend> action_on_update_stats;
    private readonly HashSet<string> _learned_skills = new();
    private Entity          e;

    private  Dictionary<string, Entity> _skill_action_modifiers = new();
    private  Dictionary<string, Entity> _skill_entity_modifiers = new();
    internal float[]         s_armor        = new float[9];
    public Sect sect;
    public   HashSet<string> tmp_all_skills = new();
    public List<string> tmp_all_attack_skills = new();

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
    public static void RegisterCombatActionOnAttack(Action<ActorExtend, BaseSimObject, ListPool<CombatActionAsset>> action)
    {
        action_on_attack += action;
    }
    private static Action<ActorExtend, BaseSimObject, ListPool<CombatActionAsset>> action_on_attack;
    [Hotfixable]
    public bool TryToAttack(BaseSimObject target, Action kill_action = null, float bonus_area_effect = 0)
    {
        var actor = Base;
        if (actor.isInLiquid() && !actor.asset.force_ocean_creature) return false;
        if (!actor.isAttackReady()) return false;

        CombatActionAsset basic_attack_action = null;
        
        using var attack_action_pool = new ListPool<CombatActionAsset>();
        // 加入普攻
        if (actor.s_type_attack == WeaponType.Melee)
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
        WorldboxGame.CombatActions.CastSkill.AddToPool(attack_action_pool, tmp_all_attack_skills.Count);
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
            actor.velocity.x = x * 0.6f;
            actor.velocity.y = y * 0.6f;
            actor.velocity.z = z * 2f;
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

    private void TestHugeSwordQi()
    {
        Actor target = FindTestTarget();
        
        CastSkillV2(WrappedSkills.StartHugeSwordQi.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast huge sword qi to {target.data.id}");
    }
    private void TestCastNewFireball()
    {
        Actor target = FindTestTarget();

        CastSkillV2(WrappedSkills.StartFireballCaster.id, target, true);
        ModClass.LogInfo($"{Base.data.id} cast fireball to {target.data.id}");
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
                                   Randy.randomChance(ModClass.L.ElementRootLibrary.base_prob);
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
    public void GetHit(float damage, ref ElementComposition damage_composition, BaseSimObject attacker, AttackType attack_type_for_vanilla = AttackType.Other, bool ignore_damage_reduction = false)
    {

        if (!Base.isAlive() || Base.hasStatus("invincible") || Base.data.health <= 0)
        {
            return;
        }

        if (Base == attacker) return;
        if (!ignore_damage_reduction)
        {
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
            }
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
        
    }

    public void SetSect(Sect new_sect)
    {
        WorldboxGame.I.Sects.setDirtyUnits(sect);
        sect = new_sect;
        WorldboxGame.I.Sects.unitAdded(new_sect);
        Base.setStatsDirty();
    }
}