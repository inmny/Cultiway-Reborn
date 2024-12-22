using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content.Components;
using Cultiway.Core;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Libraries;

public struct ElixirIngrediantCheck
{
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

        return true;
    }
}

public delegate void ElixirCraftDelegate(ActorExtend ae, Entity elixir_entity, Entity[] ingrediants);

public delegate void ElixirConsumedDelegate(ActorExtend ae, Entity elixir_entity, ref Elixir elixir);

public delegate bool ElixirCheckDelegate(ActorExtend ae, Entity elixir_entity, ref Elixir elixir);

public class ElixirAsset : Asset
{
    public ElixirCheckDelegate    consumable_check_action;
    public ElixirConsumedDelegate consumed_action;
    public ElixirCraftDelegate    craft_action;
    public ElixirIngrediantCheck[] ingrediants;
    public string                  name_key;

    [Hotfixable]
    public void Craft(ActorExtend ae, Entity crafting_elixir_entity, IHasInventory receiver, Entity[] corr_ingrediants)
    {
        crafting_elixir_entity.AddComponent(new Elixir
        {
            elixir_id = id
        });
        craft_action?.Invoke(ae, crafting_elixir_entity, corr_ingrediants);
        for (var i = 0; i < corr_ingrediants.Length; i++) corr_ingrediants[i].DeleteEntity();
        crafting_elixir_entity.RemoveComponent<CraftingElixir>();

        receiver.AddSpecialItem(crafting_elixir_entity);
    }

    public bool QueryInventoryForIngrediants(IHasInventory inv, out Entity[] corr_ingrediants)
    {
        var check_result = new Entity[ingrediants.Length];
        var items = inv.GetItems();
        foreach (Entity item in items)
        {
            if (item.GetIncomingLinks<CraftOccupyingRelation>().Count > 0) continue;
            for (var i = 0; i < ingrediants.Length; i++)
            {
                if (!check_result[i].IsNull) continue;
                if (ingrediants[i].Check(item))
                {
                    check_result[i] = item;
                    break;
                }
            }
        }

        var res = check_result.All(x => !x.IsNull);
        corr_ingrediants = res ? check_result : null;
        return res;
    }
}