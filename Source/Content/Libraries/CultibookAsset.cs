using Cultiway.Abstract;
using Cultiway.Core.Components;

namespace Cultiway.Content.Libraries;

public class CultibookAsset : Asset, IDeleteWhenUnknown
{
    /// <summary>
    /// 当掌握程度达到100%时的属性加成
    /// </summary>
    public BaseStats FinalStats;

    public string Name;
    public ItemLevel Level;

    public int Current { get; set; } = 0;
}