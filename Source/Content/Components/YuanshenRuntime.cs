using System;
using System.Collections.Generic;
using Friflo.Engine.ECS;
using UnityEngine;

namespace Cultiway.Content.Components;

/// <summary>命魂出窍时使用的三种心神分配姿态。</summary>
public enum YuanshenTravelStance : byte
{
    /// <summary>外放四成可用心神，肉身保留六成。</summary>
    Guarded,

    /// <summary>外放六成可用心神，肉身保留四成。</summary>
    Balanced,

    /// <summary>外放九成可用心神，肉身保留一成并继续独立行动。</summary>
    FullRelease
}

/// <summary>元神节点在唯一人物会话中的角色。</summary>
public enum YuanshenNodeRole : byte
{
    /// <summary>不具备身份继承资格的普通分念。</summary>
    Thought,

    /// <summary>短时高投入战斗法相。</summary>
    DharmaForm,

    /// <summary>绑定载体的稳定化身。</summary>
    Avatar,

    /// <summary>依托授权锚点出现的短时显圣投影。</summary>
    Manifestation
}

/// <summary>元神节点当前执行的移动状态。</summary>
public enum YuanshenNodeAction : byte
{
    /// <summary>停在当前位置等待命令。</summary>
    Idle,

    /// <summary>前往一个明确指定的地面坐标。</summary>
    Moving,

    /// <summary>返回所属人物当前主位置。</summary>
    Returning,

    /// <summary>节点已击破，等待唯一生命循环入口完成结算。</summary>
    Broken
}

/// <summary>命魂载体与肉身之间的牵引状态。</summary>
public enum YuanshenTetherCondition : byte
{
    /// <summary>牵引完整，能够正常归返。</summary>
    Stable,

    /// <summary>牵引受到轻度干扰。</summary>
    Fluctuating,

    /// <summary>牵引持续受压，归返明显变慢。</summary>
    Obstructed,

    /// <summary>牵引已经切断，命魂无法依靠肉身归返。</summary>
    Severed
}

/// <summary>人物持有的元神节点会话与心神份额总账。</summary>
public struct YuanshenRuntimeState : IComponent
{
    /// <summary>当前临时命魂人物的稳定编号；命魂在体或人物无身时为零。</summary>
    public long soul_carrier_actor_id;

    /// <summary>当前活动会话的稳定编号。</summary>
    public long session_id;

    /// <summary>最近一次创建节点使用的生成代次。</summary>
    public int generation;

    /// <summary>当前临时命魂人物的创建代次，不随其他节点创建变化。</summary>
    public int soul_carrier_generation;

    /// <summary>下一个可分配的会话内逻辑节点编号。</summary>
    public int next_logical_id;

    /// <summary>当前心神分配姿态。</summary>
    public YuanshenTravelStance stance;

    /// <summary>命魂所在位置承载的可用心神百分比。</summary>
    public float main_soul_share;

    /// <summary>命魂离体后留在肉身中的无命魂残留份额。</summary>
    public float body_residual_share;

    /// <summary>因节点受创而暂时无法使用的心神百分比。</summary>
    public float injury_locked_share;

    /// <summary>按整秒结算维持消耗的累计时间。</summary>
    public float upkeep_elapsed;

    /// <summary>按整秒结算创伤恢复的累计时间。</summary>
    public float recovery_elapsed;

    /// <summary>本次离体已经持续的时间。</summary>
    public float travel_elapsed;

    /// <summary>当前活动普通分念的稳定句柄；只属于本次运行时。</summary>
    public List<YuanshenNodeHandle> thought_nodes;

    /// <summary>当前活动法相、化身和显圣投影的稳定句柄。</summary>
    public List<YuanshenNodeHandle> advanced_nodes;

    /// <summary>玩家或决策当前聚焦的元神节点；失效句柄视为没有聚焦。</summary>
    public YuanshenNodeHandle focused_node;

    /// <summary>下一次可以使用基础神念攻击的世界时间。</summary>
    public double soul_strike_ready_at;

    /// <summary>距离下一次元神决策的秒数。</summary>
    public float think_cooldown;

    /// <summary>最近一次由决策明确选定的战斗目标编号。</summary>
    public long decision_combat_target_id;

    /// <summary>命魂当前是否由有效临时人物承载。</summary>
    public readonly bool IsOutside => soul_carrier_actor_id > 0L;

    /// <summary>当前仍可参与分配的心神百分比。</summary>
    public readonly float AvailableShare => Mathf.Clamp(100f - injury_locked_share, 0f, 100f);
}

