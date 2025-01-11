using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

public struct Xian : ICultisysComponent
{
    public int               level;
    public float             wakan;
    [Ignore]
    public BaseCultisysAsset Asset => Cultisyses.Xian;
    [Ignore]
    public int CurrLevel
    {
        get => level;
        set => level = value;
    }
}