using Cultiway.Core.Components;
using Friflo.Engine.ECS;

namespace Cultiway.Core.SkillLibV3.Components;

/// <summary>
/// 标记一次已经开始、可跨帧持续运行的技能执行会话。
/// </summary>
public struct SkillExecution : IComponent
{
    /// <summary>
    /// 会话已经请求结束。该状态立即阻止后续碰撞，实体回收仍统一交给帧末命令缓冲。
    /// </summary>
    public bool end_requested;
}

public enum SkillExecutionBodyOwnership
{
    /// <summary>Body 由本次执行创建，结束时一并回收。</summary>
    Spawned,

    /// <summary>Body 是已有持久实体，结束时只解除控制。</summary>
    Borrowed,
}

/// <summary>
/// 执行会话指向实际世界本体的关系。普通法术可拥有临时 Body，法器能力则借用已有法器实体。
/// </summary>
public struct SkillExecutionBodyRelation : ILinkRelation
{
    public Entity body;
    public SkillExecutionBodyOwnership ownership;
    public string role;

    public Entity GetRelationKey() => body;
}

/// <summary>
/// 标记一个持久世界实体当前正由某次技能执行驱动，防止其他表现系统覆盖其位姿。
/// </summary>
public struct SkillExecutionBodyLease : IComponent
{
    public Entity execution;
}

/// <summary>
/// SkillExecution 的 Body 绑定和结束入口。
/// </summary>
public static class SkillExecutionLifecycle
{
    public static bool TryBorrowBody(Entity execution, Entity body, string role = "primary")
    {
        if (execution.IsNull || body.IsNull || body.HasComponent<SkillExecutionBodyLease>()) return false;

        execution.AddRelation(new SkillExecutionBodyRelation
        {
            body = body,
            ownership = SkillExecutionBodyOwnership.Borrowed,
            role = role,
        });
        body.AddComponent(new SkillExecutionBodyLease { execution = execution });
        return true;
    }

    public static void BindSpawnedBody(Entity execution, Entity body, string role = "primary")
    {
        execution.AddRelation(new SkillExecutionBodyRelation
        {
            body = body,
            ownership = SkillExecutionBodyOwnership.Spawned,
            role = role,
        });
    }

    public static bool TryGetPrimaryBody(Entity execution, out Entity body)
    {
        var relations = execution.GetRelations<SkillExecutionBodyRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            SkillExecutionBodyRelation relation = relations[i];
            if (relation.role != "primary") continue;
            body = relation.body;
            return !body.IsNull;
        }

        body = default;
        return false;
    }

    public static void RequestEnd(Entity execution)
    {
        if (execution.IsNull) return;

        ref SkillExecution state = ref execution.GetComponent<SkillExecution>();
        if (state.end_requested) return;

        state.end_requested = true;
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(execution.Id);
    }
}
