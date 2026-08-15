using System;

namespace Cultiway.Core.Pathfinding;

/// <summary>寻路空间类型。</summary>
public enum PathWorldKind : byte
{
    MainWorld,
    SubWorld
}

/// <summary>唯一标识一次世界生命周期内的寻路空间。</summary>
public readonly struct PathWorldKey : IEquatable<PathWorldKey>
{
    public PathWorldKey(PathWorldKind kind, long instanceId, int generation)
    {
        Kind = kind;
        InstanceId = instanceId;
        Generation = generation;
    }

    public PathWorldKind Kind { get; }
    public long InstanceId { get; }
    public int Generation { get; }

    public static PathWorldKey MainWorld(int generation)
    {
        return new PathWorldKey(PathWorldKind.MainWorld, 0, generation);
    }

    public static PathWorldKey SubWorld(long instanceId, int generation)
    {
        return new PathWorldKey(PathWorldKind.SubWorld, instanceId, generation);
    }

    public bool Equals(PathWorldKey other)
    {
        return Kind == other.Kind && InstanceId == other.InstanceId && Generation == other.Generation;
    }

    public override bool Equals(object obj)
    {
        return obj is PathWorldKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            int hash = (int)Kind;
            hash = hash * 397 ^ InstanceId.GetHashCode();
            return hash * 397 ^ Generation;
        }
    }

    public static bool operator ==(PathWorldKey left, PathWorldKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PathWorldKey left, PathWorldKey right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{Kind}:{InstanceId}:{Generation}";
    }
}

/// <summary>在指定寻路空间内唯一标识一个寻路实体。</summary>
public readonly struct PathAgentKey : IEquatable<PathAgentKey>
{
    public PathAgentKey(PathWorldKey world, long agentId)
    {
        World = world;
        AgentId = agentId;
    }

    public PathWorldKey World { get; }
    public long AgentId { get; }
    public bool IsValid => AgentId > 0;

    public bool Equals(PathAgentKey other)
    {
        return World.Equals(other.World) && AgentId == other.AgentId;
    }

    public override bool Equals(object obj)
    {
        return obj is PathAgentKey other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return World.GetHashCode() * 397 ^ AgentId.GetHashCode();
        }
    }

    public static bool operator ==(PathAgentKey left, PathAgentKey right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PathAgentKey left, PathAgentKey right)
    {
        return !left.Equals(right);
    }

    public override string ToString()
    {
        return $"{World}/agent:{AgentId}";
    }
}

/// <summary>绑定一次已接受提交，防止旧调用方消费后来替换的路径。</summary>
public readonly struct PathHandle : IEquatable<PathHandle>
{
    public PathHandle(PathAgentKey agent, long submissionToken)
    {
        Agent = agent;
        SubmissionToken = submissionToken;
    }

    public PathAgentKey Agent { get; }
    public long SubmissionToken { get; }
    public bool IsValid => Agent.IsValid && SubmissionToken > 0;

    public bool Equals(PathHandle other)
    {
        return Agent.Equals(other.Agent) && SubmissionToken == other.SubmissionToken;
    }

    public override bool Equals(object obj)
    {
        return obj is PathHandle other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return Agent.GetHashCode() * 397 ^ SubmissionToken.GetHashCode();
        }
    }

    public static bool operator ==(PathHandle left, PathHandle right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(PathHandle left, PathHandle right)
    {
        return !left.Equals(right);
    }
}

internal enum PathRetryMode : byte
{
    TimedMainWorld,
    CallerManaged
}

/// <summary>一次搜索使用的空间规则；worker 只读取这些不可变标量。</summary>
internal readonly struct PathSearchRules
{
    internal PathSearchRules(
        bool allowPortals,
        bool hardBlockTerrain,
        bool preventCornerCutting,
        int maxExpandedNodes,
        PathRetryMode retryMode)
    {
        AllowPortals = allowPortals;
        HardBlockTerrain = hardBlockTerrain;
        PreventCornerCutting = preventCornerCutting;
        MaxExpandedNodes = maxExpandedNodes;
        RetryMode = retryMode;
    }

    internal bool AllowPortals { get; }
    internal bool HardBlockTerrain { get; }
    internal bool PreventCornerCutting { get; }
    internal int MaxExpandedNodes { get; }
    internal PathRetryMode RetryMode { get; }

    internal static PathSearchRules MainWorld => new(
        true,
        false,
        false,
        0,
        PathRetryMode.TimedMainWorld);

    internal static PathSearchRules ForSubWorld(int tileCount)
    {
        return new PathSearchRules(false, true, true, Math.Max(1, tileCount), PathRetryMode.CallerManaged);
    }
}
