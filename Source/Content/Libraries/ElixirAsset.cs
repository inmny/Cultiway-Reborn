using System;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.AIGC;
using Cultiway.Content.Components;
using Cultiway.Content.Const;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;
using NeoModLoader.General;

namespace Cultiway.Content.Libraries;

public struct ElixirIngredientCheck
{
    public string ingredient_name;
    public bool   need_element_root;
    public string element_root_id;
    public bool   need_jindan;
    public string jindan_id;

    public bool NeedElementRoot()
    {
        return need_element_root || !string.IsNullOrEmpty(element_root_id);
    }

    public bool NeedJindan()
    {
        return need_jindan || !string.IsNullOrEmpty(jindan_id);
    }

    public bool NeedIngredientName()
    {
        return !string.IsNullOrEmpty(ingredient_name);
    }

    /// <summary>
    ///     数量，暂时不支持多个
    /// </summary>
    public int count;

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
            if (!string.IsNullOrEmpty(jindan_id))
                if (item_entity.GetComponent<Jindan>().Type.id != jindan_id)
                    return false;
        }

        if (NeedIngredientName())
        {
            if (!item_entity.TryGetComponent(out EntityName name)) return false;
            if (name.value != ingredient_name) return false;
        }

        return true;
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
    /// <summary>
    /// 仅用于动态生成的丹药, 用于保证随机效果的一致性（存读档）
    /// </summary>
    public int seed_for_random_effect;
    public string                  name_key;
    public string description_key;
    
    public string GetName()
    {
        if (string.IsNullOrEmpty(name_key))
        {
            var param = new string[ingredients.Length + 1];
            param[0] = description_key;
            for (int i=0;i<ingredients.Length;i++)
            {
                param[i+1] = ingredients[i].ingredient_name;
            }

            name_key = ElixirNameGenerator.Instance.GenerateName(param);
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
        for (var i = 0; i < corr_ingredients.Length; i++) corr_ingredients[i].DeleteEntity();

        crafting_elixir_entity.RemoveComponent<CraftingElixir>();
        crafting_elixir_entity.AddComponent(new EntityName(GetName()));

        receiver.AddSpecialItem(crafting_elixir_entity);
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
            if (item.GetIncomingLinks<CraftOccupyingRelation>().Count > 0) continue;
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

    public int Current { get; set; } = 0;
}