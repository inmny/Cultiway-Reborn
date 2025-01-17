using Cultiway.Core.Components;
using Cultiway.Utils.Extension;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;
using HarmonyLib;
using NeoModLoader.api.attributes;

namespace Cultiway.Core.Systems.Logic;

public class SyncCityRelationSystem : QuerySystem<ActorBinder>
{
    protected override void OnUpdate()
    {
        Query.ForEach(([Hotfixable](binders, entities) =>
        {
            for(int i=0;i<entities.Length;i++)
            {
                var a = binders[i].Actor;
                if (a == null || !a.isAlive()) continue;
                var e = entities.EntityAt(i);

                var cities = e.GetRelations<ForceCityBelongRelation>();
                bool need_update = false;
                bool has_city_relation = false;
                foreach (var city in cities)
                {
                    has_city_relation = true;
                    if (city.ForceEntity.GetComponent<CityBinder>().City != a.city)
                    {
                        need_update = true;
                        break;
                    }
                }
                if (!has_city_relation && a.city != null)
                {
                    need_update = true;
                }
                if (need_update)
                {
                    cities.Do(city => e.RemoveRelation<ForceCityBelongRelation>(city.ForceEntity));
                    if (a.city != null)
                        e.AddRelation(new ForceCityBelongRelation { ForceEntity = a.city.GetExtend().E });
                }
            }
        })).RunParallel();
    }
}