using System.Collections.Generic;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

/// <summary>
/// 管理宗门藏宝阁的运行期库存和物权关系。
/// </summary>
public sealed class SectTreasureInventory
{
    private readonly Sect _sect;
    private Entity _entity;

    internal SectTreasureInventory(Sect sect)
    {
        _sect = sect;
    }

    internal void Initialize()
    {
        if (!_entity.IsNull)
        {
            _entity.AddTag<TagRecycle>();
        }

        _entity = ModClass.I.W.CreateEntity(new SectInventoryBinder(_sect.getID()));
    }

    internal void Dispose()
    {
        if (_entity.IsNull) return;
        _entity.AddTag<TagRecycle>();
        _entity = default;
    }

    public void Add(Entity item)
    {
        InventoryLifecycle.NotifyBeforeItemAdded(_sect, item);
        foreach (Entity owner in item.GetIncomingLinks<InventoryRelation>().Entities)
        {
            owner.RemoveRelation<InventoryRelation>(item);
        }
        _entity.AddRelation(new InventoryRelation { item = item });
        InventoryLifecycle.NotifyAfterItemAdded(_sect, item);
    }

    public void Extract(Entity item)
    {
        InventoryLifecycle.NotifyBeforeItemExtracted(_sect, item);
        _entity.RemoveRelation<InventoryRelation>(item);
        InventoryLifecycle.NotifyAfterItemExtracted(_sect, item);
    }

    public IEnumerable<Entity> GetStoredItems()
    {
        var relations = _entity.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            yield return relations[i].item;
        }
    }

    public IEnumerable<Entity> GetOwnedItems()
    {
        var relations = _entity.GetRelations<SectTreasureRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            yield return relations[i].item;
        }
    }

    public bool Owns(Entity item)
    {
        var relations = _entity.GetRelations<SectTreasureRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == item) return true;
        }

        return false;
    }

    public bool IsStored(Entity item)
    {
        var relations = _entity.GetRelations<InventoryRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            if (relations[i].item == item) return true;
        }

        return false;
    }

    public void AddOwnership(Entity item)
    {
        _entity.AddRelation(new SectTreasureRelation { item = item });
    }

    public void RemoveOwnership(Entity item)
    {
        _entity.RemoveRelation<SectTreasureRelation>(item);
    }

    public static Sect FindOwner(Entity item)
    {
        var owners = item.GetIncomingLinks<SectTreasureRelation>().Entities;
        foreach (Entity owner in owners)
        {
            if (owner.TryGetComponent(out SectInventoryBinder binder)) return binder.Sect;
        }

        return null;
    }
}
