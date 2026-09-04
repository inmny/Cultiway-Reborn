using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Cultiway.Patch;
using Cultiway.Utils.Extension;
using UnityEngine;

namespace Cultiway.Content;

/// <summary>捕获、校验、清除和采用物质肉身的公共入口，不处理修炼与身份。</summary>
public static class PhysicalBodyService
{
    /// <summary>属于物质肉身的原版特质组。</summary>
    private static readonly HashSet<string> BodyTraitGroups = new(StringComparer.Ordinal)
    {
        "body",
        "physique",
        "health",
        "appearance",
        "protection"
    };

    /// <summary>从当前人物严格捕获物质肉身和灵根。</summary>
    /// <param name="actor">具有实际肉身的人物。</param>
    /// <param name="snapshot">返回不持有游戏对象引用的快照。</param>
    /// <returns>形态、亚种、数据和灵根完整时返回真。</returns>
    public static bool TryCapture(Actor actor, out PhysicalBodySnapshot snapshot)
    {
        snapshot = default;
        if (actor == null || actor.isRekt() || actor.asset == null || actor.subspecies == null ||
            actor.data == null) return false;
        ActorExtend extend = actor.GetExtend();
        if (extend == null || !extend.HasElementRoot()) return false;
        ref ElementRoot root = ref extend.GetElementRoot();
        var rootValues = new float[Cultiway.Const.ElementIndex.Count];
        for (var i = 0; i < rootValues.Length; i++) rootValues[i] = root[i];
        string[] traits = actor.traits
            .Where(trait => trait != null && BodyTraitGroups.Contains(trait.group_id))
            .Select(trait => trait.id)
            .Distinct()
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        snapshot = new PhysicalBodySnapshot
        {
            actor_asset_id = actor.asset.id,
            subspecies_id = actor.subspecies.data.id,
            sex = actor.data.sex,
            head = actor.data.head,
            phenotype_index = actor.data.phenotype_index,
            phenotype_shade = actor.data.phenotype_shade,
            body_age = actor.data.getAge(),
            body_trait_ids = traits,
            element_root_values = rootValues
        };
        return snapshot.IsValid;
    }

    /// <summary>核对宿主在引导期间是否仍是开始时冻结的同一具物质肉身。</summary>
    /// <param name="actor">当前宿主人物。</param>
    /// <param name="expected">引导开始时的肉身快照。</param>
    /// <returns>形态、亚种、外观、肉身特质和灵根均未变化时返回真。</returns>
    public static bool MatchesSnapshot(Actor actor, in PhysicalBodySnapshot expected)
    {
        if (!TryCapture(actor, out PhysicalBodySnapshot current) || !expected.IsValid) return false;
        if (!string.Equals(current.actor_asset_id, expected.actor_asset_id, StringComparison.Ordinal) ||
            current.subspecies_id != expected.subspecies_id || current.sex != expected.sex ||
            current.head != expected.head || current.phenotype_index != expected.phenotype_index ||
            current.phenotype_shade != expected.phenotype_shade ||
            !current.body_trait_ids.SequenceEqual(expected.body_trait_ids)) return false;
        for (var i = 0; i < current.element_root_values.Length; i++)
            if (!Mathf.Approximately(current.element_root_values[i], expected.element_root_values[i])) return false;
        return true;
    }

    /// <summary>在提交前严格解析快照中所有资产和特质。</summary>
    /// <param name="snapshot">待解析快照。</param>
    /// <param name="bodyAsset">返回人物形态资产。</param>
    /// <param name="subspecies">返回原亚种。</param>
    /// <param name="traits">返回全部肉身特质。</param>
    /// <returns>每个稳定编号都能精确解析时返回真。</returns>
    public static bool TryResolve(
        in PhysicalBodySnapshot snapshot,
        out ActorAsset bodyAsset,
        out Subspecies subspecies,
        out ActorTrait[] traits)
    {
        bodyAsset = null;
        subspecies = null;
        traits = null;
        if (!snapshot.IsValid || World.world?.subspecies == null) return false;
        bodyAsset = AssetManager.actor_library.get(snapshot.actor_asset_id);
        subspecies = World.world.subspecies.get(snapshot.subspecies_id);
        if (bodyAsset == null || subspecies == null || subspecies.isRekt()) return false;
        traits = new ActorTrait[snapshot.body_trait_ids.Length];
        for (var i = 0; i < traits.Length; i++)
        {
            ActorTrait trait = AssetManager.traits.get(snapshot.body_trait_ids[i]);
            if (trait == null || !BodyTraitGroups.Contains(trait.group_id)) return false;
            traits[i] = trait;
        }
        return true;
    }

