using System.Collections.Concurrent;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class CityExtendManager : ExtendComponentManager<CityExtend>
{
    public readonly EntityStore World;
    private readonly ConcurrentDictionary<CityData, CityExtend> _city_to_extend = new();

    internal CityExtendManager(EntityStore world)
    {
        World = world;
    }

    public CityExtend Get(City city)
    {
        var cityData = city.data;
        var cityId = cityData.id;

        // 所有对EntityStore的访问都需要在锁的保护下进行
        lock (EntityStoreLock.GlobalLock)
        {
            if (_city_to_extend.TryGetValue(cityData, out var val))
            {
                ref var binder = ref val.E.GetComponent<CityBinder>();
                if (binder.ID == cityId && val.Base != null)
                {
                    return val;
                }

                // ID不匹配或Base为null，回收旧的CityExtend
                if (!val.E.IsNull)
                {
                    val.E.AddTag<TagRecycle>();
                }
                _city_to_extend.TryRemove(cityData, out _);
            }

            // 创建新的CityExtend
            var newExtend = new CityExtend(World.CreateEntity(new CityBinder(cityId)));
            _city_to_extend[cityData] = newExtend;
            return newExtend;
        }
    }
}