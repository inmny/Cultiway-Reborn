using Cultiway.Abstract;
using Cultiway.Content.Artifacts;
using Cultiway.Core;

namespace Cultiway.Content;

/// <summary>将宗门成员属性缓存接到 Content 定义的法器供奉能力。</summary>
public sealed class ArtifactSectIntegration : ICanInit
{
    public void Init()
    {
        ActorExtend.RegisterCachedStatsBuilder(ArtifactSectService.ContributeMemberStats);
    }
}
