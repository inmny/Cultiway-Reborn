using Cultiway.Core.Components;
using Cultiway.Core.SkillLibV3.Components;
using Friflo.Engine.ECS;
using Friflo.Engine.ECS.Systems;

namespace Cultiway.Core.SkillLibV3.Systems;

/// <summary>
/// 在执行会话被删除前处理 Body 所有权：临时 Body 一并回收，借用 Body 只解除租约。
/// </summary>
public sealed class ReleaseSkillExecutionBodiesSystem : QuerySystem<SkillExecution>
{
    public ReleaseSkillExecutionBodiesSystem()
    {
        Filter.AllTags(Tags.Get<TagRecycle>());
    }

    protected override void OnUpdate()
    {
        Query.ForEachEntity((ref SkillExecution _, Entity execution) =>
        {
            var relations = execution.GetRelations<SkillExecutionBodyRelation>();
            for (int i = 0; i < relations.Length; i++)
            {
                SkillExecutionBodyRelation relation = relations[i];
                Entity body = relation.body;
                if (body.IsNull || body == execution) continue;

                if (relation.ownership == SkillExecutionBodyOwnership.Spawned)
                {
                    CommandBuffer.AddTag<TagRecycle>(body.Id);
                    continue;
                }

                if (body.TryGetComponent(out SkillExecutionBodyLease lease) && lease.execution == execution)
                {
                    CommandBuffer.RemoveComponent<SkillExecutionBodyLease>(body.Id);
                }
            }
        });
        CommandBuffer.Playback();
    }
}
