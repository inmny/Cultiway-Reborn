using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 标记一个已炼成的法器实体。第一阶段效果属性留空；
/// 器形可由实体上的 <c>ItemShape</c> 组件反查 ItemShapeLibrary 得到（多态为 ArtifactShapeAsset）。
/// </summary>
public struct Artifact : IComponent
{
}
