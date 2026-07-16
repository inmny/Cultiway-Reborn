using Cultiway.Content.Artifacts;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Content.Systems.Logic;

/// <summary>在技能轨迹查询结束后结算剑阵命中，避免伤害事件在 ECS 查询锁内修改结构。</summary>
public sealed class ArtifactSwordArrayHitSystem : BaseSystem
{
    protected override void OnUpdateGroup()
    {
        ArtifactSwordArrayExecution.ResolvePendingHits();
    }
}
