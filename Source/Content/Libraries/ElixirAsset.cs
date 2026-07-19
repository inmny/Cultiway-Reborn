using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.AIGC;
using Cultiway.Content.Artifacts;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Content.Events;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using NeoModLoader.General;
using UnityEngine;

namespace Cultiway.Content.Libraries;

public struct ElixirIngredientCheck
{
    public string ingredient_name;
    public bool   need_element_root;
    public string element_root_id;
    public bool   need_jindan;

    /// <summary>需要精确匹配的金丹规范名称；为空时仅要求材料含有金丹。</summary>
    public string jindan_name;

    /// <summary>判断该材料槽是否要求携带任意或指定类型的灵根。</summary>
    public bool NeedElementRoot()
    {
        return need_element_root || !string.IsNullOrEmpty(element_root_id);
    }

    /// <summary>判断该材料槽是否要求携带任意或指定名称的金丹。</summary>
    public bool NeedJindan()
    {
        return need_jindan || !string.IsNullOrEmpty(jindan_name);
    }

    /// <summary>判断该材料槽是否还要求精确匹配掉落物名称。</summary>
    public bool NeedIngredientName()
    {
        return !string.IsNullOrEmpty(ingredient_name);
    }

    /// <summary>
    ///     数量，暂时不支持多个
    /// </summary>
    public int count;

    /// <summary>按灵根、金丹规范名称和材料名称依次验证一个候选实体。</summary>
    public bool Check(Entity item_entity)
    {
        if (NeedElementRoot())
        {
            if (!item_entity.HasComponent<ElementRoot>()) return false;
            if (!string.IsNullOrEmpty(element_root_id))
                if (item_entity.GetComponent<ElementRoot>().Type.id != element_root_id)
                    return false;
        }

        if (NeedJindan())
        {
            if (!item_entity.HasComponent<Jindan>()) return false;
            if (!string.IsNullOrEmpty(jindan_name))
                if (!string.Equals(item_entity.GetComponent<Jindan>().GetName(), jindan_name,
                        StringComparison.Ordinal))
                    return false;
        }

        if (NeedIngredientName())
        {
            if (!item_entity.TryGetComponent(out EntityName name)) return false;
            if (name.value != ingredient_name) return false;
        }

        return true;
    }

    /// <summary>返回该材料槽最主要的可显示条件名称。</summary>
    public string GetName()
    {
        if (NeedElementRoot())
        {
            return element_root_id.Localize();
        }

        if (NeedJindan())
        {
            return jindan_name;
        }

        if (NeedIngredientName())
        {
            return ingredient_name;
        }

        return null;
    }
}

public delegate void ElixirCraftDelegate(ActorExtend ae, Entity elixir_entity, Entity[] ingredients);

public delegate void ElixirEffectDelegate(ActorExtend ae, Entity elixir_entity, ref Elixir elixir);

public delegate bool ElixirCheckDelegate(ActorExtend ae, Entity elixir_entity, ref Elixir elixir);

public class ElixirAsset : Asset, IDeleteWhenUnknown
{
    public ElixirCheckDelegate  consumable_check_action;
    public ElixirCraftDelegate  craft_action;
    public ElixirEffectDelegate effect_action;
    public ElixirEffectType     effect_type;
    public ElixirIngredientCheck[] ingredients;
    public ItemLevel base_level;
    public bool has_recipe_semantic;
    public ElixirRecipeContext recipe_semantic;
    /// <summary>
    /// 仅用于动态生成的丹药, 用于保证随机效果的一致性（存读档）
    /// </summary>
    public int seed_for_random_effect;
    public string                  name_key;
    public string description_key;
    /// <summary>
    /// 动态效果是否已生成
    /// </summary>
    public bool effect_ready = true;
    
    public string GetName()
    {
        if (string.IsNullOrEmpty(name_key))
        {
            if (has_recipe_semantic)
            {
                name_key = ElixirNameGenerator.GenerateDefaultName(this);
            }
            else
            {
                var checks = ingredients ?? Array.Empty<ElixirIngredientCheck>();
                var param = new string[checks.Length + 1];
                param[0] = description_key ?? string.Empty;
                for (int i=0;i<checks.Length;i++)
                {
                    param[i+1] = checks[i].ingredient_name;
                }

                name_key = ElixirNameGenerator.Instance.GenerateName(param);
                if (string.IsNullOrEmpty(name_key))
                {
                    name_key = ElixirNameGenerator.GenerateDefaultName(this);
                }
            }
        }
        if (LM.Has(name_key))
        {
            return LM.Get(name_key);
        }

        return name_key;
    }
    public void SetupDataGain(ElixirEffectDelegate effect_action)
    {
        this.effect_action = effect_action;
        effect_type = ElixirEffectType.DataGain;
    }
    public void SetupDataChange(ElixirEffectDelegate effect_action)
    {
        this.effect_action = effect_action;
        effect_type = ElixirEffectType.DataChange;
    }
    public void SetupRestore(ElixirEffectDelegate effect_action)
    {
        this.effect_action = effect_action;
        effect_type = ElixirEffectType.Restore;
    }
    public void SetupStatusGain(StatusComponent status_given, StatusOverwriteStats overwrite_stats = default)
    {
        effect_action = (ActorExtend ae, Entity elixir_entity, ref Elixir elixir) =>
        {
            var status = status_given.Type.NewEntity();
            if (overwrite_stats != default)
            {
                status.AddComponent(overwrite_stats);
            }
            ae.AddSharedStatus(status_given.Type.NewEntity());
        };
        effect_type = ElixirEffectType.StatusGain;
    }
    public void SetupStatusGain(ElixirEffectDelegate effect_action)
    {
        this.effect_action = effect_action;
        effect_type = ElixirEffectType.StatusGain;
    }

