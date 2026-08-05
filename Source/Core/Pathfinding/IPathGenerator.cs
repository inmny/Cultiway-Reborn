using System;
using System.Collections.Generic;
using System.Threading;

namespace Cultiway.Core.Pathfinding;

public interface IPathGenerator
{
    PathGenerationResult GenerateSegment(PathRequest request, CancellationToken cancellationToken);
}

/// <summary>
/// 一次局部分段搜索的不可变结果。成功结果只包含下一段可消费路径，不代表已经抵达最终目标。
/// </summary>
public readonly struct PathGenerationResult
{
    private PathGenerationResult(IReadOnlyList<PathStep> steps, bool reachedTarget, int endTileId,
        PathFailureReason failureReason, Exception error, int expandedNodes, PathGenerationKind kind,
        float endStamina, float endHealth)
    {
        Steps = steps ?? Array.Empty<PathStep>();
        ReachedTarget = reachedTarget;
        EndTileId = endTileId;
        FailureReason = failureReason;
        Error = error;
        ExpandedNodes = expandedNodes;
        Kind = kind;
        EndStamina = endStamina;
        EndHealth = endHealth;
    }

    public IReadOnlyList<PathStep> Steps { get; }
    public bool ReachedTarget { get; }
    public int EndTileId { get; }
    public PathFailureReason FailureReason { get; }
    public Exception Error { get; }
    public int ExpandedNodes { get; }
    public PathGenerationKind Kind { get; }
    /// <summary>执行完该分段后的预估剩余体力；NaN 表示生成器未提供。</summary>
    public float EndStamina { get; }
    /// <summary>执行完该分段后的预估剩余生命；NaN 表示生成器未提供。</summary>
    public float EndHealth { get; }
    public bool IsSuccess => FailureReason == PathFailureReason.None;

    public static PathGenerationResult Success(IReadOnlyList<PathStep> steps, bool reachedTarget, int endTileId,
        int expandedNodes = 0, PathGenerationKind kind = PathGenerationKind.Search,
        float endStamina = float.NaN, float endHealth = float.NaN)
    {
        return new PathGenerationResult(steps, reachedTarget, endTileId, PathFailureReason.None, null,
            expandedNodes, kind, endStamina, endHealth);
    }

    public static PathGenerationResult Fail(PathFailureReason reason, Exception error = null,
        int expandedNodes = 0)
    {
        return new PathGenerationResult(Array.Empty<PathStep>(), false, -1,
            reason == PathFailureReason.None ? PathFailureReason.GeneratorException : reason,
            error, expandedNodes, PathGenerationKind.Search, float.NaN, float.NaN);
    }
}

public enum PathGenerationKind
{
    Search,
    StraightLine,
    RegionCorridor,
    Portal
}
