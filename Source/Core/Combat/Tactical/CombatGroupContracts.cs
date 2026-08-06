using System;
using System.Collections.Generic;

namespace Cultiway.Core.Combat.Tactical;

/// <summary>战术共享状态使用的来源无关群组键。</summary>
public readonly struct CombatGroupKey : IEquatable<CombatGroupKey>
{
    /// <summary>创建一个群组键。</summary>
    public CombatGroupKey(string providerId, long ownerId, int partitionId = 0)
    {
        ProviderId = providerId ?? string.Empty;
        OwnerId = ownerId;
        PartitionId = partitionId;
    }

    /// <summary>群组提供者标识。</summary>
    public string ProviderId { get; }

    /// <summary>领域所有者稳定 ID。</summary>
    public long OwnerId { get; }

    /// <summary>所有者内部的扁平分区。</summary>
    public int PartitionId { get; }

    /// <inheritdoc />
    public bool Equals(CombatGroupKey other)
    {
        return OwnerId == other.OwnerId &&
               PartitionId == other.PartitionId &&
               string.Equals(ProviderId, other.ProviderId, StringComparison.Ordinal);
    }

    /// <inheritdoc />
    public override bool Equals(object obj)
    {
        return obj is CombatGroupKey other && Equals(other);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(ProviderId, OwnerId, PartitionId);
    }
}

/// <summary>把任意长期多人组织适配为战术共享情报群组。</summary>
public interface ICombatGroupProvider
{
    /// <summary>提供者稳定 ID。</summary>
    string Id { get; }

    /// <summary>判断角色当前是否属于一个可共享战术状态的群组。</summary>
    bool TryResolveGroup(Actor actor, out CombatGroupKey key);

    /// <summary>把群组当前有效成员写入调用方列表。</summary>
    void CollectMembers(in CombatGroupKey key, IList<Actor> output);

    /// <summary>解析群组的集结领导者。</summary>
    Actor ResolveLeader(in CombatGroupKey key);

    /// <summary>返回群组在没有外部临时指令时采用的战术指令。</summary>
    CombatDirective ResolveDefaultDirective(in CombatGroupKey key, Actor actor);

    /// <summary>该群组是否使用伤亡、士气与溃退共识；鼠群等组织可关闭。</summary>
    bool UsesRoutMechanics { get; }
}
