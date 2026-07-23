using System.Collections.Generic;
using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 在轨迹查询解锁后结算落点型范围法术，避免空地点必须先碰到单位才能生效。
/// </summary>
public sealed class LogicSkillPositionImpactSystem : QuerySystem<SkillPositionImpactState>
{
    private readonly List<Entity> pending = new();

    public LogicSkillPositionImpactSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle, TagSkillAnimationNoCollision>());
    }

    protected override void OnUpdate()
    {
        pending.Clear();
        Query.ForEachEntity((ref SkillPositionImpactState state, Entity entity) =>
        {
            if (state.Requested && !state.Resolved) pending.Add(entity);
        });

        for (int i = 0; i < pending.Count; i++)
        {
            Entity entity = pending[i];
            if (!entity.IsNull) SkillHitResolver.ResolvePositionImpact(entity);
        }
    }
}
