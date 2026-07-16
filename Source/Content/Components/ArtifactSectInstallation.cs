using Friflo.Engine.ECS;

namespace Cultiway.Content.Components;

/// <summary>宗门藏宝阁运行实体指向正在供奉的法器。</summary>
public struct ArtifactSectInstallationRelation : ILinkRelation
{
    public Entity artifact;

    public Entity GetRelationKey()
    {
        return artifact;
    }
}