    [Hotfixable]
    public void Craft(ActorExtend ae, Entity crafting_elixir_entity, IHasInventory receiver, Entity[] corr_ingredients)
    {
        var elixir_component = new Elixir
        {
            elixir_id = id
        };
        switch (effect_type)
        {
            case ElixirEffectType.Restore:
                crafting_elixir_entity.Add(elixir_component, Tags.Get<TagElixirRestore>());
                break;
            case ElixirEffectType.DataChange:
                crafting_elixir_entity.Add(elixir_component, Tags.Get<TagElixirDataChange>());
                break;
            case ElixirEffectType.StatusGain:
                crafting_elixir_entity.Add(elixir_component, Tags.Get<TagElixirStatusGain>());
                break;
            case ElixirEffectType.DataGain:
                crafting_elixir_entity.Add(elixir_component, Tags.Get<TagElixirDataGain>());
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        craft_action?.Invoke(ae, crafting_elixir_entity, corr_ingredients);
        for (var i = 0; i < corr_ingredients.Length; i++) 
        {
            corr_ingredients[i].DeleteEntity();
        }

        crafting_elixir_entity.RemoveComponent<CraftingElixir>();
        crafting_elixir_entity.RemoveTag<TagUncompleted>();
        crafting_elixir_entity.AddComponent(new EntityName(GetName()));
        var value = crafting_elixir_entity.GetComponent<Elixir>().value;
        var level = base_level;
        if (value > 0)
        {
            var level_addition = (int)Mathf.Log10(value);
            level.Level += level_addition;
            var overflow_level = level.Level - 8;
            if (overflow_level > 0)
            {
                level.Level = 8;
                var stage_addition = (int)Mathf.Log10(overflow_level);
                level.Stage += stage_addition;
                level.Stage = Mathf.Min(level.Stage, 3);
            }
        }
        ArtifactProductionResultEvent productionResult = ArtifactProductionService.DispatchResult(
            ae,
            ArtifactProductionProcesses.Alchemy,
            this,
            crafting_elixir_entity);
        ElixirCraftResultEvent result = new(this, crafting_elixir_entity)
        {
            QualityBonus = productionResult.QualityBonus,
        };
        ArtifactAbilityDispatcher.Dispatch(ae.E, result);
        level = ItemLevel.FromValue(level + result.QualityBonus);
        if (crafting_elixir_entity.HasComponent<ItemLevel>())
        {
            ref var existing_level = ref crafting_elixir_entity.GetComponent<ItemLevel>();
            existing_level.Level = Mathf.Max(level.Level, existing_level.Level);
            existing_level.Stage = Mathf.Max(level.Stage, existing_level.Stage);
        }
        else
        {
            crafting_elixir_entity.AddComponent(level);
        }

        int outputCount = ArtifactProductionService.ResolveOutputCount(productionResult.YieldMultiplier);
        ArtifactProductionService.AddOutputs(receiver, crafting_elixir_entity, outputCount);
    }

    public bool QueryInventoryForIngredients(IHasInventory inv, out Entity[] corr_ingredients)
    {
        if (ingredients == null || ingredients.Length == 0)
        {
            corr_ingredients = null;
            return false;
        }
        var check_result = new Entity[ingredients.Length];
        var items = inv.GetItems();
        foreach (Entity item in items)
        {
            if (item.Tags.HasAny(Tags.Get<TagConsumed, TagOccupied>())) continue;
            for (var i = 0; i < ingredients.Length; i++)
            {
                if (!check_result[i].IsNull) continue;
                if (ingredients[i].Check(item))
                {
                    check_result[i] = item;
                    break;
                }
            }
        }

        var res = check_result.All(x => !x.IsNull);
        corr_ingredients = res ? check_result : null;
        return res;
    }

    public void OnDelete()
    {
    }

    public int Current { get; set; } = 0;
}
