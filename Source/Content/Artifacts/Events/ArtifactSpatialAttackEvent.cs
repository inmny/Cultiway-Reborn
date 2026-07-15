namespace Cultiway.Content.Artifacts.Events;

/// <summary>
/// 战斗系统向已装备法器提出的一次空间攻击请求。具体能力决定是否响应以及如何操纵法器实体。
/// </summary>
public sealed class ArtifactSpatialAttackEvent
{
    public BaseSimObject Target { get; }

    public ArtifactSpatialAttackEvent(BaseSimObject target)
    {
        Target = target;
    }
}
