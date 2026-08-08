using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>角色已经掌握的骑士流派。</summary>
public struct KnightStyleMastery : IComponent
{
    /// <summary>稳定的 <see cref="Libraries.KnightStyleAsset"/> ID，按掌握顺序排列。</summary>
    public string[] style_ids;
}
