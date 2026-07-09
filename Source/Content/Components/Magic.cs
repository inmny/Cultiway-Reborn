using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

public struct Magic : ICultisysComponent
{
    public int   level;
    public float spirit;
    [Ignore]
    public BaseCultisysAsset Asset => Cultisyses.Magic;
    [Ignore]
    public int CurrLevel
    {
        get => level;
        set => level = value;
    }
}
