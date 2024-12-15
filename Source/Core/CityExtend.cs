using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Friflo.Engine.ECS;
using HarmonyLib;

namespace Cultiway.Core;

public class CityExtend : ExtendComponent<City>
{
    private readonly Entity e;

    public CityExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<CityBinder>()._ce = this;
    }

    public          Entity E    => e;
    public override City   Base => e.HasComponent<CityBinder>() ? e.GetComponent<CityBinder>().City : null;

    public override string ToString()
    {
        return $"[{e.GetComponent<CityBinder>().id}] {Base.getCityName()}: {e}";
    }

    public void TestAddSpecialItem()
    {
        AddSpecialItem(SpecialItemUtils.StartBuild(ItemShapes.Ball.id).Build());
    }

    public void AddSpecialItem(Entity item_entity)
    {
        item_entity.GetIncomingLinks<InventoryRelation>().Entities
            .Do(owner => owner.RemoveRelation<InventoryRelation>(item_entity));
        e.AddRelation(new InventoryRelation { item = item_entity });
    }

    public List<SpecialItem> GetSpecialItems()
    {
        return e.GetRelations<InventoryRelation>().Select(x => x.item.GetComponent<SpecialItem>()).ToList();
    }
}