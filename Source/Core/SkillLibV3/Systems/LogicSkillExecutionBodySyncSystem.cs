using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 将执行会话计算出的位姿同步到它所驱动的实际世界 Body。
/// </summary>
public sealed class LogicSkillExecutionBodySyncSystem : QuerySystem<SkillExecution, Position, Rotation>
{
    public LogicSkillExecutionBodySyncSystem()
    {
        Filter.WithoutAnyTags(Tags.Get<TagPrefab, TagInactive, TagRecycle>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref SkillExecution _, ref Position position, ref Rotation rotation, Entity execution) =>
        {
            if (execution.HasComponent<SkillExecutionWithoutBody>()) return;

            var relations = execution.GetRelations<SkillExecutionBodyRelation>();
            if (relations.Length == 0)
            {
                SkillExecutionLifecycle.RequestEnd(execution);
                return;
            }

            for (int i = 0; i < relations.Length; i++)
            {
                SkillExecutionBodyRelation relation = relations[i];
                Entity body = relation.body;
                if (body.IsNull)
                {
                    SkillExecutionLifecycle.RequestEnd(execution);
                    continue;
                }

                if (relation.ownership == SkillExecutionBodyOwnership.Borrowed &&
                    (!body.TryGetComponent(out SkillExecutionBodyLease lease) || lease.execution != execution))
                {
                    SkillExecutionLifecycle.RequestEnd(execution);
                    continue;
                }

                body.GetComponent<Position>().value = position.value;
                body.GetComponent<Rotation>().value = rotation.value;
            }
        });
    }
}
