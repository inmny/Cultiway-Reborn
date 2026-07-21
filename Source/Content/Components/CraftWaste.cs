using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

public enum CraftProcessType
{
    Alchemy,
    ArtifactRefining,
}

public enum CraftFailureReason
{
    Interrupted,
    IngredientsMissing,
}

/// <summary>
/// 记录已经失败且不可恢复的炼制品。
/// </summary>
public struct CraftWaste : IComponent
{
    public CraftProcessType process;
    public CraftFailureReason reason;
}
