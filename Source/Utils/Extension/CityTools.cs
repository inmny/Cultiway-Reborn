using Cultiway.Core;

namespace Cultiway.Utils.Extension;

public static class CityTools
{
    public static CityExtend GetExtend(this City city)
    {
        return ModClass.I.CityExtendManager.Get(city.data.id, true);
    }
}