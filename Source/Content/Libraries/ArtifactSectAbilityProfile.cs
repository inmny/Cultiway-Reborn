using System;
using Cultiway.Content.Components;
using Cultiway.Core;
using Friflo.Engine.ECS;

namespace Cultiway.Content.Libraries;

/// <summary>宗门供奉能力计算成员增益时的只读上下文。</summary>
public readonly struct ArtifactSectAbilityContext
{
    public readonly Sect sect;
    public readonly Entity artifact;
    /// <summary>进入宗门法器聚合前的成员属性快照，供比例增益按同一基准计算。</summary>
    public readonly BaseStats baseline_stats;

    public ArtifactSectAbilityContext(Sect sect, Entity artifact, BaseStats baselineStats)
    {
        this.sect = sect;
        this.artifact = artifact;
        baseline_stats = baselineStats;
    }
}

/// <summary>能力在法器被宗门供奉时提供的被动接口。</summary>
public sealed class ArtifactSectAbilityProfile
{
    /// <summary>同组能力按强度竞争供奉名额；为空时不参与分组限制。</summary>
    public string stacking_group;

    /// <summary>同一宗门内该组最多生效的能力实例数。</summary>
    public int max_active = 1;

    public Func<ArtifactAbilityInstance, float> ResolvePriority;
    public Action<ArtifactSectAbilityContext, ArtifactAbilityInstance, BaseStats> ContributeMemberStats;
}
