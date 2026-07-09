using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 标记一个正在炼制中的法器实体。
/// </summary>
public struct CraftingArtifact : IComponent
{
    public int progress;
}
