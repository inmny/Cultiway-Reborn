using Friflo.Engine.ECS;
using Friflo.Json.Fliox;

namespace Cultiway.Core.Components;

public struct CityBinder(long id) : IComponent
{
    public readonly long id = id;

    [Ignore]
    public City City
    {
        get
        {
            if (_city == null) _city = World.world.cities.get(id);

            return _city;
        }
    }

    [Ignore]
    public   CityExtend CE => _ce;
    [Ignore]
    internal CityExtend _ce;
    private  City       _city;
}