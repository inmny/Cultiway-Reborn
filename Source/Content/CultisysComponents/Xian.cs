using Cultiway.Abstract;
using Cultiway.Core.Libraries;

namespace Cultiway.Content.CultisysComponents;

public struct Xian : ICultisysComponent
{
    public int               level;
    public float             wakan;
    public BaseCultisysAsset Asset => Cultisyses.Xian;

    public int CurrLevel
    {
        get => level;
        set => level = value;
    }
}