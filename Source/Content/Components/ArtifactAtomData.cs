using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>
/// 法器炼成时选中的语义原子。当前只用于命名和图标倾向。
/// </summary>
public struct ArtifactAtomData : IComponent
{
    public string[] atom_ids;
}