/// <summary>临时命魂人物当前执行的移动状态。</summary>
public enum YuanshenSoulCarrierAction : byte
{
    /// <summary>停在当前位置，由普通人物战斗逻辑自行行动。</summary>
    Idle,

    /// <summary>前往玩家或战斗系统明确指定的位置。</summary>
    Moving,

    /// <summary>沿牵引返回原人物肉身。</summary>
    Returning,

    /// <summary>完整度已经归零，等待生命循环结算。</summary>
    Broken,
}

/// <summary>临时命魂人物与唯一所有者之间的运行时绑定。</summary>
public struct YuanshenSoulCarrierState : IComponent
{
    /// <summary>技能、资源、身份和击杀归属人物的稳定编号。</summary>
    public long owner_actor_id;

    /// <summary>所属元神活动会话编号。</summary>
    public long session_id;

    /// <summary>创建代次，用于拒绝已经失效的临时人物。</summary>
    public int generation;

    /// <summary>当前承载的可用心神百分比。</summary>
    public float mind_share;

    /// <summary>当前完整度上限。</summary>
    public float maximum_integrity;

    /// <summary>当前剩余完整度。</summary>
    public float current_integrity;

    /// <summary>已经因完整度损失转入创伤的心神份额。</summary>
    public float locked_share;

    /// <summary>当前明确移动目标。</summary>
    public Vector2 destination;

    /// <summary>当前移动与归返状态。</summary>
    public YuanshenSoulCarrierAction action;

    /// <summary>当前牵引状态。</summary>
    public YuanshenTetherCondition tether_condition;

    /// <summary>牵引受到干扰的累计秒数。</summary>
    public float interference_seconds;

    /// <summary>最近一次牵引干扰发生的世界时间。</summary>
    public double last_interference_at;

    /// <summary>最近一次有效神魂伤害的攻击者人物编号。</summary>
    public long last_attacker_actor_id;

    /// <summary>重新提交寻路命令的累计秒数。</summary>
    public float movement_refresh_elapsed;

    /// <summary>当前完整度比例。</summary>
    public readonly float IntegrityRatio => maximum_integrity > 0f
        ? Mathf.Clamp01(current_integrity / maximum_integrity)
        : 0f;
}

/// <summary>一枚元神节点的稳定身份、角色、移动、完整度与牵引状态。</summary>
public struct YuanshenNodeState : IComponent
{
    /// <summary>节点所属人物的稳定编号。</summary>
    public long owner_actor_id;

    /// <summary>节点所属活动会话编号。</summary>
    public long session_id;

    /// <summary>节点在会话内不重复的逻辑编号。</summary>
    public int logical_id;

    /// <summary>节点创建代次，防止实体编号回收后误中新的节点。</summary>
    public int generation;

    /// <summary>节点角色。</summary>
    public YuanshenNodeRole role;

    /// <summary>节点当前承载的可用心神百分比。</summary>
    public float mind_share;

    /// <summary>节点当前移动状态。</summary>
    public YuanshenNodeAction action;

    /// <summary>当前地面移动目标。</summary>
    public Vector2 move_target;

    /// <summary>每秒移动的世界格数。</summary>
    public float move_speed;

    /// <summary>创建节点时冻结的完整度上限。</summary>
    public float integrity_maximum;

    /// <summary>节点当前剩余完整度。</summary>
    public float integrity_current;

    /// <summary>创建节点时划入的初始心神份额。</summary>
    public float allocated_share;

    /// <summary>已经因完整度损失转入创伤锁定的份额。</summary>
    public float locked_share;

    /// <summary>当前牵引状态。</summary>
    public YuanshenTetherCondition tether_condition;

    /// <summary>近期切割与压制累计的干扰秒数。</summary>
    public float tether_interference_seconds;

    /// <summary>最近一次增加牵引干扰的世界时间。</summary>
    public double tether_last_interference_at;

    /// <summary>当前完整度比例。</summary>
    public readonly float IntegrityRatio => integrity_maximum > 0f
        ? Mathf.Clamp01(integrity_current / integrity_maximum)
        : 0f;

    /// <summary>取当前状态的稳定节点句柄。</summary>
    /// <returns>包含全部稳定身份字段的句柄。</returns>
    public readonly YuanshenNodeHandle GetHandle()
    {
        return new YuanshenNodeHandle(in this);
    }
}

