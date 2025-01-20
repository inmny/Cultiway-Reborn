using System.Linq;
using Cultiway.Const;
using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using NeoModLoader.api.attributes;

namespace Cultiway.Content.Systems.Logic;

public class CityDistributeItemsSystem : QuerySystem<CityBinder>
{
    public CityDistributeItemsSystem()
    {
    }
    protected override void OnUpdate()
    {
        Query.ForEach(([Hotfixable](cities, entities) =>
        {
            for (int i = 0; i < entities.Length; i++)
            {
                var ce = cities[i].CE;
                if (ce.Base == null || !ce.Base.isAlive()) continue;
                var items = ce.GetItems();
                using var pool = new ListPool<Entity>();
                foreach (var item in items)
                {
                    if (item.IsNull) continue;
                    
                    var data = item.Data;
                    if (data.TryGet(out AliveTimeLimit limit))
                    {
                        var left_years = limit.value - data.Get<AliveTimer>().value;
                        if (Toolbox.randomChance(left_years / limit.value))
                        {
                            continue;
                        }
                    }
                    pool.Add(item);
                }

                var units = ce.Base.units.getSimpleList();
                if (units.Count == 0) continue;
                foreach (var item in pool)
                {
                    var unit = units.GetRandom();
                    if (unit.isAlive())
                        unit.GetExtend().AddSpecialItem(item);
                }
            }
        })).RunParallel();
    }
}