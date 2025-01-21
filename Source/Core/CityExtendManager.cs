using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class CityExtendManager : ExtendComponentManager<CityExtend>
{
    public readonly  EntityStore                    World;
    private readonly ConditionalWeakTable<City, CityExtend> _city_to_extend = new();

    internal CityExtendManager(EntityStore world)
    {
        World = world;
    }
    public CityExtend Get(City city)
    {
        if (_city_to_extend.TryGetValue(city, out var val)) return val;
        val = new CityExtend(World.CreateEntity(new CityBinder(city.data.id)));
        _city_to_extend.Add(city, val);
        return val;
    }
}