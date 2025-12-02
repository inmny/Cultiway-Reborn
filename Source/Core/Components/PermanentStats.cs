using Cultiway;
using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>
/// 记录长期叠加的属性（例如丹药DataGain）
/// </summary>
public struct PermanentStats : IComponent
{
    public BaseStats Stats;
}
