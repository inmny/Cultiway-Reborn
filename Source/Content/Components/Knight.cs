using Cultiway.Abstract;
using Cultiway.Core.Libraries;
using Friflo.Json.Fliox;

namespace Cultiway.Content.Components;

/// <summary>骑士体系组件。level 为骑士等级(0-9)，vigor 为突破资源「斗气」。</summary>
public struct Knight : ICultisysComponent
{
    public int   level;
    public float vigor;

    [Ignore]
    public BaseCultisysAsset Asset => Cultisyses.Knight;

    [Ignore]
    public int CurrLevel
    {
        get => level;
        set => level = value;
    }
}
