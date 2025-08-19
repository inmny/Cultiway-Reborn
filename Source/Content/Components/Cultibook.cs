using Cultiway.Content.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public struct Cultibook : IComponent
{
    /// <summary>
    /// 当掌握程度达到100%时的属性加成
    /// </summary>
    public BaseStats FinalStats;
}