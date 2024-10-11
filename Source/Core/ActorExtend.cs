using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Skills;
using Cultiway.Content.Skills.Modifiers;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLib.Components.Triggers;
using Cultiway.Patch;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Core;

public class ActorExtend : ExtendComponent<Actor>
{
    private static Action<ActorExtend> action_on_new_creature;

    private static   Action<ActorExtend> action_on_update_stats;
    private readonly Entity              e;

    private  Dictionary<string, Entity> _skill_actions = new();
    internal float[]                    s_damage_ratio = new float[7];

    public ActorExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<ActorBinder>()._ae = this;
    }

    public override Actor Base => e.GetComponent<ActorBinder>().Actor;

    public static void RegisterActionOnNewCreature(Action<ActorExtend> action)
    {
        action_on_new_creature += action;
    }

    public override string ToString()
    {
        return $"{e.GetComponent<ActorBinder>().id}: {e}";
    }

    public void CastSkill(string id, BaseSimObject target_obj)
    {
        if (!_skill_actions.TryGetValue(id, out var start_action_entity))
        {
            return;
        }

        ref var start_action = ref start_action_entity.GetComponent<StartObjActionContainerInfo>();
        StartObjTrigger trigger = new()
        {
            target = target_obj
        };
        Entity skill_entity = ModClass.I.Skill.RequestSkillEntity(this, target_obj, target_obj.currentTile, 1);
        start_action.Meta.action(ref trigger, ref skill_entity, ref start_action_entity);
    }

    public void CastSkillV2(string id, BaseSimObject target_obj)
    {
    }

    public Entity GetSkillActionEntity(string action_id, Entity default_action = default)
    {
        return _skill_actions.TryGetValue(action_id, out var res) ? res : default_action;
    }

    private void TestAddSkill()
    {
        _skill_actions[nameof(SkillTriggerActions.FireballStarter)] = SkillTriggerActions.FireballStarter.NewEntity();
    }

    [Hotfixable]
    private void TestAddCastNumMod(int num)
    {
        if (!_skill_actions.TryGetValue(nameof(SkillTriggerActions.FireballStarter), out var action_entity))
        {
            action_entity = SkillTriggerActions.FireballStarter.NewEntity();
            _skill_actions[nameof(SkillTriggerActions.FireballStarter)] = action_entity;
        }

        action_entity.GetComponent<CastNum>().Value = num;
    }

    [Hotfixable]
    private void TestCastSkill()
    {
        Actor target = null;
        do
        {
            target = World.world.units.GetRandom();
        } while (!Base.kingdom.isEnemy(target.kingdom));

        CastSkill(nameof(SkillTriggerActions.FireballStarter), target);
        ModClass.LogInfo($"{Base.data.id} cast skill to {target.data.id}");
    }

    internal void ExtendNewCreature()
    {
        // 灵根
        bool has_element_root = Toolbox.randomChance(ModClass.L.ElementRootLibrary.base_prob);
        if (has_element_root)
        {
            var composition = new float[5];
            float sum = 0;
            for (int i = 0; i < 5; i++)
            {
                composition[i] = Toolbox.randomFloat(0, 1);
                sum += composition[i];
            }

            for (int i = 0; i < 5; i++)
            {
                composition[i] /= sum;
            }

            e.AddComponent(new ElementRoot(composition));
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

        action_on_update_stats?.Invoke(this);
    }

    internal void PostUpdateStats()
    {
        var stats = Base.stats;

        var armor = stats[S.armor];
        s_damage_ratio[0] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        armor = stats[nameof(CoreBaseStats.IronArmor)];
        s_damage_ratio[DamageIndex.Iron] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        armor = stats[nameof(CoreBaseStats.WoodArmor)];
        s_damage_ratio[DamageIndex.Wood] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        armor = stats[nameof(CoreBaseStats.WaterArmor)];
        s_damage_ratio[DamageIndex.Water] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        armor = stats[nameof(CoreBaseStats.FireArmor)];
        s_damage_ratio[DamageIndex.Fire] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        armor = stats[nameof(CoreBaseStats.EarthArmor)];
        s_damage_ratio[DamageIndex.Earth] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
        armor = stats[nameof(CoreBaseStats.SoulArmor)];
        s_damage_ratio[DamageIndex.Soul] = armor / (armor + DamageCalcHyperParameters.ArmorEffectDecay);
    }

    public void NewCultisys<T>(CultisysAsset<T> cultisys) where T : struct, ICultisysComponent
    {
        e.AddComponent(cultisys.DefaultComponent);
        cultisys.OnGetAction?.Invoke(this, cultisys, ref e.GetComponent<T>());
    }

    public void GetHit(float damage, ref DamageComposition damage_composition, BaseSimObject attacker)
    {
        damage *= 1 - s_damage_ratio[0];
        var total_ratio = 0f;
        var sum = 0f;
        for (int i = 0; i < 6; i++)
        {
            total_ratio += damage_composition[i] * (1 - s_damage_ratio[i + 1]);
            sum += damage_composition[i];
        }

        damage *= total_ratio / sum;

        damage = Mathf.Clamp(damage, 0, int.MaxValue >> 2);

        Base.data.health -= (int)damage;

        // 补齐原版的一些效果
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

    internal void SelfDestroy()
    {
        e.DeleteEntity();

        foreach (var skill_action in _skill_actions.Values)
        {
            skill_action.DeleteEntity();
        }
    }
}