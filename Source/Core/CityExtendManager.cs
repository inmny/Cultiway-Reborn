using System.Collections.Generic;
using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core;

public class CityExtendManager : ExtendComponentManager<CityExtend>
{
    public readonly  EntityStore                    World;
    private readonly Dictionary<string, CityExtend> _data = new();

    internal CityExtendManager(EntityStore world)
    {
        World = world;
    }

    public CityExtend Get(string id, bool new_when_null = false)
    {
        if (!_data.TryGetValue(id, out CityExtend val) && new_when_null)
        {
            val = new CityExtend(World.CreateEntity(new CityBinder(id)));
            _data[id] = val;
        }

        return val;
    }
}