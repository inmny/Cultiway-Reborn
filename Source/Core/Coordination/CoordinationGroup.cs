using System;
using System.Collections.Generic;

namespace Cultiway.Core.Coordination;

/// <summary>协调群组的稳定运行期标识；提供者解释所有者与分区的具体含义。</summary>
public readonly struct CoordinationGroupKey : IEquatable<CoordinationGroupKey>
{
    /// <summary>创建一个不依赖领域对象引用的群组标识。</summary>
    public CoordinationGroupKey(string providerId, long ownerId, int partitionId = 0)
    {
        ProviderId = providerId ?? string.Empty;
        OwnerId = ownerId;
        PartitionId = partitionId;
    }

    /// <summary>负责解析该群组的提供者标识。</summary>
    public string ProviderId { get; }

    /// <summary>领域所有者的世界内稳定标识，例如宗门或巢穴 ID。</summary>
    public long OwnerId { get; }

    /// <summary>同一所有者内部的扁平分区，例如鼠群编号。</summary>
    public int PartitionId { get; }

    /// <inheritdoc />
    public bool Equals(CoordinationGroupKey other)
    {
        return OwnerId == other.OwnerId &&
               PartitionId == other.PartitionId &&
               string.Equals(ProviderId, other.ProviderId, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return obj is CoordinationGroupKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(ProviderId, OwnerId, PartitionId);
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return $"{ProviderId}:{OwnerId}:{PartitionId}";
    }
}

/// <summary>领域侧提供的长期群组适配器。</summary>
public interface ICoordinationGroupProvider
{
    /// <summary>提供者的稳定唯一标识。</summary>
    string Id { get; }

    /// <summary>判断群组来源当前是否仍然有效。</summary>
    bool IsValid(in CoordinationGroupKey key);

    /// <summary>判断角色当前是否属于该长期群组。</summary>
    bool Contains(in CoordinationGroupKey key, Actor actor);

    /// <summary>把群组当前成员写入调用方提供的临时列表。</summary>
    void CollectMembers(in CoordinationGroupKey key, IList<Actor> output);

    /// <summary>解析群组当前的领域领导者；没有领导者时返回 null。</summary>
    Actor ResolveLeader(in CoordinationGroupKey key);
}
