using Cultiway.Abstract;
using Cultiway.Core.Components;
using Friflo.Engine.ECS;

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
}