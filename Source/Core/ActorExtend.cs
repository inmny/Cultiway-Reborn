using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content;
using Cultiway.Content.Components;
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
using UnityEngine;

namespace Cultiway.Core;

public class ActorExtend : ExtendComponent<Actor>, IHasInventory, IHasStatus
{
    private static Action<ActorExtend> action_on_new_creature;

    private static   Action<ActorExtend> action_on_update_stats;
    private readonly HashSet<string> _learned_skills = new();
    private readonly Entity          e;

    private  Dictionary<string, Entity> _skill_actions = new();
    internal float[]         s_armor        = new float[9];
    public   HashSet<string> tmp_all_skills = new();

    public ActorExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<ActorBinder>()._ae = this;
    }

    public Entity E => e;

    public override Actor Base => e.HasComponent<ActorBinder>() ? e.GetComponent<ActorBinder>().Actor : null;

    public bool TryToAttack(BaseSimObject target)
    {
        var actor = Base;
        if (actor.isInLiquid() && !actor.asset.oceanCreature) return false;
        if (!actor.isAttackReady()) return false;

        CombatActionAsset basic_attack_action = null;
        
        using var attack_action_pool = new ListPool<CombatActionAsset>();
        if (!attack_action_pool.Any()) return false;
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
        // TODO: 加入技能
        

        AttackData attack_data = new(actor, target.currentTile, default, target);
        
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

        void attack_succeed(CombatActionAsset combat_action)
        {
            actor.timer_action = actor.s_attackSpeed_seconds;
            actor.attackTimer = actor.s_attackSpeed_seconds;
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

    public List<Entity> GetItems()
    {
        return e.GetRelations<InventoryRelation>().Select(x => x.item).ToList();
    }

    public static void RegisterActionOnNewCreature(Action<ActorExtend> action)
    {
        action_on_new_creature += action;
    }

    public override string ToString()
    {
        return $"[{e.GetComponent<ActorBinder>().id}] {Base.getName()}: {e}";
    }

    public void AddSkillModifier<TModifier, TValue>(string action_id, TModifier modifier)
        where TModifier : struct, IModifier<TValue>
    {
        if (!_skill_actions.TryGetValue(action_id, out Entity action_entity))
        {
            action_entity = TriggerActionBaseMeta.AllDict[action_id].NewModifierContainer();
            _skill_actions[action_id] = action_entity;
        }

        action_entity.AddComponent(modifier);
    }

    public void LearnSkill(string id)
    {
        _learned_skills.Add(id);
    }

    public void CastSkillV2(string id, BaseSimObject target_obj)
    {
        ModClass.I.SkillV2.NewSkillStarter(id, this, target_obj, 100);
    }

    public Entity GetSkillActionEntity(string action_id, Entity default_action)
    {
        return _skill_actions.TryGetValue(action_id, out var res) ? res : default_action;
    }


    [Hotfixable]
    private void TestCastFireball()
    {
        Actor target = null;
        do
        {
            target = World.world.units.GetRandom();
        } while (!Base.kingdom.isEnemy(target.kingdom));

        CastSkillV2(ExampleTriggerActions.StartSkillFireball.id, target);
        ModClass.LogInfo($"{Base.data.id} cast fireball to {target.data.id}");
    }

    private void TestCastCommonWeapon()
    {
        Actor target = null;
        do
        {
            target = World.world.units.GetRandom();
        } while (!Base.kingdom.isEnemy(target.kingdom));

        CastSkillV2(CommonWeaponSkills.StartWeaponSkill.id, target);
        ModClass.LogInfo($"{Base.data.id} cast weapon to {target.data.id}");
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
    public float GetStat(string stat_id)
    {
        action_on_get_stats?.Invoke(this, stat_id);
        return Base.stats[stat_id];
    }
    internal void ExtendNewCreature()
    {
        // 灵根
        var has_element_root = Base.asset.GetExtend<ActorAssetExtend>().must_have_element_root ||
                               Toolbox.randomChance(ModClass.L.ElementRootLibrary.base_prob);
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

            damage_ratio *= total_ratio / sum;
            total_ratio = 0f;
            sum = 0f;
            for (var i = 5; i < 7; i++)
            {
                total_ratio += damage_composition[i] * (1 - s_armor[i]);
                sum += damage_composition[i];
            }

            damage_ratio *= total_ratio / sum * (1 - s_armor[ElementIndex.Entropy]);
            damage = Mathf.Clamp(damage * damage_ratio, 0, int.MaxValue >> 2);

            Base.data.health -= (int)damage;
        }

        // 补齐原版的一些效果// 补齐原版的一些效果
        int health_before = Base.data.health;

        if (Base.data.health <= 0) Base.data.health = 1;
        PatchActor.getHit_snapshot(Base, 0, pAttacker: attacker);
        
        Base.data.health = health_before; // 防止强制扣血
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

    internal void SelfDestroy()
    {
        e.DeleteEntity();

        foreach (var skill_action in _skill_actions.Values)
        {
            skill_action.DeleteEntity();
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
}