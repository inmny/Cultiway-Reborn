using System;
using System.Collections.Generic;
using System.Linq;

namespace Cultiway.Core.Semantics;

/// <summary>
/// Content 向 Core 注册的角色语义来源。每个贡献器只读取自己拥有的数据。
/// </summary>
public interface IActorSemanticContributor
{
    string Id { get; }
    int Priority { get; }
    void Contribute(ActorExtend actor, SemanticProfileBuilder builder);
}

/// <summary>
/// 角色语义贡献器目录。新增玩法只需注册贡献器，不必修改档案构建器。
/// </summary>
public static class SemanticContributorService
{
    private static readonly Dictionary<string, IActorSemanticContributor> byId =
        new(StringComparer.Ordinal);
    private static IActorSemanticContributor[] ordered = Array.Empty<IActorSemanticContributor>();

    public static void Register(IActorSemanticContributor contributor)
    {
        if (byId.ContainsKey(contributor.Id))
            throw new InvalidOperationException($"角色语义贡献器重复注册: {contributor.Id}");
        byId.Add(contributor.Id, contributor);
        ordered = byId.Values
            .OrderBy(x => x.Priority)
            .ThenBy(x => x.Id, StringComparer.Ordinal)
            .ToArray();
    }

    public static SemanticProfile Build(ActorExtend actor)
    {
        var builder = new SemanticProfileBuilder(ModClass.L.SemanticLibrary);
        for (var i = 0; i < ordered.Length; i++) ordered[i].Contribute(actor, builder);
        return builder.Build();
    }
}