/// <summary>节点当前任务类型。</summary>
public enum YuanshenNodeTaskKind : byte
{
    /// <summary>停留在当前位置。</summary>
    Idle,

    /// <summary>移动到明确指定的地面点。</summary>
    Move,

    /// <summary>守护一个明确地点。</summary>
    GuardPoint,

    /// <summary>跟随一个已知友方人物。</summary>
    FollowActor,

    /// <summary>追踪一枚刚在魂战中锁定的敌方元神节点。</summary>
    TrackLockedNode,

    /// <summary>远程控制一件已经祭炼的法器。</summary>
    ControlArtifact,

    /// <summary>攻击一名已经明确指定的敌方人物。</summary>
    EngageActor,

    /// <summary>沿已经建立的锚点连接进行引导迁移。</summary>
    AnchorTransit,

    /// <summary>返回人物当前主位置。</summary>
    Return
}

/// <summary>一个元神节点同一时刻唯一执行的任务。</summary>
public struct YuanshenNodeTask : IComponent
{
    /// <summary>任务类型。</summary>
    public YuanshenNodeTaskKind kind;

    /// <summary>移动、守护或追踪目标位置。</summary>
    public Vector2 point;

    /// <summary>人物或建筑目标的稳定编号。</summary>
    public long target_object_id;

    /// <summary>敌方元神节点目标的稳定句柄。</summary>
    public YuanshenNodeHandle target_node;

    /// <summary>法器实体的当前编号；每次使用仍需校验祭炼归属。</summary>
    public int artifact_entity_id;

    /// <summary>任务目标刷新累计秒数。</summary>
    public float update_elapsed;

    /// <summary>任务开始的世界时间。</summary>
    public double started_at;

    /// <summary>任务最晚结束时间；零表示没有固定期限。</summary>
    public double expires_at;
}

/// <summary>法器当前由哪一道元神节点远程控制。</summary>
public struct ArtifactYuanshenControl : IComponent
{
    /// <summary>法器祭炼与资源归属人物编号。</summary>
    public long owner_actor_id;

    /// <summary>提供法器世界起点的元神节点句柄。</summary>
    public YuanshenNodeHandle node;
}

/// <summary>可以跨帧保存并稳定失效的元神节点目标句柄。</summary>
public readonly struct YuanshenNodeHandle : IEquatable<YuanshenNodeHandle>
{
    /// <summary>节点所属人物编号。</summary>
    public readonly long OwnerActorId;

    /// <summary>节点所属活动会话编号。</summary>
    public readonly long SessionId;

    /// <summary>节点在会话内的逻辑编号。</summary>
    public readonly int LogicalId;

    /// <summary>节点生成代次。</summary>
    public readonly int Generation;

    /// <summary>从一枚当前节点状态创建稳定句柄。</summary>
    /// <param name="state">当前节点状态。</param>
    public YuanshenNodeHandle(in YuanshenNodeState state)
    {
        OwnerActorId = state.owner_actor_id;
        SessionId = state.session_id;
        LogicalId = state.logical_id;
        Generation = state.generation;
    }

    /// <summary>判断两个句柄的全部稳定字段是否相同。</summary>
    /// <param name="other">另一个句柄。</param>
    /// <returns>四项身份完全一致时返回真。</returns>
    public bool Equals(YuanshenNodeHandle other)
    {
        return OwnerActorId == other.OwnerActorId && SessionId == other.SessionId &&
               LogicalId == other.LogicalId && Generation == other.Generation;
    }

    /// <summary>判断一个对象是否为相同节点句柄。</summary>
    /// <param name="obj">待比较对象。</param>
    /// <returns>对象是相同句柄时返回真。</returns>
    public override bool Equals(object obj)
    {
        return obj is YuanshenNodeHandle other && Equals(other);
    }

    /// <summary>生成包含全部稳定字段的散列值。</summary>
    /// <returns>稳定散列值。</returns>
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = OwnerActorId.GetHashCode();
            hash = hash * 397 ^ SessionId.GetHashCode();
            hash = hash * 397 ^ LogicalId;
            hash = hash * 397 ^ Generation;
            return hash;
        }
    }

    /// <summary>判断两个句柄是否相同。</summary>
    public static bool operator ==(YuanshenNodeHandle left, YuanshenNodeHandle right) => left.Equals(right);

    /// <summary>判断两个句柄是否不同。</summary>
    public static bool operator !=(YuanshenNodeHandle left, YuanshenNodeHandle right) => !left.Equals(right);
}
