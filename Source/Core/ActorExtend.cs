using System;
using System.Collections.Generic;
using System.Text;
using Cultiway.Abstract;
using Cultiway.Const;
using Cultiway.Content.CultisysComponents;
using Cultiway.Content.Skills;
using Cultiway.Core.Components;
using Cultiway.Core.Libraries;
using Cultiway.Core.SkillLibV2;
using Cultiway.Core.SkillLibV2.Api;
using Cultiway.Core.SkillLibV2.Examples;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Core;

public class ActorExtend : ExtendComponent<Actor>
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

        StringBuilder sb = new();
        var attacker_exist = attacker != null && attacker.isActor() && attacker.a.GetExtend().HasCultisys<Xian>();
        sb.Append(
            $"{Base.data.id} get hit by {attacker?.base_data.id ?? "null"}(from Xian: {attacker_exist}) with {damage} damage, ");
        damage = Mathf.Clamp(damage * damage_ratio, 0, int.MaxValue >> 2);
        sb.Append($"after armor {damage} damage\n");
        for (var i = 0; i < 8; i++)
            sb.AppendLine($"{ElementIndex.ElementNames[i]}: {damage_composition[i]}, {s_armor[i]}");

        sb.AppendLine($"Armor: {s_armor[ElementIndex.Entropy + 1]}");

        if (HasCultisys<Xian>()) ModClass.LogInfo(sb.ToString());

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
}