using Cultiway.Core.Libraries;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 标记一个正在炼制中的法器实体。shape 为由材料倾向推断出的器形。
/// </summary>
public struct CraftingArtifact : IComponent
{
    public ArtifactShapeAsset shape;
    public int progress;
}
