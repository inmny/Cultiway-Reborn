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

/// <summary>
/// 标记执行会话没有需要统一同步位姿的单一世界 Body。
/// 会话仍可通过 Body 关系持有独立运动的派生实体，并在结束时统一回收它们。
/// </summary>
public struct SkillExecutionWithoutBody : IComponent
{
}

/// <summary>实际本体归属于本次生成，还是原世界借用。</summary>
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
    /// <summary>本次实际驱动的世界实体。</summary>
    public Entity body;

    /// <summary>只有生成本体才允许随施放删除。</summary>
    public SkillExecutionBodyOwnership ownership;

    /// <summary>本体在这次施放中的用途。</summary>
    public string role;

    /// <summary>同一施放对同一本体只登记一次关系。</summary>
    public Entity GetRelationKey() => body;
}

/// <summary>
/// 标记一个持久世界实体当前正由某次技能执行驱动，防止其他表现系统覆盖其位姿。
/// </summary>
public struct SkillExecutionBodyLease : IComponent
{
    /// <summary>当前唯一控制者，包含实体身份而非仅可复用的整数编号。</summary>
    public Entity execution;
}

/// <summary>
/// SkillExecution 的 Body 绑定和结束入口。
/// </summary>
public static class SkillExecutionLifecycle
{
    /// <summary>借用已有本体；控制冲突、结束或待回收主体不能被重新取得。</summary>
    /// <param name="execution">实际施放。</param><param name="body">原有本体。</param><param name="role">本次关系中的用途。</param>
    public static bool TryBorrowBody(Entity execution, Entity body, string role = "primary") =>
        TryBind(execution, body, SkillExecutionBodyOwnership.Borrowed, role);

    /// <summary>登记本次新建的临时本体，结束时由同一施放负责回收。</summary>
    /// <param name="execution">实际施放。</param><param name="body">新建本体。</param><param name="role">本次用途。</param>
    public static bool TryBindSpawnedBody(Entity execution, Entity body, string role = "primary") =>
        TryBind(execution, body, SkillExecutionBodyOwnership.Spawned, role);

    /// <summary>两种所有权共用实际控制凭据，不能把已借用原物重新登记为生成物。</summary>
    /// <param name="execution">实际施放。</param><param name="body">实际本体。</param>
    /// <param name="ownership">本次所有权。</param><param name="role">关系用途。</param>
    private static bool TryBind(Entity execution, Entity body, SkillExecutionBodyOwnership ownership, string role)
    {
        if (!IsLive(execution) || body.IsNull || body == execution ||
            IsTemplate(body) || body.Tags.Has<TagRecycle>() ||
            body.HasComponent<SkillExecutionBodyLease>() || string.IsNullOrEmpty(role)) return false;
        execution.AddRelation(new SkillExecutionBodyRelation { body = body, ownership = ownership, role = role });
        body.AddComponent(new SkillExecutionBodyLease { execution = execution });
        return true;
    }

    /// <summary>主线程在实体遍历之外解除关系：只回收本次仍拥有的生成物，借用原物始终保留。</summary>
    /// <param name="execution">结束或初始化失败的施放。</param>
    public static void ReleaseBodies(Entity execution)
    {
        if (execution.IsNull) return;
        var relations = execution.GetRelations<SkillExecutionBodyRelation>();
        while (relations.Length > 0)
        {
            SkillExecutionBodyRelation relation = relations[relations.Length - 1];
            Entity body = relation.body;
            execution.RemoveRelation<SkillExecutionBodyRelation>(body);
            if (!body.IsNull && body.TryGetComponent(out SkillExecutionBodyLease lease) && lease.execution == execution)
            {
                body.RemoveComponent<SkillExecutionBodyLease>();
                if (relation.ownership == SkillExecutionBodyOwnership.Spawned && !body.Tags.Has<TagRecycle>())
                    body.AddTag<TagRecycle>();
            }

            relations = execution.GetRelations<SkillExecutionBodyRelation>();
        }
    }

    /// <summary>已经正式结束或待回收的施放不能再取得本体。</summary>
    /// <param name="execution">实际施放。</param>
    private static bool IsLive(Entity execution) => !execution.IsNull &&
                                                    execution.TryGetComponent(out SkillExecution state) &&
                                                    !state.end_requested && !execution.Tags.Has<TagRecycle>() &&
                                                    !IsTemplate(execution);

    /// <summary>新运行实例的预制标签在帧末移除，按实际资产原型身份区分，不能误拒绝刚生成的实例。</summary>
    /// <param name="entity">需要区分的实际实体。</param>
    private static bool IsTemplate(Entity entity) => entity.Tags.Has<TagPrefab>() &&
                                                     (!entity.TryGetComponent(out SkillEntity skill) ||
                                                      skill.Asset == null || skill.Asset.PrefabEntity == entity);

    /// <summary>只返回仍由本次施放控制的主本体。</summary>
    /// <param name="execution">实际施放。</param><param name="body">仍有效的主本体。</param>
    public static bool TryGetPrimaryBody(Entity execution, out Entity body)
    {
        if (!IsLive(execution))
        {
            body = default;
            return false;
        }

        var relations = execution.GetRelations<SkillExecutionBodyRelation>();
        for (int i = 0; i < relations.Length; i++)
        {
            SkillExecutionBodyRelation relation = relations[i];
            if (relation.role != "primary") continue;
            body = relation.body;
            return !body.IsNull && !body.Tags.Has<TagRecycle>() &&
                   body.TryGetComponent(out SkillExecutionBodyLease lease) && lease.execution == execution;
        }

        body = default;
        return false;
    }

    /// <summary>立即停止本次作用，统一回收阶段再释放本体关系。</summary>
    /// <param name="execution">需要结束的施放。</param>
    public static void RequestEnd(Entity execution)
    {
        if (execution.IsNull) return;

        ref SkillExecution state = ref execution.GetComponent<SkillExecution>();
        if (state.end_requested) return;

        state.end_requested = true;
        ModClass.I.CommandBuffer.AddTag<TagRecycle>(execution.Id);
    }
}