using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

public struct CityBinder(string id) : IComponent
{
    public readonly string id = id;

    public City City
    {
        get
        {
            if (_city == null) _city = World.world.cities.get(id);

            return _city;
        }
    }

    public   CityExtend CE => _ce;
    internal CityExtend _ce;
    private  City       _city;
}