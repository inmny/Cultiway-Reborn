namespace Cultiway.Core.Libraries;

/// <summary>
/// 法器器形的用途倾向，用于辅助 AI 决策、命名与后续效果分配。第一阶段仅占位。
/// </summary>
public enum ArtifactPurpose
{
    /// <summary>攻伐</summary>
    Offensive,
    /// <summary>护身</summary>
    Defensive,
    /// <summary>辅助</summary>
    Support,
    /// <summary>修炼</summary>
    Cultivate,
    /// <summary>生产</summary>
    Production,
}
