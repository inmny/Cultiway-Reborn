using System;

namespace Cultiway.Core.Coordination;

/// <summary>一个协调行动席位的固定约束。</summary>
public sealed class CoordinationRoleDefinition
{
    /// <summary>席位标识；只在所属行动定义内要求唯一。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>进入集合阶段前至少需要分配的成员数。</summary>
    public int MinimumCount { get; set; }

    /// <summary>允许分配的最大成员数；小于等于零表示不设上限。</summary>
    public int MaximumCount { get; set; }

    /// <summary>进入执行阶段前该席位至少需要实际到场的人数。</summary>
    public int MinimumReadyCount { get; set; }

    /// <summary>该席位采用的自愿、职责或强制参加策略。</summary>
    public CoordinationParticipationMode ParticipationMode { get; set; }

    /// <summary>行动进入执行阶段后是否仍允许补充该席位。</summary>
    public bool AllowLateJoin { get; set; }

    /// <summary>参与关系在离开执行任务后释放，还是持续到整个活动结束。</summary>
    public CoordinationParticipantLifetime ParticipantLifetime { get; set; } =
        CoordinationParticipantLifetime.ExecutionBound;

    /// <summary>角色只有执行这些任务时才会被视为正在履行席位并计算到场。</summary>
    public string[] ExecutionTaskIds { get; set; } = Array.Empty<string>();
}

/// <summary>协调行动的静态定义；具体领域数据由每次启动时创建的会话保存。</summary>
public sealed class CoordinatedActivityDefinitionAsset : Asset
{
    /// <summary>同一角色出现行动冲突时使用的优先级，数值越大越优先。</summary>
    public int Priority { get; set; }

    /// <summary>当前行动是否允许被更高优先级行动抢占。</summary>
    public bool Preemptible { get; set; } = true;

    /// <summary>招募阶段允许持续的模拟秒数。</summary>
    public float RecruitmentTimeoutSeconds { get; set; } = 5f;

    /// <summary>集合阶段允许持续的模拟秒数。</summary>
    public float AssemblyTimeoutSeconds { get; set; } = 5f;

    /// <summary>全部执行阶段累计允许的最长时间；小于等于零表示由会话自行结束。</summary>
    public float RunningTimeoutSeconds { get; set; }

    /// <summary>服务更新该行动的最小间隔。</summary>
    public float HeartbeatSeconds { get; set; } = 0.25f;

    /// <summary>除各席位要求外，整个行动至少需要到场的人数。</summary>
    public int MinimumReadyCount { get; set; }

    /// <summary>已分配成员中至少需要到场的比例，取值会被限制在 0 到 1。</summary>
    public float MinimumReadyRatio { get; set; } = 1f;

    /// <summary>执行阶段失去集合条件时采用的统一生命周期策略。</summary>
    public CoordinationRunningReadinessPolicy RunningReadinessPolicy { get; set; }

    /// <summary>当前行动包含的扁平角色席位。</summary>
    public CoordinationRoleDefinition[] Roles { get; set; } = Array.Empty<CoordinationRoleDefinition>();

    /// <summary>按标识解析一个角色席位。</summary>
    public CoordinationRoleDefinition GetRole(string roleId)
    {
        for (var i = 0; i < Roles.Length; i++)
        {
            CoordinationRoleDefinition role = Roles[i];
            if (role != null && role.Id == roleId) return role;
        }
        return null;
    }
}

/// <summary>协调行动定义使用的资产库。</summary>
public sealed class CoordinatedActivityDefinitionLibrary : AssetLibrary<CoordinatedActivityDefinitionAsset>
{
}