    /// <summary>让同一人物采用已经预先校验的肉身，不修改修炼、技能或关系。</summary>
    /// <param name="actor">继续承载原身份的人物。</param>
    /// <param name="snapshot">物质肉身快照。</param>
    /// <returns>严格解析并完成采用时返回真。</returns>
    public static bool TryApply(Actor actor, in PhysicalBodySnapshot snapshot)
    {
        if (actor == null || actor.isRekt() ||
            !TryResolve(snapshot, out ActorAsset asset, out Subspecies subspecies, out ActorTrait[] traits))
            return false;
        ActorExtend extend = actor.GetExtend();
        RemoveBodyTraits(actor);
        for (var i = 0; i < traits.Length; i++) actor.addTrait(traits[i], true);
        var rootValues = new float[Cultiway.Const.ElementIndex.Count];
        Array.Copy(snapshot.element_root_values, rootValues, rootValues.Length);
        extend.GetOrAddComponent<ElementRoot>() = new ElementRoot(rootValues);
        actor.setAsset(asset);
        actor.setSubspecies(subspecies);
        actor.data.sex = snapshot.sex;
        actor.data.head = snapshot.head;
        actor.data.phenotype_index = snapshot.phenotype_index;
        actor.data.phenotype_shade = snapshot.phenotype_shade;
        actor.data.age_overgrowth = snapshot.body_age - Date.getYearsSince(actor.data.created_time);
        actor.setFlying(asset.flying);
        actor.setShowShadow(asset.shadow);
        actor.clearGraphicsFully();
        extend.MarkCultiwayStatsDirty(false);
        extend.MarkCultiwaySkillCacheDirty(false);
        extend.MarkSemanticProfileDirty();
        CoreFormationEffectResolver.Synchronize(extend);
        actor.setStatsDirty();
        actor.updateStats();
        actor.city?.setCitizensDirty();
        return true;
    }

    /// <summary>失去肉身时释放普通装备，并清除肉身特质与灵根。</summary>
    /// <param name="actor">即将转为无身形态的人物。</param>
    public static void ReleaseAndStrip(Actor actor)
    {
        if (actor == null) return;
        if (actor.equipment != null && actor.hasEquipment())
        {
            List<Item> items = actor.equipment.getItems().ToList();
            actor.equipment.destroyAllEquipment();
            if (actor.current_tile?.zone?.hasCity() == true)
                actor.current_tile.zone.city.tryToPutItems(items);
        }
        RemoveBodyTraits(actor);
        ActorExtend extend = actor.GetExtend();
        if (extend.HasElementRoot()) extend.E.RemoveComponent<ElementRoot>();
    }

    /// <summary>终止明确宿主人物且禁止元婴出逃，用于已经冻结的身体转移提交。</summary>
    /// <param name="host">被终止的宿主人物。</param>
    /// <param name="source">身体接收者和死亡归因人物。</param>
    /// <returns>宿主不再存活时返回真。</returns>
    public static bool TerminateHostForTransfer(Actor host, Actor source)
    {
        if (host == null || host.isRekt() || source == null || source.isRekt()) return false;
        ReleaseAndStrip(host);
        float lethalDamage = Mathf.Max(host.data.health, host.getMaxHealth()) + 1f;
        PatchActor.getHit_snapshot(
            host,
            lethalDamage,
            pFlash: false,
            pAttackType: AttackType.Metamorphosis,
            pAttacker: source,
            pSkipIfShake: false,
            pMetallicWeapon: false,
            pCheckDamageReduction: false);
        if (host.isAlive())
        {
            host.setHealth(0);
            host.checkDeath();
        }
        if (host.isAlive()) host.dieAndDestroy(AttackType.Metamorphosis);
        return !host.isAlive() || host.isRekt();
    }

    /// <summary>移除人物当前全部物质肉身特质。</summary>
    /// <param name="actor">需要清理的人物。</param>
    private static void RemoveBodyTraits(Actor actor)
    {
        if (actor == null) return;
        ActorTrait[] traits = actor.traits
            .Where(trait => trait != null && BodyTraitGroups.Contains(trait.group_id))
            .ToArray();
        actor.removeTraits(traits);
    }
}
