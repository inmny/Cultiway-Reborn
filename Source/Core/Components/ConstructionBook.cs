using Friflo.Engine.ECS;

namespace Cultiway.Core.Components;

/// <summary>书中记录的确定构造版本；实际定义由世界能力目录维护。</summary>
public struct ConstructionBook : IComponent
{
    /// <summary>不会因作者后来修改而改变的能力编号。</summary>
    public string AbilityId;
}