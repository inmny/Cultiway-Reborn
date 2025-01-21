using Cultiway.Core;

namespace Cultiway.Utils.Extension;

public static class CityTools
{
    private static readonly CityExtendManager CityExtendManager = ModClass.I.CityExtendManager;
    public static CityExtend GetExtend(this City city)
    {
        return CityExtendManager.Get(city);
    }
}