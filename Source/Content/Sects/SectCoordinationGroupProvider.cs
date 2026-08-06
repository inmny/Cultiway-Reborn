using System.Collections.Generic;
using Cultiway.Core;
using Cultiway.Core.Coordination;
using Cultiway.Utils.Extension;

namespace Cultiway.Content.Sects;

/// <summary>把宗门长期成员关系适配为协调行动群组。</summary>
public sealed class SectCoordinationGroupProvider : ICoordinationGroupProvider
{
    /// <summary>宗门协调群组提供者标识。</summary>
    public const string ProviderId = "cultiway.sect";

    /// <inheritdoc />
    public string Id => ProviderId;

    /// <inheritdoc />
    public bool IsValid(in CoordinationGroupKey key)
    {
        Sect sect = Resolve(key);
        return !sect.isRekt();
    }

    /// <inheritdoc />
    public bool Contains(in CoordinationGroupKey key, Actor actor)
    {
        Sect sect = Resolve(key);
        return !sect.isRekt() && !actor.isRekt() && actor.GetExtend().sect == sect;
    }

    /// <inheritdoc />
    public void CollectMembers(in CoordinationGroupKey key, IList<Actor> output)
    {
        Sect sect = Resolve(key);
        if (sect.isRekt()) return;
        List<Actor> members = sect.GetLivingMembers();
        for (var i = 0; i < members.Count; i++) output.Add(members[i]);
    }

    /// <inheritdoc />
    public Actor ResolveLeader(in CoordinationGroupKey key)
    {
        Sect sect = Resolve(key);
        return sect.isRekt() ? null : sect.GetLeaderActor();
    }

    /// <summary>按群组键解析宗门。</summary>
    private static Sect Resolve(in CoordinationGroupKey key)
    {
        return key.PartitionId == 0 ? WorldboxGame.I?.Sects?.get(key.OwnerId) : null;
    }
}
