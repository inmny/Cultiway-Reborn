using System;
using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.Skills;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Examples;
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