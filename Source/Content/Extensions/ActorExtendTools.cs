using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Libraries;
using Cultiway.Core;
using Cultiway.Core.SkillLibV3;
using Cultiway.Core.SkillLibV3.Components;
using Cultiway.Core.SkillLibV3.Modifiers;
using Cultiway.Core.SkillLibV3.Utils;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using UnityEngine;

namespace Cultiway.Content.Extensions;

public static class ActorExtendTools
{
    /// <summary>
    ///     使用丹药
    /// </summary>
    /// <remarks>一旦使用成功，丹药实体将被删除</remarks>
    /// <returns>是否使用成功 </returns>
    [Hotfixable]
    public static bool TryConsumeElixir(this ActorExtend ae, Entity elixir_entity)
    {
        ref Elixir elixir = ref elixir_entity.GetComponent<Elixir>();
        ElixirAsset elixir_asset = Libraries.Manager.ElixirLibrary.get(elixir.elixir_id);
        try
        {
            if (elixir_asset.consumable_check_action?.Invoke(ae, elixir_entity, ref elixir) ?? true)
                elixir_asset.effect_action?.Invoke(ae, elixir_entity, ref elixir);
            else
                return false;
        }
        catch (Exception e)
        {
            ModClass.LogError($"[{elixir_entity.Id}] ElixirAsset({elixir.elixir_id}).consumed_action error: {e}");
            return false;
        }

        ModClass.LogInfo($"[{ae}] consumes {elixir_entity.Id}({elixir.elixir_id})");
        elixir_entity.DeleteEntity();
        return true;
    }
    public static void EnhanceSkillRandomly(this ActorExtend ae, string source)
    {
        if (ae.all_skills.Count > 0)
        {
            var skill_container_entity = ae.all_skills.GetRandom();

            var builder = new SkillContainerBuilder(skill_container_entity);
            var existing_ids = CollectModifierIds(skill_container_entity);
            var candidate_assets = new List<SkillModifierAsset>();
            var weight_accum = new List<int>();
            var conflict_tags = CollectConflictTags(existing_ids);
            var total = 0;
            foreach (SkillModifierAsset asset in ModClass.I.SkillV3.ModifierLib.list)
            {
                if (asset == null) continue;
                if (asset.id == PlaceholderModifier.PlaceholderAssetId) continue;
                var weight = asset.Rarity.Weight();
                if (weight <= 0) continue;
                var alreadyHas = existing_ids.Contains(asset.id);
                if (!alreadyHas && asset.ConflictTags.Any(conflict_tags.Contains)) continue;

                total += weight;
                candidate_assets.Add(asset);
                weight_accum.Add(total);
            }

            if (candidate_assets.Count == 0) return;

            var chosen_index = RdUtils.RandomIndexWithAccumWeight(weight_accum.ToArray());
            var chosen_asset = candidate_assets[chosen_index];

            if (chosen_asset.OnAddOrUpgrade?.Invoke(builder) ?? false)
            {
                //ModClass.LogInfo($"[{ae}] enhanced {skill_container_entity.Id}({skill_container_entity.GetComponent<SkillContainer>().Asset})");
                builder.Build();
            }
        }
    }
    public static bool RestoreWakan(this ActorExtend ae, float value)
    {
        if (value <= 0) return false;
        if (!ae.HasCultisys<Xian>()) return false;
        ref Xian xian = ref ae.GetCultisys<Xian>();
        xian.wakan = Mathf.Min(xian.wakan + value,
            Mathf.Max(xian.wakan, ae.Base.stats[BaseStatses.MaxWakan.id] * XianSetting.WakanRestoreLimit));
        return true;
    }

    public static bool HasCultibook(this ActorExtend ae)
    {
        return ae.HasMaster<CultibookAsset>();
    }
/*
    public static CultibookMasterRelation GetCultibookMasterRelation(this ActorExtend ae)
    {
        return ae.E.GetRelations<CultibookMasterRelation>().First();
    }

    public static void SetCultibookMasterRelation(this ActorExtend ae, Entity cultibook, float master_value)
    {
        if (ae.E.GetRelations<CultibookMasterRelation>().Any())
        {
            ae.E.GetRelation<CultibookMasterRelation, Entity>(cultibook).MasterValue = master_value;
        }
        else
        {
            ae.E.AddRelation(new CultibookMasterRelation()
            {
                Cultibook = cultibook,
                MasterValue = master_value
            });
        }
    }
*/
    public static ref Yuanying GetYuanying(this ActorExtend ae)
    {
        return ref ae.GetComponent<Yuanying>();
    }
    public static ref Jindan GetJindan(this ActorExtend ae)
    {
        return ref ae.GetComponent<Jindan>();
    }
    public static ref XianBase GetXianBase(this ActorExtend ae)
    {
        return ref ae.GetComponent<XianBase>();
    }


    private static HashSet<string> CollectModifierIds(Entity skill_container_entity)
    {
        var ids = new HashSet<string>();
        if (skill_container_entity.IsNull) return ids;

        foreach (var component_type in skill_container_entity.GetComponentTypes())
        {
            if (!typeof(IModifier).IsAssignableFrom(component_type)) continue;
            var modifier = (IModifier)skill_container_entity.GetComponent(component_type);
            ids.Add(modifier.ModifierAsset.id);
            if (modifier is PlaceholderModifier placeholder && placeholder.ModifierAssetIds != null)
            {
                ids.UnionWith(placeholder.ModifierAssetIds);
            }
        }

        return ids;
    }

    private static HashSet<string> CollectConflictTags(HashSet<string> modifierIds)
    {
        var tags = new HashSet<string>();
        foreach (var id in modifierIds)
        {
            var asset = ModClass.I.SkillV3.ModifierLib.get(id);
            if (asset == null) continue;
            tags.UnionWith(asset.ConflictTags);
        }

        return tags;
    }
}
