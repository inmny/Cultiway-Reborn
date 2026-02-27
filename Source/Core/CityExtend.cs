using System;
using System.Collections.Generic;
using System.Linq;
using Cultiway.Abstract;
using Cultiway.Content;
using Cultiway.Content.Components;
using Cultiway.Content.Extensions;
using Cultiway.Core.Components;
using Cultiway.Utils;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using HarmonyLib;

namespace Cultiway.Core;

public class CityExtend : ExtendComponent<City>, IHasInventory, IAsForce, IDisposable
{
    private readonly Entity e;

    public CityExtend(Entity e)
    {
        this.e = e;
        e.GetComponent<CityBinder>()._ce = this;
    }

    public override Entity E    => e;
    public override City   Base => e.HasComponent<CityBinder>() ? e.GetComponent<CityBinder>().City : null;

    public void AddSpecialItem(Entity item_entity)
    {
        item_entity.GetIncomingLinks<InventoryRelation>().Entities
            .Do(owner => owner.RemoveRelation<InventoryRelation>(item_entity));
        e.AddRelation(new InventoryRelation { item = item_entity });
    }

    public void ExtractSpecialItem(Entity item_entity)
    {
        e.RemoveRelation<InventoryRelation>(item_entity);
    }

    public IEnumerable<Entity> GetItems()
    {
        var rels = e.GetRelations<InventoryRelation>();
        var rel_count = rels.Length;
        for (int i = 0; i < rel_count; i++)
        {
            yield return rels[i].item;  
        }
        yield break;
    }

    public bool HasItem<TComponent>() where TComponent : struct, IComponent
    {
        var rels = e.GetRelations<InventoryRelation>();
        for (int i = 0; i < rels.Length; i++)
        {
            if (rels[i].item.HasComponent<TComponent>())
            {
                return true;
            }
        }
        return false;
    }

    public override string ToString()
    {
        return $"[{e.GetComponent<CityBinder>().id}] {Base.name}: {e}";
    }

    public void TestAddSpecialItem()
    {
        AddSpecialItem(SpecialItemUtils.StartBuild(ItemShapes.Ball.id, World.world.getCurWorldTime()).Build());
    }

    public void TestAddOpenElementRootElixir()
    {
        AddSpecialItem(
            SpecialItemUtils.StartBuild(ItemShapes.Ball.id, World.world.getCurWorldTime())
                .AddComponent(ElementRoot.Roll())
                .AddComponent(new Elixir
                {
                    elixir_id = Elixirs.OpenElementRootElixir.id
                })
                .Build()
        );
    }

    public void TestGiveOpenElementRootElixir()
    {
        var open_element_root_elixirs = GetSpecialItems<Elixir>()
            .Where(item => item.self.GetComponent<Elixir>().elixir_id == Elixirs.OpenElementRootElixir.id).ToList();
        if (open_element_root_elixirs.Count == 0) return;
        ActorExtend lucky_dog = null;
        foreach (Actor actor in Base.units)
        {
            ActorExtend ae = actor.GetExtend();
            if (ae.HasElementRoot()) continue;
            lucky_dog = ae;
        }

        if (lucky_dog == null) return;
        SpecialItem elixir = open_element_root_elixirs.GetRandom();

        lucky_dog.TryConsumeElixir(elixir.self);
        ModClass.LogInfo(lucky_dog.Base.getName());
    }

    public List<SpecialItem> GetSpecialItems()
    {
        var rels = e.GetRelations<InventoryRelation>();
        var result = new List<SpecialItem>(rels.Length);
        for (int i = 0; i < rels.Length; i++)
        {
            result.Add(rels[i].item.GetComponent<SpecialItem>());
        }
        return result;
    }
    public SpecialItem GetRandomSpecialItem(Func<Entity, bool> filter)
    {
        using var pool = new ListPool<SpecialItem>(GetItems().Select(x => x.GetComponent<SpecialItem>()).Where(x => filter(x.self)));
        if (pool.Any())
            return pool.GetRandom();
        return default;
    }

    public List<SpecialItem> GetSpecialItems<TComponent>() where TComponent : struct, IComponent
    {
        return GetItems().Where(x => x.HasComponent<TComponent>()).Select(x => x.GetComponent<SpecialItem>()).ToList();
    }

    public List<SpecialItem> GetSpecialItems<TComponent1, TComponent2>()
        where TComponent1 : struct, IComponent
        where TComponent2 : struct, IComponent
    {
        return GetItems().Where(x => x.HasComponent<TComponent1>() && x.HasComponent<TComponent2>())
            .Select(x => x.GetComponent<SpecialItem>()).ToList();
    }

    internal void PrepareDestroy()
    {
    }

    public bool HasRelatedForce<TRelation>() where TRelation : struct, IForceRelation
    {
        return E.GetRelations<TRelation>().Length > 0;
    }

    public IEnumerable<Entity> GetForces<TRelation>() where TRelation : struct, IForceRelation
    {
        var rels = E.GetRelations<TRelation>();
        var rel_count = rels.Length;
        for (int i = 0; i < rel_count; i++)
        {
            yield return rels[i].GetRelationKey();  
        }
        yield break;
    }

    public void JoinForce<TRelation>(Entity force)  where TRelation : struct, IForceRelation
    {
        E.AddRelation(new TRelation { ForceEntity = force });
    }

    public Entity GetSelf()
    {
        return E;
    }

    public void Dispose()
    {
        if (!e.IsNull)
        {
            e.AddTag<TagRecycle>();

            foreach (var item in GetItems())
            {
                item.AddTag<TagRecycle>();
            }
        }
    }
}